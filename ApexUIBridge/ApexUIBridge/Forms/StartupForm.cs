using FlaUI.Core;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

using ApexUIBridge.Core;
using ApexUIBridge.Core.Logger;
using ApexUIBridge.Models;
using ApexUIBridge.Settings;
using ApexUIBridge.ViewModels;

using AnthropicMinimal;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using ApexUIBridge.LlamaSharpAI;

using AiMessage = AnthropicMinimal.Message;
using Debug = System.Diagnostics.Debug;

namespace ApexUIBridge.Forms;

/// <summary>
/// The main application window and central orchestrator of ApexUIBridge.
///
/// Responsibilities:
/// <list type="bullet">
///   <item>Displays the running-process grid and delegates process discovery to
///         <see cref="ViewModels.StartupViewModel"/>.</item>
///   <item>Loads a selected process into a <see cref="ViewModels.ProcessViewModel"/>
///         and populates the UI tree, details panel, and patterns grid.</item>
///   <item>Wires toolbar buttons (refresh, capture, highlight, hover, focus,
///         XPath, copy JSON/XML) to the active <see cref="ViewModels.ProcessViewModel"/>.</item>
///   <item>Hosts the Finder panel (<see cref="Core.ElementFinderService"/>) for
///         structured window/element searches.</item>
///   <item>Embeds the AI chat panel: routes user messages to one of three
///         configurable providers — Anthropic (cloud), LlamaSharp Chat (local GGUF),
///         or LlamaSharp Instruct (local GGUF) — and optionally auto-executes
///         <c>[CMD: …]</c> Bridge commands parsed from the AI response.</item>
///   <item>Persists AI settings to <c>%AppData%\ApexUIBridge\ai-settings.json</c>
///         via <see cref="Settings.JsonSettingsService{T}"/>.</item>
///   <item>Runs a background sync timer (<see cref="_windowChangeTimer"/>) to keep
///         the process list current without a manual refresh.</item>
/// </list>
///
/// Layout note: all WinForms controls are declared in
/// <c>StartupForm.Designer.cs</c>; this file contains only logic.
/// </summary>
public partial class StartupForm : Form
{

    private string API_KEY { get; set; } = string.Empty;

    private string systemMessageCommands { get; set; } = @"You are an AI assistant with direct control of Windows applications. Execute UI commands by outputting them in this format: [CMD: COMMAND args]

### Available Commands
CLICK <id> | DOUBLE_CLICK <id> | RIGHT_CLICK <id> | MIDDLE_CLICK <id>
TYPE <id> <text> | SEND_KEYS <id> <keys> | SET_VALUE <id> <value>
SELECT <id> | SELECT_BY_TEXT <id> <text> | SELECT_BY_INDEX <id> <index>
SCROLL <id> <direction> [amount] | SET_SLIDER <id> <value>
TOGGLE <id> | EXPAND <id> | COLLAPSE <id> | FOCUS <id> | HOVER <id>
WINDOW_ACTION <id> <action> | LIST_WINDOWS | SCAN_WINDOW <windowName> | SEARCH <text>
GET_TEXT <id> | GET_ELEMENT <id> | GET_TREE [depth] | CAPTURE <id>
DRAG <id> <x> <y> | DRAG_TO_ELEMENT <sourceId> <targetId>

### Rules — STRICTLY FOLLOW THESE
1. Only ONE [CMD:] per response. Never batch multiple commands.
2. You MUST wait for the [RESULT:] before issuing the next command. Do NOT anticipate results.
3. NEVER invent or guess element IDs. You do NOT know any IDs until SCAN_WINDOW returns them.
4. Only use IDs that appear in the most recent scan/search results.

### Workflow
1. [CMD: LIST_WINDOWS] — see available windows
2. [CMD: SCAN_WINDOW <windowName>] — returns the full element tree with all element IDs and names
3. Read the returned element tree. Extract the exact IDs you need.
4. Issue ONE action command using an ID from the results (e.g. [CMD: CLICK <id>])
5. Wait for the result, then issue the next command

### Example (multi-turn)
User: Click the Save button in Notepad

Assistant turn 1:
I'll scan the Notepad window first.
[CMD: SCAN_WINDOW Notepad]

(system returns results with real IDs)

Assistant turn 2:
I can see the Save button with ID <real_id_from_results>. Clicking it now.
[CMD: CLICK <real_id_from_results>]

(system returns success)

Assistant turn 3:
Done! The Save button was clicked.";

    private readonly StartupViewModel _viewModel = new();
    private readonly InternalLogger _logger;
    private readonly ElementOperations _elementOps = new();
    private readonly BindingList<ProcessWindowInfo> _gridItems = new();
    private readonly AutomationBase _finderAutomation = new UIA3Automation();
    private ProcessViewModel? _processViewModel;
    private bool _isSyncingProcesses;
    private readonly List<AiMessage> _aiConversationHistory = new();
    private LlamaSharpClient? _llamaClient;
    private LlamaSharpInstructClient? _llamaInstructClient;

    private readonly Bridge _bridge = new Bridge();


