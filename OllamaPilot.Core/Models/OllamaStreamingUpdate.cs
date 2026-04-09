namespace OllamaPilot.Core.Models;

public sealed class OllamaStreamingUpdate
{
    public string TextChunk { get; init; } = string.Empty;
    public string ThinkingChunk { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
    public string? ErrorMessage { get; init; }
}
