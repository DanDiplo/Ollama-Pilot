namespace OllamaPilot.Core.Models;

public sealed class OllamaParsedResponse
{
    public string FullText { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public string CodeBlock { get; init; } = string.Empty;
    public bool IsApplyReady { get; init; }
    public string? Language { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public string? SourceFilePath { get; init; }
    public object? ApplyTarget { get; init; }
    public bool HasCodeBlock => !string.IsNullOrWhiteSpace(CodeBlock);
}
