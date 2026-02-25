namespace ApexUIBridge.Models;

public class AiSettings
{
    public string Provider { get; set; } = "LlamaSharp Instruct";
    public string ModelPath { get; set; } = "";
    public string AnthropicApiKey { get; set; } = "";
    public string AnthropicModel { get; set; } = "claude-haiku-4-5-20251001";
    public float Temperature { get; set; } = 0.8f;
    public string ReasoningEffort { get; set; } = "medium";
    public int MaxTokens { get; set; } = 2048;
    public int Threads { get; set; } = 10;
    public int ContextSize { get; set; } = 4096;
    public int GpuLayers { get; set; } = 10;
    public List<string> AntiPromptsChat { get; set; } = new() { "User:" };
    public List<string> AntiPromptsInstruct { get; set; } = new() { "[INST]" };
    public bool ShowThinking { get; set; } = false;
    public bool AutoExec { get; set; } = false;
    public string SystemPrompt { get; set; } = "";
}
