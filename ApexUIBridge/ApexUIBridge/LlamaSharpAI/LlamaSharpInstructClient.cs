using LLama;
using LLama.Common;
using LLama.Sampling;

using System.Text;

namespace ApexUIBridge.LlamaSharpAI;

public sealed class LlamaSharpInstructClient : IDisposable
{
    const string InstructionPrefix = "[INST]";
    const string InstructionSuffix = "[/INST]";

    private LLamaWeights? _model;
    private LLamaContext? _context;
    private InstructExecutor? _executor;
    private CancellationTokenSource? _cts;
    private string _loadedModelPath = "";
    private string _systemInstruction = "";
    private bool _isFirstMessage = true;

    public bool IsLoaded => _model != null && _executor != null;

    /// <summary>
    /// Sampling temperature used during inference. Lower = more focused/deterministic,
    /// higher = more creative. Defaults to 0.8.
    /// </summary>
    public float Temperature { get; set; } = 0.8f;

    /// <summary>
    /// Reasoning effort level for models that support extended thinking.
    /// Accepted values: "low", "medium", "high". Empty = model default.
    /// Appended to the system message as "Reasoning: {effort}" on the first turn.
    /// </summary>
    public string ReasoningEffort { get; set; } = "medium";

    /// <summary>Number of CPU threads used for inference.</summary>
    public int Threads { get; set; } = 10;

    /// <summary>KV-cache context size in tokens.</summary>
    public int ContextSize { get; set; } = 4096;

    /// <summary>Maximum tokens to generate per response.</summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>Number of layers to offload to GPU.</summary>
    public int GpuLayers { get; set; } = 10;

    /// <summary>Anti-prompt strings that terminate generation.</summary>
    public List<string> AntiPrompts { get; set; } = new() { InstructionPrefix };

    /// <summary>Tokens per second for the most recently completed generation.</summary>
    public float LastTokensPerSecond { get; private set; }

    /// <summary>Seconds from send to first token for the most recently completed generation.</summary>
    public float LastTimeToFirstToken { get; private set; }

    public event EventHandler<string>? StreamingTextReceived;
    public event EventHandler<string>? ThinkingReceived;
    public event EventHandler<int>? GenerationCompleted;

    /// <summary>
    /// Cancels an in-progress generation. Safe to call when idle.
    /// </summary>
    public void Stop() => _cts?.Cancel();

    /// <summary>
    /// Loads the model on a threadpool thread (matching the CodingAssistant pattern).
    /// Subsequent calls with the same path are a no-op — the existing executor continues.
    /// </summary>
    public async Task LoadModelAsync(string modelPath, string systemInstruction)
    {
        if (_model != null && _loadedModelPath == modelPath && _executor != null)
        {
            _systemInstruction = systemInstruction;
            return;
        }

        await Task.Run(async () =>
        {
            if (_model == null || _loadedModelPath != modelPath)
            {
                DisposeModel();
                _loadedModelPath = modelPath;

                var parameters = new ModelParams(modelPath)
                {
                    ContextSize = (uint)ContextSize,
                    Threads = Threads
                };

                _model = await LLamaWeights.LoadFromFileAsync(parameters);
                _context = _model.CreateContext(parameters);
            }

            _systemInstruction = systemInstruction;
            ResetExecutor();
        });
    }

    private void ResetExecutor()
    {
        _executor = new InstructExecutor(_context!, InstructionPrefix, InstructionSuffix, null);
        _isFirstMessage = true;
    }