    // Settings persistence
    private readonly ISettingsService<AiSettings> _aiSettingsService = new JsonSettingsService<AiSettings>(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ApexUIBridge", "ai-settings.json"));
    private AiSettings _aiSettings = new();

    // System stats timer
    private readonly System.Windows.Forms.Timer _sysStatsTimer = new() { Interval = 1500 };
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTime _lastCpuSample = DateTime.UtcNow;

    public StartupForm(InternalLogger logger)
    {
        _logger = logger;
        InitializeComponent();
        WireEvents();

        _grid.DataSource = _gridItems;

        HoverManager.Initialize(new UIA3Automation(), () => App.FlaUiAppOptions.HoverOverlay(), _logger);

        // Ensure settings directory exists and load settings
        Directory.CreateDirectory(Path.GetDirectoryName(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ApexUIBridge", "ai-settings.json"))!);
        _aiSettings = _aiSettingsService.Load();

        // Apply loaded settings to UI
        ApplySettingsToUI();

        SetupProviderCombo();
    }

    #region AI Chat

    private void ToggleAiChatPanel()
    {
        _contentSplit.Panel2Collapsed = !_contentSplit.Panel2Collapsed;
    }

    private void AiSetUIEnabled(bool enabled)
    {
        _btnAiSend.Enabled = enabled;
        _btnAiStream.Enabled = enabled;
        _aiInput.Enabled = enabled;
        AiSetIndicator(!enabled);
    }

    private void AiAppendOutput(string text)
    {
        if (InvokeRequired) { Invoke(() => AiAppendOutput(text)); return; }

        _aiOutput.SelectionColor = Color.FromArgb(220, 220, 220);
        _aiOutput.AppendText(text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        _aiOutput.ScrollToCaret();
    }

    private void AiAppendThinking(string text)
    {
        if (InvokeRequired) { Invoke(() => AiAppendThinking(text)); return; }
        if (!_aiSettings.ShowThinking) return;

        _aiOutput.SelectionColor = Color.FromArgb(100, 100, 100);
        _aiOutput.AppendText(text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        _aiOutput.ScrollToCaret();
    }

    private void AiSetIndicator(bool running)
    {
        if (InvokeRequired) { Invoke(() => AiSetIndicator(running)); return; }
        _aiIndicatorLabel.ForeColor = running ? Color.LimeGreen : Color.Gray;
        _btnAiStop.Enabled = running;
    }

    private void ApplySettingsToUI()
    {
        // API key
        if (!string.IsNullOrEmpty(_aiSettings.AnthropicApiKey))
            _aiApiKeyBox.Text = _aiSettings.AnthropicApiKey;
        else if (!string.IsNullOrEmpty(API_KEY))
            _aiApiKeyBox.Text = API_KEY;
        else
        {
            var envKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrEmpty(envKey))
                _aiApiKeyBox.Text = envKey;
        }

        // Model path (LlamaSharp)
        _aiModelPathBox.Text = _aiSettings.ModelPath;

        // System prompt
        _aiSystemBox.Text = string.IsNullOrEmpty(_aiSettings.SystemPrompt)
            ? systemMessageCommands
            : _aiSettings.SystemPrompt;

        // Auto-exec
        _chkAiAutoExec.Checked = _aiSettings.AutoExec;

        // Anthropic model selection
        int modelIdx = _aiModelCombo.Items.IndexOf(_aiSettings.AnthropicModel);
        if (modelIdx >= 0) _aiModelCombo.SelectedIndex = modelIdx;
        else if (_aiModelCombo.Items.Count > 0) _aiModelCombo.SelectedIndex = 0;
    }

    private void AiClearChat()
    {
        _aiConversationHistory.Clear();
        _llamaClient?.ResetConversation(_aiSystemBox.Text);
        _llamaInstructClient?.ResetConversation(_aiSystemBox.Text);
        _aiOutput.Clear();
        _aiStatusLabel.Text = "Chat cleared";
        AiSetIndicator(false);
        _aiStatsLabel.Text = "";
    }

    private async Task AiSendMessageAsync(bool stream)
    {
        if (_aiProviderCombo.SelectedItem?.ToString() == "LlamaSharp (Local)")
        {
            await AiSendLlamaMessageAsync();
            return;
        }

        if (_aiProviderCombo.SelectedItem?.ToString() == "LlamaSharp Instruct")
        {
            await AiSendLlamaInstructMessageAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(_aiApiKeyBox.Text))
        {
            MessageBox.Show(this, "Please enter your API key.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var userText = _aiInput.Text.Trim();
        if (string.IsNullOrEmpty(userText)) return;

        AiSetUIEnabled(false);
        _aiInput.Clear();

        AiAppendOutput($"\n--- USER ---\n{userText}\n\n--- ASSISTANT ---\n");
        _aiConversationHistory.Add(AiMessage.CreateUserMessage(userText));

        var selectedModel = _aiModelCombo.SelectedItem?.ToString() ?? AnthropicMinimal.Models.Claude45Haiku;

        try
        {
            using var client = new AnthropicClient(_aiApiKeyBox.Text);

            // Auto-continue loop: keep calling the AI until no more commands
            const int maxTurns = 20;
            for (int turn = 0; turn < maxTurns; turn++)
            {
                var request = new MessageRequest
                {
                    Model = selectedModel,
                    MaxTokens = 4096,
                    Messages = new List<AiMessage>(_aiConversationHistory),
                    System = string.IsNullOrWhiteSpace(_aiSystemBox.Text) ? null : _aiSystemBox.Text
                };

                string responseText;

                if (stream)
                {
                    responseText = await AiSendStreamingAsync(client, request);
                }
                else
                {
                    _aiStatusLabel.Text = "Sending...";
                    var response = await client.SendMessageAsync(request);
                    responseText = response?.GetText() ?? "(empty response)";
                    AiAppendOutput(responseText);
                    _aiStatusLabel.Text = $"Done — {response?.Usage.InputTokens ?? 0} in / {response?.Usage.OutputTokens ?? 0} out tokens";
                }

                _aiConversationHistory.Add(AiMessage.CreateAssistantMessage(responseText));
                AiAppendOutput("\n");

                if (!_chkAiAutoExec.Checked && !_aiSettings.AutoExec)
                    break;

                var commands = AiParseCommands(responseText);
                if (commands.Count == 0)
                    break; // No commands = AI is done, exit loop

                // Execute commands and add results to history
                await AiExecuteCommandsFromResponse(commands);

                // Loop continues — will call AI again with results
                AiAppendOutput("\n--- ASSISTANT ---\n");
            }
        }
        catch (Exception ex)
        {
            AiAppendOutput($"\n[ERROR] {ex.Message}\n");
            _aiStatusLabel.Text = "Error";
        }
        finally
        {
            AiSetUIEnabled(true);
            _aiInput.Focus();
        }
    }

    private async Task<string> AiSendStreamingAsync(AnthropicClient client, MessageRequest request)
    {
        _aiStatusLabel.Text = "Streaming...";
        var fullText = new StringBuilder();

        client.StreamingTextReceived += (_, text) =>
        {
            fullText.Append(text);
            AiAppendOutput(text);
        };

        client.MessageDelta += (_, evt) =>
        {
            Invoke(() => _aiStatusLabel.Text = $"Done — {evt.Usage.OutputTokens} output tokens");
        };

        client.Error += (_, evt) =>
        {
            Invoke(() =>
            {
                AiAppendOutput($"\n[STREAM ERROR] {evt.Error.Message}\n");
                _aiStatusLabel.Text = "Stream error";
            });
        };

        await client.SendMessageStreamAsync(request);
        return fullText.ToString();
    }

    private async Task<string> AiExecuteCommandsFromResponse(List<string> commands)
    {
        AiAppendOutput($"\n--- EXECUTING {commands.Count} COMMAND(S) ---\n");
        var resultMessages = new List<string>();

        foreach (var cmd in commands)
        {
            AiAppendOutput($"[CMD] {cmd}\n");

            System.Diagnostics.Debug.WriteLine($"[CMD] {cmd}\n");


            _aiStatusLabel.Text = $"Executing: {cmd}";

            var cmdResult = await _bridge.ExecuteCommand(cmd);
            var status = cmdResult.IsSuccess ? "OK" : "FAIL";
            AiAppendOutput($"  [{status}] {cmdResult.Message}\n");

            if (!string.IsNullOrEmpty(cmdResult.Data))
                AiAppendOutput($"  Data: {cmdResult.Data}\n");

            // Include Data in the result sent back to the AI so it can see element trees, text content, etc.
            var resultLine = $"[RESULT: {cmd} - {status}: {cmdResult.Message}]";
            if (!string.IsNullOrEmpty(cmdResult.Data))
                resultLine += $"\nData:\n{cmdResult.Data}";
            resultMessages.Add(resultLine);
        }

        var resultSummary = resultMessages.Count > 0 ? string.Join("\n", resultMessages) : "";

        if (resultMessages.Count > 0)
        {
            AiAppendOutput($"\n--- COMMAND RESULTS ---\n{resultSummary}\n");
            _aiConversationHistory.Add(AiMessage.CreateUserMessage(resultSummary));
        }

        _aiStatusLabel.Text = $"Executed {commands.Count} command(s)";
        return resultSummary;
    }

    private static List<string> AiParseCommands(string responseText)
    {
        var commands = new List<string>();
        var matches = Regex.Matches(responseText, @"\[CMD:\s*([^\]]+)\]");
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
                commands.Add(match.Groups[1].Value.Trim());
        }
        return commands;
    }

    private void SetupProviderCombo()
    {
        // Wire events — controls themselves are declared in Designer.cs
        _aiProviderCombo.SelectedIndexChanged += OnProviderChanged;

        _btnAiBrowse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select GGUF Model File",
                Filter = "GGUF Models (*.gguf)|*.gguf|All Files (*.*)|*.*",
                CheckFileExists = true
            };
            if (!string.IsNullOrEmpty(_aiModelPathBox.Text))
                dlg.InitialDirectory = Path.GetDirectoryName(_aiModelPathBox.Text) ?? "";
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _aiModelPathBox.Text = dlg.FileName;
                _aiSettings.ModelPath = dlg.FileName;
                SaveSettings();
            }
        };

        _aiModelPathBox.Leave += (_, _) =>
        {
            _aiSettings.ModelPath = _aiModelPathBox.Text.Trim();
            SaveSettings();
        };

        // Select provider from saved settings, then update visibility
        int provIdx = _aiProviderCombo.Items.IndexOf(_aiSettings.Provider);
        _aiProviderCombo.SelectedIndex = provIdx >= 0 ? provIdx : 2; // default: LlamaSharp Instruct
        // SelectedIndex setter fires OnProviderChanged, so visibility is already set
    }

    private void OnProviderChanged(object? sender, EventArgs e)
    {
        var provider = _aiProviderCombo.SelectedItem?.ToString() ?? _aiSettings.Provider;
        bool isLlama = provider is "LlamaSharp (Local)" or "LlamaSharp Instruct";

        _lblAiApiKey.Visible = !isLlama;
        _aiApiKeyBox.Visible = !isLlama;
        _lblAiModelPath.Visible = isLlama;
        _aiModelPathBox.Visible = isLlama;
        _btnAiBrowse.Visible = isLlama;
        _lblAiModel.Visible = !isLlama;
        _aiModelCombo.Visible = !isLlama;

        // Persist provider change immediately so it survives unexpected exits
        SyncUIToSettings();
        SaveSettings();
    }

    private void SyncUIToSettings()
    {
        _aiSettings.Provider = _aiProviderCombo.SelectedItem?.ToString() ?? _aiSettings.Provider;
        _aiSettings.ModelPath = _aiModelPathBox.Text.Trim();
        _aiSettings.AnthropicApiKey = _aiApiKeyBox.Text.Trim();
        _aiSettings.AnthropicModel = _aiModelCombo.SelectedItem?.ToString() ?? _aiSettings.AnthropicModel;
        _aiSettings.AutoExec = _chkAiAutoExec.Checked;
        _aiSettings.SystemPrompt = _aiSystemBox.Text;
    }

    private void SaveSettings()
    {
        try { _aiSettingsService.Save(_aiSettings); }
        catch { /* non-critical */ }
    }

    private void OpenAiSettingsDialog()
    {
        SyncUIToSettings();
        using var dlg = new AiSettingsDialog(_aiSettings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            SaveSettings();
            ApplySettingsToUI();
            int idx = _aiProviderCombo.Items.IndexOf(_aiSettings.Provider);
            if (idx >= 0) _aiProviderCombo.SelectedIndex = idx;
            OnProviderChanged(null, EventArgs.Empty);
        }
    }

    private async Task AiSendLlamaMessageAsync()
    {
        var modelPath = _aiModelPathBox.Text.Trim();
        if (string.IsNullOrEmpty(modelPath))
        {
            MessageBox.Show(this, "Please enter the path to your GGUF model file.", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!File.Exists(modelPath))
        {
            MessageBox.Show(this, $"Model file not found:\n{modelPath}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var userText = _aiInput.Text.Trim();
        if (string.IsNullOrEmpty(userText)) return;

        AiSetUIEnabled(false);
        _aiInput.Clear();
        AiAppendOutput($"\n--- USER ---\n{userText}\n\n--- ASSISTANT ---\n");
        _aiConversationHistory.Add(AiMessage.CreateUserMessage(userText));

        try
        {
            _llamaClient ??= new LlamaSharpClient();
            _llamaClient.Temperature = _aiSettings.Temperature;
            _llamaClient.ReasoningEffort = _aiSettings.ReasoningEffort;
            _llamaClient.MaxTokens = _aiSettings.MaxTokens;
            _llamaClient.Threads = _aiSettings.Threads;
            _llamaClient.ContextSize = _aiSettings.ContextSize;
            _llamaClient.GpuLayers = _aiSettings.GpuLayers;
            if (_aiSettings.AntiPromptsChat.Count > 0)
                _llamaClient.AntiPrompts = _aiSettings.AntiPromptsChat;

            _aiStatusLabel.Text = _llamaClient.IsLoaded ? "Preparing..." : "Loading model...";
            await _llamaClient.LoadModelAsync(modelPath, _aiSystemBox.Text);

            const int maxTurns = 20;
            var currentInput = userText;

            for (int turn = 0; turn < maxTurns; turn++)
            {
                _aiStatusLabel.Text = "Generating...";

                EventHandler<string> streamHandler = (_, text) => AiAppendOutput(text);
                EventHandler<string> thinkingHandler = (_, text) => AiAppendThinking(text);
                EventHandler<int> completeHandler = (_, count) =>
                {
                    if (InvokeRequired) Invoke(() => UpdateStatsLabel(_llamaClient));
                    else UpdateStatsLabel(_llamaClient);
                };

                _llamaClient.StreamingTextReceived += streamHandler;
                _llamaClient.ThinkingReceived += thinkingHandler;
                _llamaClient.GenerationCompleted += completeHandler;

                string responseText;
                try
                {
                    responseText = await _llamaClient.SendMessageAsync(currentInput);
                }
                finally
                {
                    _llamaClient.StreamingTextReceived -= streamHandler;
                    _llamaClient.ThinkingReceived -= thinkingHandler;
                    _llamaClient.GenerationCompleted -= completeHandler;
                }

                _aiConversationHistory.Add(AiMessage.CreateAssistantMessage(responseText));
                AiAppendOutput("\n");

                if (!_chkAiAutoExec.Checked && !_aiSettings.AutoExec) break;

                var commands = AiParseCommands(responseText);
                if (commands.Count == 0) break;

                var resultSummary = await AiExecuteCommandsFromResponse(commands);
                currentInput = resultSummary;
                AiAppendOutput("\n--- ASSISTANT ---\n");
            }
        }
        catch (Exception ex)
        {
            AiAppendOutput($"\n[ERROR] {ex.Message}\n");
            _aiStatusLabel.Text = "Error";
        }
        finally
        {
            AiSetUIEnabled(true);
            _aiInput.Focus();
        }
    }

    private async Task AiSendLlamaInstructMessageAsync()
    {
        var modelPath = _aiModelPathBox.Text.Trim();
        if (string.IsNullOrEmpty(modelPath))
        {
            MessageBox.Show(this, "Please enter the path to your GGUF model file.", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!File.Exists(modelPath))
        {
            MessageBox.Show(this, $"Model file not found:\n{modelPath}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var userText = _aiInput.Text.Trim();
        if (string.IsNullOrEmpty(userText)) return;

        AiSetUIEnabled(false);
        _aiInput.Clear();
        AiAppendOutput($"\n--- USER ---\n{userText}\n\n--- ASSISTANT ---\n");
        _aiConversationHistory.Add(AiMessage.CreateUserMessage(userText));

        try
        {
            _llamaInstructClient ??= new LlamaSharpInstructClient();
            _llamaInstructClient.Temperature = _aiSettings.Temperature;
            _llamaInstructClient.ReasoningEffort = _aiSettings.ReasoningEffort;
            _llamaInstructClient.MaxTokens = _aiSettings.MaxTokens;
            _llamaInstructClient.Threads = _aiSettings.Threads;
            _llamaInstructClient.ContextSize = _aiSettings.ContextSize;
            _llamaInstructClient.GpuLayers = _aiSettings.GpuLayers;
            if (_aiSettings.AntiPromptsInstruct.Count > 0)
                _llamaInstructClient.AntiPrompts = _aiSettings.AntiPromptsInstruct;

            _aiStatusLabel.Text = _llamaInstructClient.IsLoaded ? "Preparing..." : "Loading model...";
            await _llamaInstructClient.LoadModelAsync(modelPath, _aiSystemBox.Text);

            const int maxTurns = 20;
            var currentInput = userText;

            for (int turn = 0; turn < maxTurns; turn++)
            {
                _aiStatusLabel.Text = "Generating...";

                EventHandler<string> streamHandler = (_, text) => AiAppendOutput(text);
                EventHandler<string> thinkingHandler = (_, text) => AiAppendThinking(text);
                EventHandler<int> completeHandler = (_, count) =>
                {
                    if (InvokeRequired) Invoke(() => UpdateStatsLabel(_llamaInstructClient));
                    else UpdateStatsLabel(_llamaInstructClient);
                };

                _llamaInstructClient.StreamingTextReceived += streamHandler;
                _llamaInstructClient.ThinkingReceived += thinkingHandler;
                _llamaInstructClient.GenerationCompleted += completeHandler;

                string responseText;
                try
                {
                    responseText = await _llamaInstructClient.SendMessageAsync(currentInput);
                }
                finally
                {
                    _llamaInstructClient.StreamingTextReceived -= streamHandler;
                    _llamaInstructClient.ThinkingReceived -= thinkingHandler;
                    _llamaInstructClient.GenerationCompleted -= completeHandler;
                }

                _aiConversationHistory.Add(AiMessage.CreateAssistantMessage(responseText));
                AiAppendOutput("\n");

                if (!_chkAiAutoExec.Checked && !_aiSettings.AutoExec) break;

                var commands = AiParseCommands(responseText);
                if (commands.Count == 0) break;

                var resultSummary = await AiExecuteCommandsFromResponse(commands);
                currentInput = resultSummary;
                AiAppendOutput("\n--- ASSISTANT ---\n");
            }
        }
        catch (Exception ex)
        {
            AiAppendOutput($"\n[ERROR] {ex.Message}\n");
            _aiStatusLabel.Text = "Error";
        }
        finally
        {
            AiSetUIEnabled(true);
            _aiInput.Focus();
        }
    }

    private void UpdateStatsLabel(LlamaSharpClient client)
    {
        _aiStatusLabel.Text = $"Done — {client.LastTokensPerSecond:F1} t/s";
        _aiStatsLabel.Text = $"{client.LastTokensPerSecond:F1} t/s  TTFT {client.LastTimeToFirstToken:F2}s";
    }

    private void UpdateStatsLabel(LlamaSharpInstructClient client)
    {
        _aiStatusLabel.Text = $"Done — {client.LastTokensPerSecond:F1} t/s";
        _aiStatsLabel.Text = $"{client.LastTokensPerSecond:F1} t/s  TTFT {client.LastTimeToFirstToken:F2}s";
    }

    #endregion







    private void WireEvents()
    {
        // Process buttons
        _btnRefresh.Click += async (_, _) => await _viewModel.Init();
        _btnPick.Click += async (_, _) => await _viewModel.PickProcessAsync();
        _btnInspect.Click += (_, _) => LoadSelectedProcess();
        _grid.DoubleClick += (_, _) => LoadSelectedProcess();
        _filter.TextChanged += (_, _) => _viewModel.FilterProcess = _filter.Text;
        _windowedOnly.CheckedChanged += (_, _) => _viewModel.IsWindowedOnly = _windowedOnly.Checked;

        // Inspect buttons
        _btnRefreshTree.Click += (_, _) => _processViewModel?.Initialize();
        _btnCopyDetails.Click += (_, _) => _processViewModel?.CopyDetailsToClipboardCommand.Execute(null);
        _btnCopyJson.Click += (_, _) => _processViewModel?.CopyJsonToClipboardCommand.Execute(null);
        _btnCopyState.Click += (_, _) => _processViewModel?.CurrentElementSaveStateCommand.Execute(null);
        _btnCapture.Click += (_, _) => _processViewModel?.CaptureSelectedItemCommand.Execute(null);
        _chkHover.CheckedChanged += (_, _) => { if (_processViewModel != null) _processViewModel.EnableHoverMode = _chkHover.Checked; };
        _chkHighlight.CheckedChanged += (_, _) => { if (_processViewModel != null) _processViewModel.EnableHighLightSelectionMode = _chkHighlight.Checked; };
        _chkFocus.CheckedChanged += (_, _) => { if (_processViewModel != null) _processViewModel.EnableFocusTrackingMode = _chkFocus.Checked; };
        _chkXPath.CheckedChanged += (_, _) => { if (_processViewModel != null) _processViewModel.EnableXPath = _chkXPath.Checked; };

        // Tree
        _tree.AfterSelect += (_, e) =>
        {
            if (_processViewModel != null && e.Node.Tag is ElementViewModel vm)
            {
                _processViewModel.SelectedItem = vm;
                _elementIdBox.Text = vm.ElementId.ToString();
                _clickButton.Enabled = true;
            }
        };
        _tree.BeforeExpand += (_, e) => ExpandNode(e.Node);

        // Finder
        _btnFindWindow.Click += (_, _) => ExecuteFindWindow();
        _btnFindElement.Click += (_, _) => ExecuteFindElement();
        _btnFindAll.Click += (_, _) => ExecuteFindAll();

        // Click demo
        _elementIdBox.TextChanged += (_, _) =>
        {
            _clickButton.Enabled = int.TryParse(_elementIdBox.Text, out _);
        };
        _clickButton.Click += (_, _) => ExecuteClick();

        // ViewModel binding
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(StartupViewModel.FilteredProcesses) or nameof(StartupViewModel.SelectedProcess))
            {
                BeginInvoke(BindGrid);
            }
        };

        // Background sync
        _windowChangeTimer.Tick += async (_, _) =>
        {
            if (_isSyncingProcesses) return;
            _isSyncingProcesses = true;
            try
            {
                if (await _viewModel.SyncProcessesAsync()) BindGrid();
            }
            finally
            {
                _isSyncingProcesses = false;
            }
        };

        // Tools menu
        _menuTestWinForms.Click += async (_, _) => await RunTestWithUI("FlaUI WinForms Test App");
        _menuTestEcommerce.Click += async (_, _) => await RunEcommerceTestWithUI("Apex E-Commerce Test App");
        _menuTestWpf.Click += async (_, _) => await RunWpfTestWithUI("FlaUI WPF Test App");
        _menuToolStripMenuItem.Click += async (_, _) => await RunMenuTestWithUI("FlaUI Menu Test App");
        _menuTestCustomWindow.Click += async (_, _) => await PromptAndRunTest();
        _menuListWindows.Click += async (_, _) => await ListWindowsToDebugLog();
        _menuScanWindow.Click += async (_, _) => await PromptAndScanWindow();
        _menuAiChat.Click += (_, _) => ToggleAiChatPanel();
        _menuAiSettings.Click += (_, _) => OpenAiSettingsDialog();

        // AI Chat events
        _btnAiSend.Click += async (_, _) => await AiSendMessageAsync(stream: false);
        _btnAiStream.Click += async (_, _) => await AiSendMessageAsync(stream: true);
        _btnAiClear.Click += (_, _) => AiClearChat();
        _btnAiStop.Click += (_, _) =>
        {
            _llamaClient?.Stop();
            _llamaInstructClient?.Stop();
            _aiStatusLabel.Text = "Stopped";
        };
        _aiInput.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = AiSendMessageAsync(stream: e.Shift);
            }
        };

        // System stats timer
        _sysStatsTimer.Tick += (_, _) => UpdateSysStats();
        _sysStatsTimer.Start();

        FormClosing += (_, _) =>
        {
            // Save before dispose — if Dispose throws, settings must already be persisted
            SyncUIToSettings();
            SaveSettings();
            _windowChangeTimer.Stop();
            _sysStatsTimer.Stop();
            _processViewModel?.ClosingCommand.Execute(null);
            _llamaClient?.Dispose();
            _llamaInstructClient?.Dispose();
        };

        Load += async (_, _) =>
        {
            _windowedOnly.Checked = true;
            await _viewModel.Init();
            BindGrid();
            _windowChangeTimer.Start();
        };
    }

    private void UpdateSysStats()
    {
        try
        {
            var proc = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var cpu = proc.TotalProcessorTime;
            var elapsed = (now - _lastCpuSample).TotalSeconds;

            float cpuPct = 0f;
            if (elapsed > 0.1)
            {
                cpuPct = (float)((cpu - _lastCpuTime).TotalSeconds / elapsed / Environment.ProcessorCount * 100.0);
                _lastCpuTime = cpu;
                _lastCpuSample = now;
            }

            long ramMb = proc.WorkingSet64 / (1024 * 1024);
            _aiSysStatsLabel.Text = $"CPU {cpuPct:F0}%  RAM {ramMb} MB";
        }
        catch { /* non-critical */ }
    }

    private void ExecuteClick()
    {
        if (!int.TryParse(_elementIdBox.Text, out int elementId))
        {
            LogDebug("Invalid element ID");
            return;
        }

        var element = ElementRegistry.FindAutomationElementById(elementId);
        if (element == null)
        {
            LogDebug($"Element {elementId} not found in registry");
            return;
        }

        LogDebug($"Executing Click on element {elementId}...");
        var result = _elementOps.Click(element, elementId);
        LogDebug(result.IsSuccess
            ? $"Click succeeded on element {elementId}"
            : $"Click failed on element {elementId}: {result.ErrorMessage}");
    }

    private ElementSearchFilter BuildFinderFilter(ControlType? defaultControlType = null)
    {
        var filter = new ElementSearchFilter();

        // Parse exclude names (comma-separated)
        var excludeText = _finderExcludeNames.Text.Trim();
        if (!string.IsNullOrEmpty(excludeText))
        {
            filter.ExcludeNames = excludeText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        // Parse include ControlTypes (comma-separated, e.g. "Button,Edit,Menu")
        var typesText = _finderIncludeTypes.Text.Trim();
        if (!string.IsNullOrEmpty(typesText))
        {
            var parsed = typesText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => Enum.TryParse<ControlType>(t, ignoreCase: true, out var ct) ? ct : (ControlType?)null)
                .Where(ct => ct.HasValue)
                .Select(ct => ct!.Value)
                .ToHashSet();

            if (parsed.Count > 0)
                filter.IncludeControlTypes = parsed;
        }

        return filter;
    }

    private void ExecuteFindWindow()
    {
        var searchText = _finderSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(searchText))
        {
            LogDebug("Find Window: enter a search term");
            return;
        }

        try
        {
            var finder = new ElementFinderService(_finderAutomation);
            var filter = BuildFinderFilter();

            LogDebug($"Finding window '{searchText}'...");
            var result = finder.FindWindow(searchText, filter);

            if (result == null)
            {
                LogDebug("Find Window: no match found");
                return;
            }

            LogDebug($"Found window: {result}");
        }
        catch (Exception ex)
        {
            LogDebug($"Find Window error: {ex.Message}");
        }
    }

    private void ExecuteFindElement()
    {
        var searchText = _finderSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(searchText))
        {
            LogDebug("Find Element: enter a search term");
            return;
        }

        if (_processViewModel == null)
        {
            LogDebug("Find Element: inspect a process first");
            return;
        }

        try
        {
            // Use the inspected process's window title to locate the window,
            // then search for the element within it
            var finder = new ElementFinderService(_finderAutomation);
            var filter = BuildFinderFilter();
            var windowTitle = _processViewModel.WindowTitle?.Replace("Process: ", "") ?? "";

            // Get the window by its handle directly from the automation
            var windowElement = _finderAutomation.FromHandle(
                _viewModel.SelectedProcess?.MainWindowHandle ?? IntPtr.Zero);

            if (windowElement == null)
            {
                LogDebug("Find Element: could not locate the inspected window");
                return;
            }

            LogDebug($"Finding element '{searchText}' in window...");
            var result = finder.FindElement(windowElement, searchText, filter);

            if (result == null)
            {
                LogDebug("Find Element: no match found");
                return;
            }

            LogDebug($"Found element: {result}");
        }
        catch (Exception ex)
        {
            LogDebug($"Find Element error: {ex.Message}");
        }
    }

    private void ExecuteFindAll()
    {
        if (_processViewModel == null)
        {
            LogDebug("Find All: inspect a process first");
            return;
        }

        try
        {
            var finder = new ElementFinderService(_finderAutomation);
            var filter = BuildFinderFilter();

            var windowElement = _finderAutomation.FromHandle(
                _viewModel.SelectedProcess?.MainWindowHandle ?? IntPtr.Zero);

            if (windowElement == null)
            {
                LogDebug("Find All: could not locate the inspected window");
                return;
            }

            LogDebug($"Finding all elements in window (Types={_finderIncludeTypes.Text}, Exclude={_finderExcludeNames.Text})...");
            var results = finder.FindAllElements(windowElement, filter);

            LogDebug($"Found {results.Count} element(s):");
            foreach (var result in results)
            {
                LogDebug($"  {result}");
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Find All error: {ex.Message}");
        }
    }

    private void BindGrid()
    {
        Dictionary<IntPtr, ProcessWindowInfo> filtered = _viewModel.FilteredProcesses.ToDictionary(x => x.MainWindowHandle);

        for (int i = _gridItems.Count - 1; i >= 0; i--)
        {
            if (!filtered.ContainsKey(_gridItems[i].MainWindowHandle))
            {
                _gridItems.RemoveAt(i);
            }
        }

        foreach (ProcessWindowInfo process in filtered.Values)
        {
            int index = _gridItems.ToList().FindIndex(x => x.MainWindowHandle == process.MainWindowHandle);

            if (index < 0)
            {
                _gridItems.Add(process);
                continue;
            }

            if (!_gridItems[index].Equals(process))
            {
                _gridItems[index] = process;
            }
        }

        if (_viewModel.SelectedProcess != null)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.DataBoundItem is ProcessWindowInfo info && info.MainWindowHandle == _viewModel.SelectedProcess.MainWindowHandle)
                {
                    row.Selected = true;
                    break;
                }
            }
        }
    }

