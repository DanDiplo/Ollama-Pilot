namespace OllamaPilot.Core.Models;

public sealed class CodeContext
{
    private string? _overrideCode;

    public string? FilePath { get; init; }
    public string? Language { get; init; }
    public string SelectedText { get; init; } = string.Empty;
    public string DocumentText { get; init; } = string.Empty;
    public string? DiagnosticText { get; init; }
    public object? ApplyTarget { get; init; }

    public bool HasSelection => !string.IsNullOrWhiteSpace(SelectedText);

    public string EffectiveCode
    {
        get => _overrideCode ?? (HasSelection ? SelectedText : DocumentText);
        init => _overrideCode = value;
    }
}
