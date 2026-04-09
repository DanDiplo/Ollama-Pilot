namespace OllamaPilot.Core.Models;

public sealed class OllamaChatTurn
{
    public string Role { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
}
