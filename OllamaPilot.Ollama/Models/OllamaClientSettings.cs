namespace OllamaPilot.Ollama.Models;

public sealed class OllamaClientSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string? AccessToken { get; set; }
    public string SelectedModel { get; set; } = "qwen2.5-coder:7b";
    public string SelectedChatModel { get; set; } = "qwen2.5-coder:7b";
    public float DefaultTemperature { get; set; } = 0.2f;
    public float ChatTemperature { get; set; } = 0.3f;
    public int ChatContextWindow { get; set; } = 4096;
    public int MaxOutputTokens { get; set; } = 2048;
    public bool IncludeSelectionByDefault { get; set; } = true;
    public ThinkingDepth ChatThinkingDepth { get; set; } = ThinkingDepth.Medium;
}
