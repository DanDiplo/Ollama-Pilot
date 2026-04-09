using Microsoft.VisualStudio.Extensibility.Editor;
using OllamaPilot.Core.Models;

namespace OllamaPilot.Extensibility.Models;

internal sealed class ApplyTarget
{
    public ApplyTargetKind Kind { get; init; }
    public string? FilePath { get; init; }
    public TextRange OriginalRange { get; init; }
    public bool IsValid => !string.IsNullOrWhiteSpace(FilePath);
}