    private void LoadSelectedProcess()
    {
        if (_grid.CurrentRow?.DataBoundItem is not ProcessWindowInfo process) return;

        if (_processViewModel != null)
        {
            _processViewModel.PropertyChanged -= OnProcessViewModelPropertyChanged;
            _processViewModel.ClosingCommand.Execute(null);
        }
        _processViewModel = new ProcessViewModel(new UIA3Automation(), process.ProcessId, process.MainWindowHandle, _logger);
        _processViewModel.PropertyChanged += OnProcessViewModelPropertyChanged;
        _processViewModel.Initialize();
        Text = $"ApexUIBridge - {process.ProcessId} {process.WindowTitle}";
        PopulateTree();
        PopulateDetails();
    }

    private void OnProcessViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProcessViewModel.Elements)) BeginInvoke(PopulateTree);
        if (e.PropertyName == nameof(ProcessViewModel.ElementPatterns)) BeginInvoke(PopulateDetails);
    }

    private void PopulateTree()
    {
        _tree.Nodes.Clear();
        if (_processViewModel == null) return;

        foreach (ElementViewModel item in _processViewModel.Elements)
        {
            TreeNode node = new(item.ToString()) { Tag = item };
            node.Nodes.Add(new TreeNode("..."));
            _tree.Nodes.Add(node);
        }
    }

    private void ExpandNode(TreeNode node)
    {
        if (_processViewModel == null || node.Tag is not ElementViewModel vm || (node.Nodes.Count == 1 && node.Nodes[0].Text != "...")) return;

        node.Nodes.Clear();
        foreach (ElementViewModel child in vm.LoadChildren())
        {
            TreeNode childNode = new(child.ToString()) { Tag = child };
            childNode.Nodes.Add(new TreeNode("..."));
            node.Nodes.Add(childNode);
        }
    }

    private void LogDebug(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _debugLog.Items.Add(line);
        _debugLog.TopIndex = _debugLog.Items.Count - 1;
        _logger.LogDebug(message);
    }

    private void PopulateDetails()
    {
        _details.Items.Clear();
        if (_processViewModel == null) return;

        foreach (ElementPatternItem item in _processViewModel.ElementPatterns)
        {
            if (!item.IsVisible) continue;

            ListViewItem section = new(item.PatternName);
            section.SubItems.Add(string.Empty);
            _details.Items.Add(section);

            if (item.Children == null) continue;

            foreach (PatternItem child in item.Children)
            {
                ListViewItem row = new($"  {child.Key}");
                row.SubItems.Add(child.Value ?? string.Empty);
                _details.Items.Add(row);
            }
        }
    }




    private async Task RunTestWithUI(string windowTitle)
    {
        LogDebug($"Starting test for '{windowTitle}'...");
        _menuTestWinForms.Enabled = false;
        _menuTestEcommerce.Enabled = false;
        _menuTestCustomWindow.Enabled = false;

        try
        {
            var result = await TestWinFormsAppByTxtCmd(windowTitle);

            // Show results in a scrollable dialog
            using var dlg = new Form
            {
                Text = $"Test Results - {windowTitle}",
                Size = new Size(800, 600),
                StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false
            };
            var txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                WordWrap = false,
                Text = result
            };
            dlg.Controls.Add(txt);
            dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            LogDebug($"Test error: {ex.Message}");
            MessageBox.Show(this, $"Test failed:\n{ex.Message}", "Test Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _menuTestWinForms.Enabled = true;
            _menuTestEcommerce.Enabled = true;
            _menuTestCustomWindow.Enabled = true;
        }
    }

    private async Task RunEcommerceTestWithUI(string windowTitle)
    {
        LogDebug($"Starting e-commerce test for '{windowTitle}'...");
        _menuTestWinForms.Enabled = false;
        _menuTestEcommerce.Enabled = false;
        _menuTestCustomWindow.Enabled = false;

        try
        {
            var result = await TestEcommerceWebAppByTxtCmd(windowTitle);

            using var dlg = new Form
            {
                Text = $"Test Results - {windowTitle}",
                Size = new Size(800, 600),
                StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false
            };
            var txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                WordWrap = false,
                Text = result
            };
            dlg.Controls.Add(txt);
            dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            LogDebug($"E-commerce test error: {ex.Message}");
            MessageBox.Show(this, $"Test failed:\n{ex.Message}", "Test Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _menuTestWinForms.Enabled = true;
            _menuTestEcommerce.Enabled = true;
            _menuTestCustomWindow.Enabled = true;
        }
    }
   private async Task RunMenuTestWithUI(string windowTitle = "FlaUI Menu Test App")
    {

        _menuToolStripMenuItem.Enabled = false;
        try
        {
            var result = await TestMenuByTxtCmd(windowTitle);

            using var dlg = new Form
            {
                Text = "Test Results - FlaUI Menu Test App",
                Size = new Size(800, 600),
                StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false
            };
            var txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                WordWrap = false,
                Text = result
            };
            dlg.Controls.Add(txt);
            dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Test failed:\n{ex.Message}", "Test Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _menuToolStripMenuItem.Enabled = true;
        }

    }




    private async Task RunWpfTestWithUI(string windowTitle)
    {
        LogDebug($"Starting WPF test for '{windowTitle}'...");
        _menuTestWinForms.Enabled = false;
        _menuTestEcommerce.Enabled = false;
        _menuTestWpf.Enabled = false;
        _menuTestCustomWindow.Enabled = false;

        try
        {
            var result = await TestWpfAppByTxtCmd(windowTitle);

            using var dlg = new Form
            {
                Text = $"Test Results - {windowTitle}",
                Size = new Size(800, 600),
                StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false
            };
            var txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                WordWrap = false,
                Text = result
            };
            dlg.Controls.Add(txt);
            dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            LogDebug($"WPF test error: {ex.Message}");
            MessageBox.Show(this, $"Test failed:\n{ex.Message}", "Test Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _menuTestWinForms.Enabled = true;
            _menuTestEcommerce.Enabled = true;
            _menuTestWpf.Enabled = true;
            _menuTestCustomWindow.Enabled = true;
        }
    }

    private async Task PromptAndRunTest()
    {
        var title = PromptForInput("Test Custom Window", "Enter the window title to test:");
        if (!string.IsNullOrWhiteSpace(title))
            await RunTestWithUI(title);
    }

    private async Task ListWindowsToDebugLog()
    {
        LogDebug("Listing all windows...");

        var windows = await Task.Run(() => _bridge.GetListOfWindows());
        if (windows.Count == 0)
        {
            LogDebug("  No windows found");
            return;
        }

        foreach (var w in windows)
            LogDebug($"  {w}");

        LogDebug($"Total: {windows.Count} window(s)");
    }

    private async Task PromptAndScanWindow()
    {
        var title = PromptForInput("Scan Window", "Enter the window title to scan:");
        if (string.IsNullOrWhiteSpace(title)) return;

        LogDebug($"Scanning '{title}'...");

        var result = await _bridge.ScanWindowByName(title);
        LogDebug($"  Scan: {result.message} ({result.elementCount} elements)");

        if (result.success)
        {
            var allElements = _bridge.GetAllElements();
            var types = allElements
                .GroupBy(e => e.ControlType)
                .OrderByDescending(g => g.Count());

            LogDebug("  Element breakdown:");
            foreach (var g in types)
                LogDebug($"    {g.Key}: {g.Count()}");
        }
    }

    private string? PromptForInput(string title, string label)
    {
        using var dlg = new Form
        {
            Text = title,
            Size = new Size(400, 150),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };
        var lbl = new Label { Text = label, Location = new Point(12, 15), AutoSize = true };
        var txt = new TextBox { Location = new Point(12, 40), Size = new Size(360, 23) };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(216, 75), Size = new Size(75, 23) };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(297, 75), Size = new Size(75, 23) };
        dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
    }

    public async Task<string> TestWinFormsAppByTxtCmd(string appWindowTitle = "FlaUI WinForms Test App")
    {

        var log = new StringBuilder();
        int stepNum = 0;
        int passed = 0, failed = 0, skipped = 0;

        log.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        log.AppendLine("║   WINFORMS TEST APP - ALL INTERACTIONS BY TEXT COMMAND         ║");
        log.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        log.AppendLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        log.AppendLine();

        log.AppendLine("═══ APP LAUNCH ═══");
        await EnsureTestAppIsRunning(appWindowTitle, "WinFormsApplication.exe", log);
        log.AppendLine();

        // Scan the target window first so Bridge has elements to work with
        log.AppendLine("═══ WINDOW SCAN ═══");
        var scanResult = await _bridge.ScanWindowByName(appWindowTitle);
        log.AppendLine($"  Scan: {scanResult.message} ({scanResult.elementCount} elements)");

        if (!scanResult.success)
        {
            log.AppendLine("  FAILED: Could not scan window - is the app running?");
            return log.ToString();
        }

        // Discover elements
        log.AppendLine();
        log.AppendLine("═══ ELEMENT DISCOVERY ═══");
        var elements = DiscoverWinFormsTestAppElements(appWindowTitle);

        if (elements == null)
        {
            log.AppendLine("  FAILED: Could not find WinForms Test App window in scanned elements");
            return log.ToString();
        }

        log.AppendLine($"  Window ID: {elements.WindowId}");
        log.AppendLine();

        // Log discovery results
        log.AppendLine("═══ DISCOVERED ELEMENTS ═══");
        void LogElement(string name, int id) =>
            log.AppendLine(id > 0 ? $"  {name,-25} ID={id}" : $"  {name,-25} NOT FOUND");

        LogElement("Button1", elements.Button1Id);
        LogElement("Button2", elements.Button2Id);
        LogElement("SimpleCheckBox", elements.SimpleCheckBoxId);
        LogElement("ThreeStateCheckBox", elements.ThreeStateCheckBoxId);
        LogElement("RadioButton1", elements.RadioButton1Id);
        LogElement("RadioButton2", elements.RadioButton2Id);
        LogElement("EditableCombo", elements.EditableComboId);
        LogElement("NonEditableCombo", elements.NonEditableComboId);
        LogElement("TextBox", elements.TextBoxId);
        LogElement("PasswordBox", elements.PasswordBoxId);
        LogElement("Slider", elements.SliderId);
        LogElement("Spinner", elements.SpinnerId);
        LogElement("ProgressBar", elements.ProgressBarId);
        LogElement("ListBox", elements.ListBoxId);
        LogElement("ListView", elements.ListViewId);
        LogElement("TreeView", elements.TreeViewId);
        LogElement("DataGrid", elements.DataGridId);
        LogElement("TabControl", elements.TabControlId);
        LogElement("TabPage1", elements.TabPage1Id);
        LogElement("TabPage2", elements.TabPage2Id);
        LogElement("MenuBar", elements.MenuBarId);
        LogElement("FileMenu", elements.FileMenuId);
        LogElement("EditMenu", elements.EditMenuId);
        LogElement("StatusBar", elements.StatusBarId);
        LogElement("DateTimePicker", elements.DateTimePickerId);
        log.AppendLine();

        // Dump all scanned elements for diagnostics
        log.AppendLine("═══ ALL SCANNED ELEMENTS ═══");
        var allScanned = _bridge.GetAllElements();
        foreach (var el in allScanned.OrderBy(e => e.Id))
        {
            log.AppendLine($"  [{el.Id,3}] {el.ControlType,-20} AutoId='{el.AutomationId}' Name='{el.Name}' Parent={el.ParentId?.ToString() ?? "root"}");
        }
        log.AppendLine();

        async Task RunStep(string description, string command, int elementId)
        {
            stepNum++;
            if (elementId == 0)
            {
                log.AppendLine($"Step {stepNum}: {description}");
                log.AppendLine($"  SKIPPED (element not found)");
                log.AppendLine();
                skipped++;
                return;
            }

            log.AppendLine($"Step {stepNum}: {description}");
            var result = await _bridge.ExecuteCommand(command);
            log.AppendLine($"  Command: {command}");
            log.AppendLine($"  Success: {result.IsSuccess}");
            log.AppendLine($"  Result: {result.Message}");
            log.AppendLine();

            if (result.IsSuccess) passed++; else failed++;
            await Task.Delay(300);
        }

        // ============================================
        // CLICK TESTS
        // ============================================
        log.AppendLine("═══ CLICK TESTS ═══");

        await RunStep("Click Button1", $"CLICK {elements.Button1Id}", elements.Button1Id);
        await RunStep("Double-Click Button2", $"DOUBLE_CLICK {elements.Button2Id}", elements.Button2Id);
        await RunStep("Right-Click Button1", $"RIGHT_CLICK {elements.Button1Id}", elements.Button1Id);

        // Close any context menu
        if (elements.Button1Id > 0)
            await _bridge.ExecuteCommand($"SEND_KEYS {elements.Button1Id} {{ESC}}");

        // ============================================
        // TEXT INPUT TESTS
        // ============================================
        log.AppendLine("═══ TEXT INPUT TESTS ═══");

        await RunStep("Type in TextBox", $"TYPE {elements.TextBoxId} Hello from TxtCmd!", elements.TextBoxId);
        await RunStep("Set Value in PasswordBox", $"SET_VALUE {elements.PasswordBoxId} Secret123", elements.PasswordBoxId);

        // Clear text fields
        if (elements.TextBoxId > 0) await _bridge.ExecuteCommand($"SET_VALUE {elements.TextBoxId} ");
        if (elements.PasswordBoxId > 0) await _bridge.ExecuteCommand($"SET_VALUE {elements.PasswordBoxId} ");

        // ============================================
        // GET TEXT TESTS
        // ============================================
        log.AppendLine("═══ GET TEXT TESTS ═══");

        await RunStep("Get Text from TextBox", $"GET_TEXT {elements.TextBoxId}", elements.TextBoxId);

        // ============================================
        // TOGGLE TESTS
        // ============================================
        log.AppendLine("═══ TOGGLE TESTS ═══");

        await RunStep("Toggle SimpleCheckBox", $"TOGGLE {elements.SimpleCheckBoxId}", elements.SimpleCheckBoxId);
        await RunStep("Toggle ThreeStateCheckBox", $"TOGGLE {elements.ThreeStateCheckBoxId}", elements.ThreeStateCheckBoxId);

        // Toggle back
        if (elements.SimpleCheckBoxId > 0) await _bridge.ExecuteCommand($"TOGGLE {elements.SimpleCheckBoxId}");

        // ============================================
        // RADIOBUTTON TESTS
        // ============================================
        log.AppendLine("═══ RADIOBUTTON TESTS ═══");

        await RunStep("Select RadioButton1", $"CLICK {elements.RadioButton1Id}", elements.RadioButton1Id);
        await RunStep("Select RadioButton2", $"CLICK {elements.RadioButton2Id}", elements.RadioButton2Id);

        // ============================================
        // COMBOBOX TESTS
        // ============================================
        log.AppendLine("═══ COMBOBOX TESTS ═══");

        await RunStep("Expand EditableCombo", $"EXPAND {elements.EditableComboId}", elements.EditableComboId);
        await RunStep("Collapse EditableCombo", $"COLLAPSE {elements.EditableComboId}", elements.EditableComboId);
        await RunStep("Select by Text in EditableCombo", $"SELECT_BY_TEXT {elements.EditableComboId} Item 2", elements.EditableComboId);
        await RunStep("Select by Index in NonEditableCombo", $"SELECT_BY_INDEX {elements.NonEditableComboId} 0", elements.NonEditableComboId);

        // ============================================
        // SLIDER/RANGE TESTS
        // ============================================
        log.AppendLine("═══ SLIDER/RANGE TESTS ═══");

        await RunStep("Set Slider to 80", $"SET_VALUE {elements.SliderId} 80", elements.SliderId);
        await RunStep("Set Slider to 20", $"SET_VALUE {elements.SliderId} 20", elements.SliderId);
        await RunStep("Set Spinner to 50", $"SET_VALUE {elements.SpinnerId} 50", elements.SpinnerId);

        // ============================================
        // LISTBOX TESTS
        // ============================================
        log.AppendLine("═══ LISTBOX TESTS ═══");

        await RunStep("Focus ListBox", $"FOCUS {elements.ListBoxId}", elements.ListBoxId);
        await RunStep("Scroll ListBox Down", $"SCROLL {elements.ListBoxId} down 2", elements.ListBoxId);

        // ============================================
        // TAB TESTS
        // ============================================
        log.AppendLine("═══ TAB TESTS ═══");

        // Try to switch to Complex Controls tab
        bool tabSwitched = false;
        if (elements.TabPage2Id > 0)
        {
            await RunStep("Select Tab 'Complex Controls'", $"CLICK {elements.TabPage2Id}", elements.TabPage2Id);
            tabSwitched = true;
        }
        else if (elements.TabControlId > 0)
        {
            // TabPage2 not found directly - use SELECT_BY_INDEX on the TabControl
            await RunStep("Select Tab 'Complex Controls' via TabControl index", $"SELECT_BY_INDEX {elements.TabControlId} 1", elements.TabControlId);
            tabSwitched = true;
        }
        await Task.Delay(500);

        // Rescan the window to discover new elements after tab switch
        // All IDs change after rescan, so re-discover everything
        if (tabSwitched)
        {
            log.AppendLine("═══ POST-TAB-SWITCH RESCAN ═══");
            var rescanResult = await _bridge.ScanWindowByName(appWindowTitle);
            log.AppendLine($"  Rescan: {rescanResult.message} ({rescanResult.elementCount} elements)");

            // Re-run full discovery since all IDs are invalidated
            var refreshedElements = DiscoverWinFormsTestAppElements(appWindowTitle);
            if (refreshedElements != null)
            {
                // Copy all refreshed IDs
                elements.WindowId = refreshedElements.WindowId;
                elements.Button1Id = refreshedElements.Button1Id;
                elements.Button2Id = refreshedElements.Button2Id;
                elements.SimpleCheckBoxId = refreshedElements.SimpleCheckBoxId;
                elements.ThreeStateCheckBoxId = refreshedElements.ThreeStateCheckBoxId;
                elements.RadioButton1Id = refreshedElements.RadioButton1Id;
                elements.RadioButton2Id = refreshedElements.RadioButton2Id;
                elements.EditableComboId = refreshedElements.EditableComboId;
                elements.NonEditableComboId = refreshedElements.NonEditableComboId;
                elements.TextBoxId = refreshedElements.TextBoxId;
                elements.PasswordBoxId = refreshedElements.PasswordBoxId;
                elements.SliderId = refreshedElements.SliderId;
                elements.SpinnerId = refreshedElements.SpinnerId;
                elements.ProgressBarId = refreshedElements.ProgressBarId;
                elements.ListBoxId = refreshedElements.ListBoxId;
                elements.ListViewId = refreshedElements.ListViewId;
                elements.TreeViewId = refreshedElements.TreeViewId;
                elements.DataGridId = refreshedElements.DataGridId;
                elements.TabControlId = refreshedElements.TabControlId;
                elements.TabPage1Id = refreshedElements.TabPage1Id;
                elements.TabPage2Id = refreshedElements.TabPage2Id;
                elements.MenuBarId = refreshedElements.MenuBarId;
                elements.FileMenuId = refreshedElements.FileMenuId;
                elements.EditMenuId = refreshedElements.EditMenuId;
                elements.StatusBarId = refreshedElements.StatusBarId;
                elements.DateTimePickerId = refreshedElements.DateTimePickerId;

                log.AppendLine($"  Re-discovered: TreeView={elements.TreeViewId}, ListView={elements.ListViewId}, DataGrid={elements.DataGridId}");
                log.AppendLine($"  Re-discovered: TabPage1={elements.TabPage1Id}, TabPage2={elements.TabPage2Id}");
                log.AppendLine($"  Re-discovered: FileMenu={elements.FileMenuId}, EditMenu={elements.EditMenuId}");
            }
            else
            {
                log.AppendLine("  WARNING: Re-discovery failed after rescan");
            }
            log.AppendLine();
        }

        // Find an expandable TreeItem (one that has children, not a leaf)
        int treeItemId = 0;

        if (elements.TreeViewId > 0)
        {
            var allElems = _bridge.GetAllElements();

            var treeItems = allElems.Where(e =>
                e.ControlType == FlaUI.Core.Definitions.ControlType.TreeItem &&
                e.ParentId == elements.TreeViewId).ToList();

            treeItemId = treeItems.FirstOrDefault(ti =>
                allElems.Any(e => e.ParentId == ti.Id))?.Id ?? treeItems.FirstOrDefault()?.Id ?? 0;
        }

        // ============================================
        // COMPLEX CONTROLS TESTS (while Complex tab is active)
        // ============================================
        log.AppendLine("═══ COMPLEX CONTROLS TESTS ═══");

        await RunStep("Expand TreeView Node", $"EXPAND {treeItemId}", treeItemId);
        await RunStep("Collapse TreeView Node", $"COLLAPSE {treeItemId}", treeItemId);
        await RunStep("Focus ListView", $"FOCUS {elements.ListViewId}", elements.ListViewId);
        await RunStep("Scroll ListView", $"SCROLL {elements.ListViewId} down 1", elements.ListViewId);
        await RunStep("Focus DataGridView", $"FOCUS {elements.DataGridId}", elements.DataGridId);

        await RunStep("Select Tab 'Simple Controls'", $"CLICK {elements.TabPage1Id}", elements.TabPage1Id);
        await Task.Delay(300);

        // ============================================
        // MENU TESTS
        // ============================================
        log.AppendLine("═══ MENU TESTS ═══");

        await RunStep("Click File Menu", $"CLICK {elements.FileMenuId}", elements.FileMenuId);
        if (elements.FileMenuId > 0)
            await _bridge.ExecuteCommand($"SEND_KEYS {elements.FileMenuId} {{ESC}}");

        // ============================================
        // FOCUS/HOVER/HIGHLIGHT TESTS
        // ============================================
        log.AppendLine("═══ FOCUS/HOVER/HIGHLIGHT TESTS ═══");

        await RunStep("Focus Button1", $"FOCUS {elements.Button1Id}", elements.Button1Id);
        await RunStep("Hover Button2", $"HOVER {elements.Button2Id}", elements.Button2Id);
        await RunStep("Highlight StatusBar", $"HIGHLIGHT {elements.StatusBarId}", elements.StatusBarId);

        // ============================================
        // KEYBOARD TESTS
        // ============================================
        log.AppendLine("═══ KEYBOARD TESTS ═══");

        if (elements.TextBoxId > 0)
        {
            await _bridge.ExecuteCommand($"TYPE {elements.TextBoxId} Test");
            await Task.Delay(100);
        }
        await RunStep("Send Keys (Ctrl+A)", $"SEND_KEYS {elements.TextBoxId} ^a", elements.TextBoxId);

        // Clear
        if (elements.TextBoxId > 0) await _bridge.ExecuteCommand($"SET_VALUE {elements.TextBoxId} ");

        // ============================================
        // WINDOW TESTS
        // ============================================
        log.AppendLine("═══ WINDOW TESTS ═══");

        await RunStep("Minimize Window", $"WINDOW_ACTION {elements.WindowId} minimize", elements.WindowId);
        await Task.Delay(500);
        await RunStep("Restore Window", $"WINDOW_ACTION {elements.WindowId} restore", elements.WindowId);

        // ============================================
        // SUMMARY
        // ============================================
        log.AppendLine("═══ TEST SUMMARY ═══");
        log.AppendLine($"  Total Steps: {stepNum}");
        log.AppendLine($"  Passed:  {passed}");
        log.AppendLine($"  Failed:  {failed}");
        log.AppendLine($"  Skipped: {skipped}");
        log.AppendLine($"  Success Rate: {(passed + failed > 0 ? (passed * 100.0 / (passed + failed)) : 0):F1}%");
        log.AppendLine();
        log.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        log.AppendLine("║                    TEST COMPLETE                               ║");
        log.AppendLine("╚════════════════════════════════════════════════════════════════╝");

        var finalResult = log.ToString();
        System.Diagnostics.Debug.WriteLine(finalResult);
        return finalResult;
    }

    /// <summary>
    /// Discovers WinForms Test App elements by AutomationId and Name.
    /// </summary>
    private WinFormsTestAppElements? DiscoverWinFormsTestAppElements(string appWindowTitle = "FlaUI WinForms Test App")
    {
        if (_bridge == null) return null;

        var allElements = _bridge.GetAllElements();

        // Find the window
        var appWindow = allElements.FirstOrDefault(e =>
            e.ControlType == FlaUI.Core.Definitions.ControlType.Window &&
            !string.IsNullOrEmpty(e.Name) &&
            e.Name.Contains(appWindowTitle, StringComparison.OrdinalIgnoreCase));

        if (appWindow == null) return null;

        var windowElements = ResolveTestAppElementsScope(appWindow, allElements, appWindowTitle, out _);
        var elements = new WinFormsTestAppElements { WindowId = appWindow.Id };

        // Helper to find by AutomationId
        int FindByAutomationId(string automationId) =>
            windowElements.FirstOrDefault(e => e.AutomationId == automationId)?.Id ?? 0;

        // Helper to find by Name and ControlType
        int FindByNameAndType(string name, FlaUI.Core.Definitions.ControlType controlType) =>
            windowElements.FirstOrDefault(e =>
                e.ControlType == controlType &&
                !string.IsNullOrEmpty(e.Name) &&
                e.Name.Contains(name, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

        // Buttons
        elements.Button1Id = FindByAutomationId("button1");
        elements.Button2Id = FindByAutomationId("button2");

        // CheckBoxes
        elements.SimpleCheckBoxId = FindByAutomationId("SimpleCheckBox");
        elements.ThreeStateCheckBoxId = FindByAutomationId("ThreeStateCheckBox");

        // RadioButtons
        elements.RadioButton1Id = FindByAutomationId("RadioButton1");
        elements.RadioButton2Id = FindByAutomationId("RadioButton2");

        // ComboBoxes
        elements.EditableComboId = FindByAutomationId("EditableCombo");
        elements.NonEditableComboId = FindByAutomationId("NonEditableCombo");

        // TextBoxes
        elements.TextBoxId = FindByAutomationId("TextBox");
        elements.PasswordBoxId = FindByAutomationId("PasswordBox");

        // Numeric/Range
        elements.SliderId = FindByAutomationId("Slider");
        elements.SpinnerId = FindByAutomationId("numericUpDown1");
        elements.ProgressBarId = FindByAutomationId("ProgressBar");

        // Lists
        elements.ListBoxId = FindByAutomationId("ListBox");
        elements.ListViewId = FindByAutomationId("listView1");
        elements.TreeViewId = FindByAutomationId("treeView1");
        elements.DataGridId = FindByAutomationId("dataGridView");

        // Tabs
        elements.TabControlId = FindByAutomationId("tabControl1");
        elements.TabPage1Id = FindByAutomationId("tabPage1");
        elements.TabPage2Id = FindByAutomationId("tabPage2");

        // Fallback: if tabPage2 not found by AutomationId (hidden tab pane),
        // find by name as a TabItem or Pane, searching full registry if scoped set misses it
        if (elements.TabPage2Id == 0)
        {
            elements.TabPage2Id = FindByNameAndType("Complex Controls", FlaUI.Core.Definitions.ControlType.TabItem);
            if (elements.TabPage2Id == 0)
                elements.TabPage2Id = FindByNameAndType("Complex Controls", FlaUI.Core.Definitions.ControlType.Pane);
            if (elements.TabPage2Id == 0)
                elements.TabPage2Id = allElements.FirstOrDefault(e =>
                    (e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem || e.ControlType == FlaUI.Core.Definitions.ControlType.Pane) &&
                    !string.IsNullOrEmpty(e.Name) &&
                    e.Name.Contains("Complex Controls", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

            // Fallback: find second TabItem child of TabControl (WinForms may not expose non-selected tab AutomationIds)
            if (elements.TabPage2Id == 0 && elements.TabControlId > 0)
            {
                var tabChildren = allElements
                    .Where(e => e.ParentId == elements.TabControlId &&
                                e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem)
                    .ToList();
                if (tabChildren.Count >= 2)
                    elements.TabPage2Id = tabChildren[1].Id;
                else if (tabChildren.Count == 0)
                {
                    // Try ANY children of TabControl that aren't TabPage1
                    var otherTabChildren = allElements
                        .Where(e => e.ParentId == elements.TabControlId && e.Id != elements.TabPage1Id)
                        .ToList();
                    if (otherTabChildren.Count > 0)
                        elements.TabPage2Id = otherTabChildren[0].Id;
                }
            }
        }

        // Menus
        elements.MenuBarId = FindByAutomationId("menuStrip1");
        if (elements.MenuBarId == 0)
            elements.MenuBarId = FindByNameAndType("menuStrip1", FlaUI.Core.Definitions.ControlType.MenuBar);

        elements.FileMenuId = FindByNameAndType("File", FlaUI.Core.Definitions.ControlType.MenuItem);
        elements.EditMenuId = FindByNameAndType("Edit", FlaUI.Core.Definitions.ControlType.MenuItem);

        // Fallback: search MenuBar children directly (WinForms menu items may not be in scoped descendants)
        if ((elements.FileMenuId == 0 || elements.EditMenuId == 0) && elements.MenuBarId > 0)
        {
            var menuChildren = allElements.Where(e => e.ParentId == elements.MenuBarId).ToList();
            if (elements.FileMenuId == 0)
                elements.FileMenuId = menuChildren.FirstOrDefault(e =>
                    !string.IsNullOrEmpty(e.Name) && e.Name.Contains("File", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
            if (elements.EditMenuId == 0)
                elements.EditMenuId = menuChildren.FirstOrDefault(e =>
                    !string.IsNullOrEmpty(e.Name) && e.Name.Contains("Edit", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
        }

        // Last resort: search ALL elements for menu items by name
        if (elements.FileMenuId == 0)
            elements.FileMenuId = allElements.FirstOrDefault(e =>
                e.ControlType == FlaUI.Core.Definitions.ControlType.MenuItem &&
                !string.IsNullOrEmpty(e.Name) && e.Name.Equals("File", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
        if (elements.EditMenuId == 0)
            elements.EditMenuId = allElements.FirstOrDefault(e =>
                e.ControlType == FlaUI.Core.Definitions.ControlType.MenuItem &&
                !string.IsNullOrEmpty(e.Name) && e.Name.Equals("Edit", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

        // Other
        elements.StatusBarId = FindByAutomationId("statusStrip1");
        elements.DateTimePickerId = FindByAutomationId("dateTimePicker1");

        return elements;
    }


    private List<ElementRecord> ResolveTestAppElementsScope(
    ElementRecord appWindow,
    IReadOnlyList<ElementRecord> allElements,
    string appWindowTitle,
    out bool usedDocumentRoot)
    {
        var debugLog = new StringBuilder();
        debugLog.AppendLine("═══════════════════════════════════════════════════════════");
        debugLog.AppendLine($"[ResolveTestAppElementsScope] START");
        debugLog.AppendLine($"  Window Title: '{appWindowTitle}'");
        debugLog.AppendLine($"  Window ID: {appWindow.Id}");
        debugLog.AppendLine($"  Window Name: '{appWindow.Name}'");
        debugLog.AppendLine($"  Window ClassName: '{appWindow.ClassName}'");
        debugLog.AppendLine($"  Window ControlType: {appWindow.ControlType}");
        debugLog.AppendLine($"  Total Elements in Registry: {allElements.Count}");

        usedDocumentRoot = false;
        var windowElements = GetAllDescendants(appWindow.Id, allElements);

        debugLog.AppendLine($"  Window Descendants: {windowElements.Count}");

        if (windowElements.Count == 0)
        {
            debugLog.AppendLine($"[ResolveTestAppElementsScope] No descendants found - returning empty list");
            debugLog.AppendLine("═══════════════════════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine(debugLog.ToString());
            return windowElements;
        }

        bool shouldPreferDocumentRoot =
            appWindowTitle.Contains("Web", StringComparison.OrdinalIgnoreCase) ||
            appWindowTitle.Contains("Browser", StringComparison.OrdinalIgnoreCase) ||
            appWindowTitle.Contains("Edge", StringComparison.OrdinalIgnoreCase) ||
            appWindowTitle.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
            appWindow.Name.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
            appWindow.Name.Contains("Edge", StringComparison.OrdinalIgnoreCase) ||
            appWindow.ClassName.Contains("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase);

        debugLog.AppendLine($"  Should Prefer Document Root: {shouldPreferDocumentRoot}");

        if (!shouldPreferDocumentRoot)
        {
            debugLog.AppendLine($"[ResolveTestAppElementsScope] Not a browser window - returning {windowElements.Count} window elements");
            debugLog.AppendLine("═══════════════════════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine(debugLog.ToString());
            return windowElements;
        }

        // Log first 10 window elements to see what we have
        debugLog.AppendLine($"  First 10 Window Elements:");
        foreach (var elem in windowElements.Take(windowElements.Count))
        {
            debugLog.AppendLine($"    - ID={elem.Id}, Type={elem.ControlType}, Name='{elem.Name}', AutomationId='{elem.AutomationId}', FrameworkId='{elem.FrameworkId}'");
        }

        // Try to find Document element in the scanned elements
        var documentCandidates = windowElements
            .Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Document)
            .ToList();

        debugLog.AppendLine($"  Document Candidates Found in Registry: {documentCandidates.Count}");
        foreach (var doc in documentCandidates)
        {
            debugLog.AppendLine($"    - Document ID={doc.Id}, Name='{doc.Name}', AutomationId='{doc.AutomationId}', ChildIds={doc.ChildIds.Count}");
        }

        if (documentCandidates.Count > 0)
        {
            var preferredDocument = documentCandidates.First();
            var documentElements = GetAllDescendants(preferredDocument.Id, allElements);
            debugLog.AppendLine($"  Using existing Document ID={preferredDocument.Id}");
            debugLog.AppendLine($"  Document Descendants: {documentElements.Count}");

            if (documentElements.Count > 0)
            {
                usedDocumentRoot = true;
                debugLog.AppendLine($"[ResolveTestAppElementsScope] SUCCESS - Returning {documentElements.Count} document elements");
                debugLog.AppendLine("═══════════════════════════════════════════════════════════");
                System.Diagnostics.Debug.WriteLine(debugLog.ToString());
                return documentElements;
            }
        }

        // If no Document found in registry, try to find it via live AutomationElement
        debugLog.AppendLine($"  No valid Document in registry - attempting live search...");
        var documentRecord = FindAndScanDocumentElement(appWindow, ref debugLog);

        if (documentRecord != null)
        {
            debugLog.AppendLine($"  Live search found Document ID={documentRecord.Id}");
            // Refresh to get updated elements list after scan
            allElements = _bridge.GetAllElements();
            debugLog.AppendLine($"  Registry refreshed - Total Elements: {allElements.Count}");

            var documentElements = GetAllDescendants(documentRecord.Id, allElements);
            debugLog.AppendLine($"  Document Descendants after rescan: {documentElements.Count}");

            if (documentElements.Count > 0)
            {
                usedDocumentRoot = true;
                debugLog.AppendLine($"[ResolveTestAppElementsScope] SUCCESS - Returning {documentElements.Count} document elements (from live search)");
                debugLog.AppendLine("═══════════════════════════════════════════════════════════");
                System.Diagnostics.Debug.WriteLine(debugLog.ToString());
                return documentElements;
            }
        }
        else
        {
            debugLog.AppendLine($"  Live search did NOT find Document element");
        }

        debugLog.AppendLine($"[ResolveTestAppElementsScope] FALLBACK - Returning {windowElements.Count} window elements");
        debugLog.AppendLine("═══════════════════════════════════════════════════════════");
        System.Diagnostics.Debug.WriteLine(debugLog.ToString());
        return windowElements;
    }

    public async Task<string> TestEcommerceWebAppByTxtCmd(string appWindowTitle = "Apex E-Commerce Test App")
    {

        var log = new StringBuilder();
        int stepNum = 0;
        int passed = 0, failed = 0, skipped = 0;

        log.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        log.AppendLine("║   E-COMMERCE WEB APP - ALL INTERACTIONS BY TEXT COMMAND        ║");
        log.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        log.AppendLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        log.AppendLine();

        log.AppendLine("═══ APP LAUNCH ═══");
        await EnsureWebAppIsOpen(appWindowTitle, log);
        log.AppendLine();

        // Scan the target window first so Bridge has elements to work with
        log.AppendLine("═══ WINDOW SCAN ═══");
        var scanResult = await _bridge.ScanWindowByName(appWindowTitle);
        log.AppendLine($"  Scan: {scanResult.message} ({scanResult.elementCount} elements)");

        if (!scanResult.success)
        {
            log.AppendLine("  FAILED: Could not scan window - is the app running?");
            return log.ToString();
        }

        // Discover elements
        log.AppendLine();
        log.AppendLine("═══ ELEMENT DISCOVERY ═══");
        var elements = DiscoverEcommerceTestPageElements(appWindowTitle);

        if (elements == null)
        {
            log.AppendLine("  FAILED: Could not find E-Commerce web app window in scanned elements");
            return log.ToString();
        }

        log.AppendLine($"  Window ID: {elements.WindowId}");
        log.AppendLine();

        // Log discovery results
        log.AppendLine("═══ DISCOVERED ELEMENTS ═══");
        void LogElement(string name, int id) =>
            log.AppendLine(id > 0 ? $"  {name,-25} ID={id}" : $"  {name,-25} NOT FOUND");

        LogElement("SearchInput", elements.SearchInputId);
        LogElement("CategorySelect", elements.CategorySelectId);
        LogElement("PriceSlider", elements.PriceSliderId);
        LogElement("FirstAddToCartButton", elements.FirstAddToCartButtonId);
        LogElement("CartQuantity", elements.CartQuantityId);
        LogElement("CouponInput", elements.CouponInputId);
        LogElement("ApplyCouponButton", elements.ApplyCouponButtonId);
        LogElement("CheckoutButton", elements.CheckoutButtonId);
        LogElement("NewsletterEmail", elements.NewsletterEmailId);
        LogElement("SubscribeButton", elements.SubscribeButtonId);
        log.AppendLine();

        // Dump all scanned elements for diagnostics
        log.AppendLine("═══ ALL SCANNED ELEMENTS ═══");
        var allScanned = _bridge.GetAllElements();
        foreach (var el in allScanned.OrderBy(e => e.Id))
        {
            log.AppendLine($"  [{el.Id,3}] {el.ControlType,-20} AutoId='{el.AutomationId}' Name='{el.Name}' Parent={el.ParentId?.ToString() ?? "root"}");
        }
        log.AppendLine();

        async Task RunStep(string description, string command, int elementId)
        {
            stepNum++;
            if (elementId == 0)
            {
                log.AppendLine($"Step {stepNum}: {description}");
                log.AppendLine($"  SKIPPED (element not found)");
                log.AppendLine();
                skipped++;
                return;
            }

            log.AppendLine($"Step {stepNum}: {description}");
            var result = await _bridge.ExecuteCommand(command);
            log.AppendLine($"  Command: {command}");
            log.AppendLine($"  Success: {result.IsSuccess}");
            log.AppendLine($"  Result: {result.Message}");
            log.AppendLine();

            if (result.IsSuccess) passed++; else failed++;
            await Task.Delay(300);
        }

        // ============================================
        // SHOPPING FLOW TESTS
        // ============================================
        log.AppendLine("═══ SHOPPING FLOW TESTS ═══");

        await RunStep("Search for running shoes", $"TYPE {elements.SearchInputId} running shoes", elements.SearchInputId);
        await RunStep("Filter category to Shoes", $"SELECT_BY_TEXT {elements.CategorySelectId} Shoes", elements.CategorySelectId);
        await RunStep("Move price slider to mid-range", $"SET_VALUE {elements.PriceSliderId} 120", elements.PriceSliderId);
        await RunStep("Add first product to cart", $"CLICK {elements.FirstAddToCartButtonId}", elements.FirstAddToCartButtonId);
        await RunStep("Update cart quantity", $"SET_VALUE {elements.CartQuantityId} 2", elements.CartQuantityId);
        await RunStep("Apply promo code", $"TYPE {elements.CouponInputId} SAVE10", elements.CouponInputId);
        await RunStep("Click apply coupon", $"CLICK {elements.ApplyCouponButtonId}", elements.ApplyCouponButtonId);
        await RunStep("Proceed to checkout", $"CLICK {elements.CheckoutButtonId}", elements.CheckoutButtonId);

        // ============================================
        // ENGAGEMENT FLOW TESTS
        // ============================================
        log.AppendLine("═══ ENGAGEMENT FLOW TESTS ═══");

        await RunStep("Enter newsletter email", $"TYPE {elements.NewsletterEmailId} qa@apexai.dev", elements.NewsletterEmailId);
        await RunStep("Subscribe", $"CLICK {elements.SubscribeButtonId}", elements.SubscribeButtonId);

        // ============================================
        // GET TEXT TESTS
        // ============================================
        log.AppendLine("═══ GET TEXT TESTS ═══");

        await RunStep("Get Text from SearchInput", $"GET_TEXT {elements.SearchInputId}", elements.SearchInputId);

        // ============================================
        // FOCUS/HOVER TESTS
        // ============================================
        log.AppendLine("═══ FOCUS/HOVER TESTS ═══");

        await RunStep("Focus SearchInput", $"FOCUS {elements.SearchInputId}", elements.SearchInputId);
        await RunStep("Hover CheckoutButton", $"HOVER {elements.CheckoutButtonId}", elements.CheckoutButtonId);
        await RunStep("Hover SubscribeButton", $"HOVER {elements.SubscribeButtonId}", elements.SubscribeButtonId);

        // ============================================
        // WINDOW TESTS
        // ============================================
        log.AppendLine("═══ WINDOW TESTS ═══");

        await RunStep("Minimize Window", $"WINDOW_ACTION {elements.WindowId} minimize", elements.WindowId);
        await Task.Delay(500);
        await RunStep("Restore Window", $"WINDOW_ACTION {elements.WindowId} restore", elements.WindowId);

        // ============================================
        // SUMMARY
        // ============================================
        log.AppendLine("═══ TEST SUMMARY ═══");
        log.AppendLine($"  Total Steps: {stepNum}");
        log.AppendLine($"  Passed:  {passed}");
        log.AppendLine($"  Failed:  {failed}");
        log.AppendLine($"  Skipped: {skipped}");
        log.AppendLine($"  Success Rate: {(passed + failed > 0 ? (passed * 100.0 / (passed + failed)) : 0):F1}%");
        log.AppendLine();
        log.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        log.AppendLine("║                    TEST COMPLETE                               ║");
        log.AppendLine("╚════════════════════════════════════════════════════════════════╝");

        var finalResult = log.ToString();
        System.Diagnostics.Debug.WriteLine(finalResult);
        return finalResult;
    }

    private EcommerceTestPageElements? DiscoverEcommerceTestPageElements(string appWindowTitle = "Apex E-Commerce Test App")
    {
        var debugLog = new StringBuilder();
        debugLog.AppendLine("═══════════════════════════════════════════════════════════");
        debugLog.AppendLine($"[DiscoverEcommerceTestPageElements] START");
        debugLog.AppendLine($"  Looking for window: '{appWindowTitle}'");

        if (_bridge == null)
        {
            debugLog.AppendLine($"  ERROR: _bridge is NULL");
            debugLog.AppendLine("═══════════════════════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine(debugLog.ToString());
            return null;
        }

        var allElements = _bridge.GetAllElements();
        debugLog.AppendLine($"  Total elements in registry: {allElements.Count}");

        var windows = allElements.Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Window).ToList();
        debugLog.AppendLine($"  Total windows in registry: {windows.Count}");

        debugLog.AppendLine($"  Window list:");
        foreach (var w in windows)
        {
            debugLog.AppendLine($"    - ID={w.Id}, Name='{w.Name}', ClassName='{w.ClassName}'");
        }

        var appWindow = allElements.FirstOrDefault(e =>
            e.ControlType == FlaUI.Core.Definitions.ControlType.Document &&
            !string.IsNullOrWhiteSpace(e.Name) &&
            e.Name.Contains(appWindowTitle, StringComparison.OrdinalIgnoreCase));

        if (appWindow == null)
        {
            debugLog.AppendLine($"  ERROR: Window '{appWindowTitle}' not found");
            debugLog.AppendLine("═══════════════════════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine(debugLog.ToString());
            return null;
        }

        debugLog.AppendLine($"  Found window:");
        debugLog.AppendLine($"    ID: {appWindow.Id}");
        debugLog.AppendLine($"    Name: '{appWindow.Name}'");
        debugLog.AppendLine($"    ClassName: '{appWindow.ClassName}'");
        debugLog.AppendLine($"    ControlType: {appWindow.ControlType}");
        debugLog.AppendLine($"    FrameworkId: '{appWindow.FrameworkId}'");
        debugLog.AppendLine($"    WindowHandle: {appWindow.WindowHandle}");

        var scoped = ResolveTestAppElementsScope(appWindow, allElements, appWindowTitle, out var usedDocumentRoot);

        debugLog.AppendLine($"  After ResolveTestAppElementsScope:");
        debugLog.AppendLine($"    Scoped elements: {scoped.Count}");
        debugLog.AppendLine($"    Used Document Root: {usedDocumentRoot}");

        if (scoped.Count > 0)
        {
            debugLog.AppendLine($"  First 10 scoped elements:");
            foreach (var elem in scoped.Take(10))
            {
                debugLog.AppendLine($"    - ID={elem.Id}, Type={elem.ControlType}, Name='{elem.Name}', AutomationId='{elem.AutomationId}'");
            }
        }

        debugLog.AppendLine("═══════════════════════════════════════════════════════════");
        System.Diagnostics.Debug.WriteLine(debugLog.ToString());

        // Rest of method unchanged - but now we know what scoped contains
        var searchSpace = new List<ElementRecord>(scoped);
        if (usedDocumentRoot)
        {
            var documentRoot = allElements.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Document && scoped.Any(s => s.ParentId == e.Id));
            if (documentRoot != null)
            {
                searchSpace.Insert(0, documentRoot);
            }
        }

        int FindElementId(string[] keys, params FlaUI.Core.Definitions.ControlType[] preferredTypes)
        {
            bool Matches(ElementRecord e) => keys.Any(k =>
                (!string.IsNullOrWhiteSpace(e.Name) && e.Name.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(e.AutomationId) && e.AutomationId.Contains(k, StringComparison.OrdinalIgnoreCase)));

            var candidates = searchSpace.Where(Matches).ToList();

            // Fallback: if nothing found in scoped search space, search all elements
            // under the same window (e.g. header elements outside Document scope)
            if (candidates.Count == 0)
            {
                candidates = allElements.Where(Matches).ToList();
            }

            if (candidates.Count == 0)
                return 0;

            if (preferredTypes.Length > 0)
            {
                var preferred = candidates.FirstOrDefault(c => preferredTypes.Contains(c.ControlType));
                if (preferred != null)
                    return preferred.Id;
            }

            return candidates.First().Id;
        }

        return new EcommerceTestPageElements
        {
            WindowId = appWindow.Id,
            SearchInputId = FindElementId(new[] { "searchProducts", "Search products" }, FlaUI.Core.Definitions.ControlType.Edit, FlaUI.Core.Definitions.ControlType.Document),
            CategorySelectId = FindElementId(new[] { "categoryFilter", "Category", "All Categories" }, FlaUI.Core.Definitions.ControlType.ComboBox, FlaUI.Core.Definitions.ControlType.Document),
            PriceSliderId = FindElementId(new[] { "priceRange", "Price range" }, FlaUI.Core.Definitions.ControlType.Slider, FlaUI.Core.Definitions.ControlType.Document),
            FirstAddToCartButtonId = FindElementId(new[] { "Add to Cart" }, FlaUI.Core.Definitions.ControlType.Button, FlaUI.Core.Definitions.ControlType.Document),
            CartQuantityId = FindElementId(new[] { "cartQuantity", "Quantity" }, FlaUI.Core.Definitions.ControlType.Edit, FlaUI.Core.Definitions.ControlType.Spinner, FlaUI.Core.Definitions.ControlType.Document),
            CouponInputId = FindElementId(new[] { "couponCode", "Coupon code" }, FlaUI.Core.Definitions.ControlType.Edit, FlaUI.Core.Definitions.ControlType.Document),
            ApplyCouponButtonId = FindElementId(new[] { "applyCoupon", "Apply Coupon" }, FlaUI.Core.Definitions.ControlType.Button, FlaUI.Core.Definitions.ControlType.Document),
            CheckoutButtonId = FindElementId(new[] { "checkoutButton", "Checkout" }, FlaUI.Core.Definitions.ControlType.Button, FlaUI.Core.Definitions.ControlType.Document),
            NewsletterEmailId = FindElementId(new[] { "newsletterEmail", "Email address" }, FlaUI.Core.Definitions.ControlType.Edit, FlaUI.Core.Definitions.ControlType.Document),
            SubscribeButtonId = FindElementId(new[] { "subscribeButton", "Subscribe" }, FlaUI.Core.Definitions.ControlType.Button, FlaUI.Core.Definitions.ControlType.Document)
        };
    }



    /// <summary>
    /// Gets all descendants of an element by walking parent-child relationships.
    /// </summary>
    private static List<ElementRecord> GetAllDescendants(int parentId, IReadOnlyList<ElementRecord> allElements)
    {
        var result = new List<ElementRecord>();
        var directChildren = allElements.Where(e => e.ParentId == parentId).ToList();

        foreach (var child in directChildren)
        {
            result.Add(child);
            result.AddRange(GetAllDescendants(child.Id, allElements));
        }

        return result;
    }

    /// <summary>
    /// Attempts to find and scan a Document element within a window.
    /// Only relevant for browser windows; returns null for native apps.
    /// </summary>
    private ElementRecord? FindAndScanDocumentElement(ElementRecord appWindow, ref StringBuilder debugLog)
    {
        // Not needed for WinForms test apps — only browser windows use Document elements
        debugLog.AppendLine("  FindAndScanDocumentElement: Not applicable for this window type");
        return null;
    }

    public async Task<string> TestWpfAppByTxtCmd(string appWindowTitle = "FlaUI WPF Test App")
    {
        var log = new StringBuilder();
        int stepNum = 0;
        int passed = 0, failed = 0, skipped = 0;

        log.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        log.AppendLine("║   WPF TEST APP - ALL INTERACTIONS BY TEXT COMMAND              ║");
        log.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        log.AppendLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        log.AppendLine();

        log.AppendLine("═══ APP LAUNCH ═══");
        await EnsureTestAppIsRunning(appWindowTitle, "WpfApplication.exe", log);
        log.AppendLine();

        // Scan the target window first so Bridge has elements to work with
        log.AppendLine("═══ WINDOW SCAN ═══");
        var scanResult = await _bridge.ScanWindowByName(appWindowTitle);
        log.AppendLine($"  Scan: {scanResult.message} ({scanResult.elementCount} elements)");

        if (!scanResult.success)
        {
            log.AppendLine("  FAILED: Could not scan window - is the app running?");
            return log.ToString();
        }

        // Discover elements
        log.AppendLine();
        log.AppendLine("═══ ELEMENT DISCOVERY ═══");
        var elements = DiscoverWpfTestAppElements(appWindowTitle);

        if (elements == null)
        {
            log.AppendLine("  FAILED: Could not find WPF Test App window in scanned elements");
            return log.ToString();
        }

        log.AppendLine($"  Window ID: {elements.WindowId}");
        log.AppendLine();

        // Log discovery results
        log.AppendLine("═══ DISCOVERED ELEMENTS ═══");
        void LogElement(string name, int id) =>
            log.AppendLine(id > 0 ? $"  {name,-25} ID={id}" : $"  {name,-25} NOT FOUND");

        LogElement("TextBox", elements.TextBoxId);
        LogElement("PasswordBox", elements.PasswordBoxId);
        LogElement("EditableCombo", elements.EditableComboId);
        LogElement("NonEditableCombo", elements.NonEditableComboId);
        LogElement("ListBox", elements.ListBoxId);
        LogElement("SimpleCheckBox", elements.SimpleCheckBoxId);
        LogElement("ThreeStateCheckBox", elements.ThreeStateCheckBoxId);
        LogElement("RadioButton1", elements.RadioButton1Id);
        LogElement("RadioButton2", elements.RadioButton2Id);
        LogElement("Slider", elements.SliderId);
        LogElement("InvokableButton", elements.InvokableButtonId);
        LogElement("TreeView", elements.TreeViewId);
        LogElement("ListView", elements.ListViewId);
        LogElement("DataGrid", elements.DataGridId);
        LogElement("TabControl", elements.TabControlId);
        LogElement("TabSimple", elements.TabSimpleId);
        LogElement("TabComplex", elements.TabComplexId);
        LogElement("MenuBar", elements.MenuBarId);
        LogElement("FileMenu", elements.FileMenuId);
        LogElement("EditMenu", elements.EditMenuId);
        LogElement("StatusBar", elements.StatusBarId);
        log.AppendLine();

        // Dump all scanned elements for diagnostics
        log.AppendLine("═══ ALL SCANNED ELEMENTS ═══");
        var allScanned = _bridge.GetAllElements();
        foreach (var el in allScanned.OrderBy(e => e.Id))
        {
            log.AppendLine($"  [{el.Id,3}] {el.ControlType,-20} AutoId='{el.AutomationId}' Name='{el.Name}' Parent={el.ParentId?.ToString() ?? "root"}");
        }
        log.AppendLine();

        async Task RunStep(string description, string command, int elementId)
        {
            stepNum++;
            if (elementId == 0)
            {
                log.AppendLine($"Step {stepNum}: {description}");
                log.AppendLine($"  SKIPPED (element not found)");
                log.AppendLine();
                skipped++;
                return;
            }

            log.AppendLine($"Step {stepNum}: {description}");
            var result = await _bridge.ExecuteCommand(command);
            log.AppendLine($"  Command: {command}");
            log.AppendLine($"  Success: {result.IsSuccess}");
            log.AppendLine($"  Result: {result.Message}");
            log.AppendLine();

            if (result.IsSuccess) passed++; else failed++;
            await Task.Delay(300);
        }

        // ============================================
        // TEXT INPUT TESTS
        // ============================================
        log.AppendLine("═══ TEXT INPUT TESTS ═══");

        await RunStep("Type in TextBox", $"TYPE {elements.TextBoxId} Hello from TxtCmd!", elements.TextBoxId);
        await RunStep("Set Value in PasswordBox", $"SET_VALUE {elements.PasswordBoxId} Secret123", elements.PasswordBoxId);

        // Clear text fields
        if (elements.TextBoxId > 0) await _bridge.ExecuteCommand($"SET_VALUE {elements.TextBoxId} ");
        if (elements.PasswordBoxId > 0) await _bridge.ExecuteCommand($"SET_VALUE {elements.PasswordBoxId} ");

        // ============================================
        // GET TEXT TESTS
        // ============================================
        log.AppendLine("═══ GET TEXT TESTS ═══");

        await RunStep("Get Text from TextBox", $"GET_TEXT {elements.TextBoxId}", elements.TextBoxId);

        // ============================================
        // TOGGLE TESTS
        // ============================================
        log.AppendLine("═══ TOGGLE TESTS ═══");

        await RunStep("Toggle SimpleCheckBox", $"TOGGLE {elements.SimpleCheckBoxId}", elements.SimpleCheckBoxId);
        await RunStep("Toggle ThreeStateCheckBox", $"TOGGLE {elements.ThreeStateCheckBoxId}", elements.ThreeStateCheckBoxId);

        // Toggle back
        if (elements.SimpleCheckBoxId > 0) await _bridge.ExecuteCommand($"TOGGLE {elements.SimpleCheckBoxId}");

        // ============================================
        // RADIOBUTTON TESTS
        // ============================================
        log.AppendLine("═══ RADIOBUTTON TESTS ═══");

        await RunStep("Select RadioButton1", $"CLICK {elements.RadioButton1Id}", elements.RadioButton1Id);
        await RunStep("Select RadioButton2", $"CLICK {elements.RadioButton2Id}", elements.RadioButton2Id);

        // ============================================
        // COMBOBOX TESTS
        // ============================================
        log.AppendLine("═══ COMBOBOX TESTS ═══");

        await RunStep("Expand EditableCombo", $"EXPAND {elements.EditableComboId}", elements.EditableComboId);
        await RunStep("Collapse EditableCombo", $"COLLAPSE {elements.EditableComboId}", elements.EditableComboId);
        await RunStep("Select by Text in EditableCombo", $"SELECT_BY_TEXT {elements.EditableComboId} Item 2", elements.EditableComboId);
        await RunStep("Select by Index in NonEditableCombo", $"SELECT_BY_INDEX {elements.NonEditableComboId} 0", elements.NonEditableComboId);

        // ============================================
        // SLIDER TESTS
        // ============================================
        log.AppendLine("═══ SLIDER TESTS ═══");

        await RunStep("Set Slider to 8", $"SET_VALUE {elements.SliderId} 8", elements.SliderId);
        await RunStep("Set Slider to 2", $"SET_VALUE {elements.SliderId} 2", elements.SliderId);

        // ============================================
        // LISTBOX TESTS
        // ============================================
        log.AppendLine("═══ LISTBOX TESTS ═══");

        await RunStep("Focus ListBox", $"FOCUS {elements.ListBoxId}", elements.ListBoxId);
        await RunStep("Scroll ListBox Down", $"SCROLL {elements.ListBoxId} down 1", elements.ListBoxId);

        // ============================================
        // BUTTON TESTS
        // ============================================
        log.AppendLine("═══ BUTTON TESTS ═══");

        await RunStep("Click InvokableButton", $"CLICK {elements.InvokableButtonId}", elements.InvokableButtonId);

        // ============================================
        // TAB TESTS
        // ============================================
        log.AppendLine("═══ TAB TESTS ═══");

        bool tabSwitched = false;
        if (elements.TabComplexId > 0)
        {
            await RunStep("Select Tab 'Complex Controls'", $"CLICK {elements.TabComplexId}", elements.TabComplexId);
            tabSwitched = true;
        }
        else if (elements.TabControlId > 0)
        {
            await RunStep("Select Tab 'Complex Controls' via TabControl index", $"SELECT_BY_INDEX {elements.TabControlId} 1", elements.TabControlId);
            tabSwitched = true;
        }
        await Task.Delay(500);

        // Rescan after tab switch
        if (tabSwitched)
        {
            log.AppendLine("═══ POST-TAB-SWITCH RESCAN ═══");
            var rescanResult = await _bridge.ScanWindowByName(appWindowTitle);
            log.AppendLine($"  Rescan: {rescanResult.message} ({rescanResult.elementCount} elements)");

            var refreshedElements = DiscoverWpfTestAppElements(appWindowTitle);
            if (refreshedElements != null)
            {
                elements.WindowId = refreshedElements.WindowId;
                elements.TextBoxId = refreshedElements.TextBoxId;
                elements.PasswordBoxId = refreshedElements.PasswordBoxId;
                elements.EditableComboId = refreshedElements.EditableComboId;
                elements.NonEditableComboId = refreshedElements.NonEditableComboId;
                elements.ListBoxId = refreshedElements.ListBoxId;
                elements.SimpleCheckBoxId = refreshedElements.SimpleCheckBoxId;
                elements.ThreeStateCheckBoxId = refreshedElements.ThreeStateCheckBoxId;
                elements.RadioButton1Id = refreshedElements.RadioButton1Id;
                elements.RadioButton2Id = refreshedElements.RadioButton2Id;
                elements.SliderId = refreshedElements.SliderId;
                elements.InvokableButtonId = refreshedElements.InvokableButtonId;
                elements.TreeViewId = refreshedElements.TreeViewId;
                elements.ListViewId = refreshedElements.ListViewId;
                elements.DataGridId = refreshedElements.DataGridId;
                elements.MenuBarId = refreshedElements.MenuBarId;
                elements.FileMenuId = refreshedElements.FileMenuId;
                elements.EditMenuId = refreshedElements.EditMenuId;
                elements.StatusBarId = refreshedElements.StatusBarId;
                elements.TabControlId = refreshedElements.TabControlId;
                elements.TabSimpleId = refreshedElements.TabSimpleId;
                elements.TabComplexId = refreshedElements.TabComplexId;

                log.AppendLine($"  Re-discovered: TreeView={elements.TreeViewId}, ListView={elements.ListViewId}, DataGrid={elements.DataGridId}");
                log.AppendLine($"  Re-discovered: TabSimple={elements.TabSimpleId}, TabComplex={elements.TabComplexId}");
            }
            else
            {
                log.AppendLine("  WARNING: Re-discovery failed after rescan");
            }
            log.AppendLine();
        }

        // Find an expandable TreeItem
        int treeItemId = 0;
        if (elements.TreeViewId > 0)
        {
            var allElems = _bridge.GetAllElements();
            var treeItems = allElems.Where(e =>
                e.ControlType == FlaUI.Core.Definitions.ControlType.TreeItem &&
                e.ParentId == elements.TreeViewId).ToList();
            treeItemId = treeItems.FirstOrDefault(ti =>
                allElems.Any(e => e.ParentId == ti.Id))?.Id ?? treeItems.FirstOrDefault()?.Id ?? 0;
        }

        // ============================================
        // COMPLEX CONTROLS TESTS
        // ============================================
        log.AppendLine("═══ COMPLEX CONTROLS TESTS ═══");

        await RunStep("Expand TreeView Node", $"EXPAND {treeItemId}", treeItemId);
        await RunStep("Collapse TreeView Node", $"COLLAPSE {treeItemId}", treeItemId);
        await RunStep("Focus ListView", $"FOCUS {elements.ListViewId}", elements.ListViewId);
        await RunStep("Scroll ListView", $"SCROLL {elements.ListViewId} down 1", elements.ListViewId);
        await RunStep("Focus DataGrid", $"FOCUS {elements.DataGridId}", elements.DataGridId);

        await RunStep("Select Tab 'Simple Controls'", $"CLICK {elements.TabSimpleId}", elements.TabSimpleId);
        await Task.Delay(300);

        // ============================================
        // MENU TESTS
        // ============================================
        log.AppendLine("═══ MENU TESTS ═══");

        await RunStep("Click File Menu", $"CLICK {elements.FileMenuId}", elements.FileMenuId);
        if (elements.FileMenuId > 0)
            await _bridge.ExecuteCommand($"SEND_KEYS {elements.FileMenuId} {{ESC}}");

        // ============================================
        // FOCUS/HOVER TESTS
        // ============================================
        log.AppendLine("═══ FOCUS/HOVER TESTS ═══");

        await RunStep("Focus TextBox", $"FOCUS {elements.TextBoxId}", elements.TextBoxId);
        await RunStep("Highlight StatusBar", $"HIGHLIGHT {elements.StatusBarId}", elements.StatusBarId);

        // ============================================
        // WINDOW TESTS
        // ============================================
        log.AppendLine("═══ WINDOW TESTS ═══");

        await RunStep("Minimize Window", $"WINDOW_ACTION {elements.WindowId} minimize", elements.WindowId);
        await Task.Delay(500);
        await RunStep("Restore Window", $"WINDOW_ACTION {elements.WindowId} restore", elements.WindowId);

        // ============================================
        // SUMMARY
        // ============================================
        log.AppendLine("═══ TEST SUMMARY ═══");
        log.AppendLine($"  Total Steps: {stepNum}");
        log.AppendLine($"  Passed:  {passed}");
        log.AppendLine($"  Failed:  {failed}");
        log.AppendLine($"  Skipped: {skipped}");
        log.AppendLine($"  Success Rate: {(passed + failed > 0 ? (passed * 100.0 / (passed + failed)) : 0):F1}%");
        log.AppendLine();
        log.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        log.AppendLine("║                    TEST COMPLETE                               ║");
        log.AppendLine("╚════════════════════════════════════════════════════════════════╝");

        var finalResult = log.ToString();
        System.Diagnostics.Debug.WriteLine(finalResult);
        return finalResult;
    }

    private WpfTestAppElements? DiscoverWpfTestAppElements(string appWindowTitle = "FlaUI WPF Test App")
    {
        if (_bridge == null) return null;

        var allElements = _bridge.GetAllElements();

        var appWindow = allElements.FirstOrDefault(e =>
            e.ControlType == FlaUI.Core.Definitions.ControlType.Window &&
            !string.IsNullOrEmpty(e.Name) &&
            e.Name.Contains(appWindowTitle, StringComparison.OrdinalIgnoreCase));

        if (appWindow == null) return null;

        var windowElements = ResolveTestAppElementsScope(appWindow, allElements, appWindowTitle, out _);
        var elements = new WpfTestAppElements { WindowId = appWindow.Id };

        int FindByAutomationId(string automationId) =>
            windowElements.FirstOrDefault(e => e.AutomationId == automationId)?.Id ?? 0;

        int FindByNameAndType(string name, FlaUI.Core.Definitions.ControlType controlType) =>
            windowElements.FirstOrDefault(e =>
                e.ControlType == controlType &&
                !string.IsNullOrEmpty(e.Name) &&
                e.Name.Contains(name, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

        elements.TextBoxId = FindByAutomationId("TextBox");
        elements.PasswordBoxId = FindByAutomationId("PasswordBox");
        elements.EditableComboId = FindByAutomationId("EditableCombo");
        elements.NonEditableComboId = FindByAutomationId("NonEditableCombo");
        elements.ListBoxId = FindByAutomationId("ListBox");
        elements.SimpleCheckBoxId = FindByAutomationId("SimpleCheckBox");
        elements.ThreeStateCheckBoxId = FindByAutomationId("ThreeStateCheckBox");
        elements.RadioButton1Id = FindByAutomationId("RadioButton1");
        elements.RadioButton2Id = FindByAutomationId("RadioButton2");
        elements.SliderId = FindByAutomationId("Slider");
        elements.InvokableButtonId = FindByAutomationId("InvokableButton");
        elements.TreeViewId = FindByAutomationId("treeView1");
        elements.ListViewId = FindByAutomationId("listView1");
        elements.DataGridId = FindByAutomationId("dataGridView");

        // Tabs
        elements.TabControlId = FindByNameAndType("TabControl", FlaUI.Core.Definitions.ControlType.Tab);
        if (elements.TabControlId == 0)
            elements.TabControlId = windowElements.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Tab)?.Id ?? 0;

        elements.TabSimpleId = FindByNameAndType("Simple Controls", FlaUI.Core.Definitions.ControlType.TabItem);
        elements.TabComplexId = FindByNameAndType("Complex Controls", FlaUI.Core.Definitions.ControlType.TabItem);

        if (elements.TabComplexId == 0 && elements.TabControlId > 0)
        {
            var tabChildren = allElements
                .Where(e => e.ParentId == elements.TabControlId && e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem)
                .ToList();
            if (tabChildren.Count >= 2)
                elements.TabComplexId = tabChildren[1].Id;
            if (elements.TabSimpleId == 0 && tabChildren.Count >= 1)
                elements.TabSimpleId = tabChildren[0].Id;
        }

        // Menus
        elements.MenuBarId = windowElements.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.MenuBar)?.Id ?? 0;
        elements.FileMenuId = FindByNameAndType("File", FlaUI.Core.Definitions.ControlType.MenuItem);
        elements.EditMenuId = FindByNameAndType("Edit", FlaUI.Core.Definitions.ControlType.MenuItem);

        if ((elements.FileMenuId == 0 || elements.EditMenuId == 0) && elements.MenuBarId > 0)
        {
            var menuChildren = allElements.Where(e => e.ParentId == elements.MenuBarId).ToList();
            if (elements.FileMenuId == 0)
                elements.FileMenuId = menuChildren.FirstOrDefault(e =>
                    !string.IsNullOrEmpty(e.Name) && e.Name.Contains("File", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
            if (elements.EditMenuId == 0)
                elements.EditMenuId = menuChildren.FirstOrDefault(e =>
                    !string.IsNullOrEmpty(e.Name) && e.Name.Contains("Edit", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
        }

        elements.StatusBarId = windowElements.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.StatusBar)?.Id ?? 0;

        return elements;
    }

    private static bool IsWindowOpen(string windowTitle) =>
        Process.GetProcesses().Any(p =>
        {
            try { return !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowTitle.Contains(windowTitle, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        });

    private static string? FindInTestApps(string fileName)
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        // Walk up from bin/[Config]/[TFM]/ to the solution folder containing TestApplications
        for (int i = 0; i < 4; i++)
            dir = Path.GetDirectoryName(dir) ?? dir;
        var testAppsRoot = Path.Combine(dir, "TestApplications");
        if (!Directory.Exists(testAppsRoot)) return null;
        return Directory.EnumerateFiles(testAppsRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    private async Task EnsureTestAppIsRunning(string windowTitle, string exeName, StringBuilder log)
    {
        if (IsWindowOpen(windowTitle)) return;

        var exePath = FindInTestApps(exeName);
        if (exePath == null)
        {
            log.AppendLine($"  NOTE: '{exeName}' not found in TestApplications - please launch manually.");
            return;
        }

        log.AppendLine($"  Launching: {exePath}");
        Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        await Task.Delay(2000);
    }

    private async Task EnsureWebAppIsOpen(string windowTitle, StringBuilder log)
    {
        if (IsWindowOpen(windowTitle)) return;

        var htmlPath = FindInTestApps("index.html");
        if (htmlPath == null)
        {
            log.AppendLine($"  NOTE: 'index.html' not found in TestApplications - please open manually.");
            return;
        }

        log.AppendLine($"  Opening in browser: {htmlPath}");
        Process.Start(new ProcessStartInfo(htmlPath) { UseShellExecute = true });
        await Task.Delay(2000);
    }

 
    public async Task<string> TestMenuByTxtCmd(string appWindowTitle = "FlaUI Menu Test App")
    {
        var log = new StringBuilder();
        int stepNum = 0;
        int passed = 0, failed = 0, skipped = 0;

        log.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        log.AppendLine("║   MENU TEST - ALL INTERACTIONS BY TEXT COMMAND                 ║");
        log.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        log.AppendLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        log.AppendLine();

        log.AppendLine("═══ APP LAUNCH ═══");
        await EnsureTestAppIsRunning(appWindowTitle, "MenuTestApp.exe", log);
        log.AppendLine();

        async Task RunStep(string description, Func<string> getMenus){
            stepNum++;
            log.AppendLine($"Step {stepNum}: {description}");
            var result = await Task.Run(getMenus);
            var isSuccess = !string.IsNullOrEmpty(result);
            log.AppendLine($"  Success: {isSuccess}");
            log.AppendLine($"  Result: {(isSuccess ? "(menu data returned)" : "(empty - window not found or no menu bar)")}");
            log.AppendLine();
            if (isSuccess) passed++; else failed++;
            await Task.Delay(300);
        }

        // ============================================
        // MENU HIERARCHY TESTS
        // ============================================
        log.AppendLine("═══ MENU HIERARCHY TESTS ═══");

        await RunStep("Get full menu hierarchy",
            () => _bridge.GetMenus(targetApp: appWindowTitle));

        await RunStep("Get menu hierarchy with depth limit",
            () => _bridge.GetMenus(targetApp: appWindowTitle, depth: 15));

        // ============================================
        // MENU CLICK TESTS
        // ============================================
        log.AppendLine("═══ MENU CLICK TESTS ═══");

        await RunStep("Click 'Reset Zoom' via View menu (with startingPoint)",
            () => _bridge.GetMenus(targetApp: appWindowTitle, menuToClick: "Reset Zoom", startingPoint: "View", depth: 10));

        await RunStep("Click 'About' (search all menus, no startingPoint)",
            () => _bridge.GetMenus(targetApp: appWindowTitle, menuToClick: "About", depth: 15));

        // ============================================
        // SUMMARY
        // ============================================
        log.AppendLine("═══ TEST SUMMARY ═══");
        log.AppendLine($"  Total Steps: {stepNum}");
        log.AppendLine($"  Passed:  {passed}");
        log.AppendLine($"  Failed:  {failed}");
        log.AppendLine($"  Skipped: {skipped}");
        log.AppendLine($"  Success Rate: {(passed + failed > 0 ? (passed * 100.0 / (passed + failed)) : 0):F1}%");
        log.AppendLine();
        log.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        log.AppendLine("║                    TEST COMPLETE                               ║");
        log.AppendLine("╚════════════════════════════════════════════════════════════════╝");

        var finalResult = log.ToString();
        System.Diagnostics.Debug.WriteLine(finalResult);
        return finalResult;
    }


}