    /// <summary>
    /// Appends "Reasoning: {effort}" to the system message so the model sees it in
    /// the system block — matching the format used by gpt-oss-20b / LM Studio:
    ///   Reasoning: low | medium | high
    /// Returns an empty string when effort is null or unrecognised.
    /// </summary>
    private static string BuildReasoningLine(string? effort)
    {
        return effort?.ToLowerInvariant() switch
        {
            "low" or "medium" or "high" => $"\nReasoning: {effort.ToLowerInvariant()}",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Sends an instruction and streams tokens back via <see cref="StreamingTextReceived"/>.
    /// Pre-final channel content (thinking) is fired via <see cref="ThinkingReceived"/>.
    /// The system instruction is prepended to the first message only (matching CodingAssistant).
    /// Inference runs on a threadpool thread so the UI remains responsive.
    /// Returns the full response text when complete.
    /// </summary>
    public async Task<string> SendMessageAsync(string userText, float temperature = -1f, int maxTokens = -1)
    {
        if (_executor == null)
            throw new InvalidOperationException("Model not loaded. Call LoadModelAsync first.");

        if (temperature < 0) temperature = Temperature;
        if (maxTokens < 0) maxTokens = MaxTokens;

        // Build the instruction. On the first message, prepend the system instruction with
        // the reasoning-effort line appended to it — matching the gpt-oss-20b system block:
        //   <system>...user system text...\nReasoning: medium</system>
        string systemPrefix = "";
        if (_isFirstMessage && !string.IsNullOrWhiteSpace(_systemInstruction))
        {
            string reasoningLine = BuildReasoningLine(ReasoningEffort);
            systemPrefix = _systemInstruction + reasoningLine + "\n\n";
        }

        var instruction = systemPrefix + userText;
        _isFirstMessage = false;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var inferenceParams = new InferenceParams
        {
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = temperature
            },
            ReasoningEffort = string.IsNullOrWhiteSpace(ReasoningEffort) ? null : ReasoningEffort,
            MaxTokens = maxTokens,
            AntiPrompts = AntiPrompts
        };

        var sb = new StringBuilder();
        int tokenCount = 0;
        var startTime = DateTime.UtcNow;
        DateTime? firstTokenTime = null;

        try
        {
            await Task.Run(async () =>
            {
                // gpt-oss-20b (and similar models) structure output as channel blocks:
                //   analysis\n{reasoning}  commentary\n{...}  final\n{actual response}
                // Buffer tokens until "final\n" is detected, firing ThinkingReceived for
                // pre-final content. The full sb always accumulates every token so
                // [CMD:] parsing is unaffected. If no channel format is detected
                // (model doesn't use channels), the full buffer is emitted as a fallback.
                const string FinalMarker = "final\n";
                var pending = new StringBuilder();
                bool inFinalChannel = false;

                await foreach (var tok in _executor.InferAsync(instruction + Environment.NewLine, inferenceParams)
                    .WithCancellation(token))
                {
                    sb.Append(tok);
                    tokenCount++;

                    if (firstTokenTime == null)
                        firstTokenTime = DateTime.UtcNow;

                    if (inFinalChannel)
                    {
                        StreamingTextReceived?.Invoke(this, tok);
                    }
                    else
                    {
                        pending.Append(tok);
                        var buf = pending.ToString();
                        var idx = buf.IndexOf(FinalMarker, StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            // Fire any accumulated thinking content before the final marker
                            if (idx > 0)
                                ThinkingReceived?.Invoke(this, buf[..idx]);

                            inFinalChannel = true;
                            var after = buf[(idx + FinalMarker.Length)..];
                            if (after.Length > 0)
                                StreamingTextReceived?.Invoke(this, after);
                            pending.Clear();
                        }
                        else if (buf.Length > 200)
                        {
                            // Sliding window — emit safe prefix to ThinkingReceived, keep
                            // only enough to detect the marker boundary
                            var safeLen = buf.Length - FinalMarker.Length;
                            ThinkingReceived?.Invoke(this, buf[..safeLen]);
                            pending.Remove(0, safeLen);
                        }
                    }
                }

                // Fallback: model doesn't use channel format — emit everything
                if (!inFinalChannel && pending.Length > 0)
                    StreamingTextReceived?.Invoke(this, pending.ToString());
            }, token);
        }
        catch (OperationCanceledException)
        {
            // Generation was stopped — return whatever was accumulated
        }

        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
        LastTokensPerSecond = elapsed > 0 ? tokenCount / (float)elapsed : 0f;
        LastTimeToFirstToken = firstTokenTime.HasValue
            ? (float)(firstTokenTime.Value - startTime).TotalSeconds
            : 0f;

        GenerationCompleted?.Invoke(this, tokenCount);
        return sb.ToString();
    }

    /// <summary>
    /// Resets the executor with a fresh context (keeps the loaded model).
    /// </summary>
    public void ResetConversation(string systemInstruction)
    {
        if (_model != null && _context != null)
        {
            _systemInstruction = systemInstruction;
            ResetExecutor();
        }
    }

    private void DisposeModel()
    {
        _executor = null;
        _context?.Dispose();
        _context = null;
        _model?.Dispose();
        _model = null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        DisposeModel();
    }
}
