using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using OllamaPilot.Extensibility.Models;

namespace OllamaPilot.Extensibility.Services;

internal sealed class EditorApplyService
{
    private readonly VisualStudioExtensibility _extensibility;

    public EditorApplyService(VisualStudioExtensibility extensibility)
    {
        _extensibility = extensibility;
    }

    public async Task<ApplyCodeResult> ReplaceGeneratedCodeAsync(IClientContext clientContext, ApplyTarget? applyTarget, string newText, CancellationToken cancellationToken)
    {
        using var textView = await clientContext.GetActiveTextViewAsync(cancellationToken);
        if (textView is null)
        {
            return Failure("No active editor was found.");
        }

        if (string.IsNullOrWhiteSpace(newText))
        {
            return Failure("No Ollama code is available to apply.");
        }

        if (applyTarget is null || !applyTarget.IsValid)
        {
            return Failure("No saved apply target is available for this Ollama result.");
        }

        if (!string.Equals(textView.FilePath, applyTarget.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Open the same file that was used when this Ollama result was generated.");
        }

        TextRange translatedRange;
        try
        {
            translatedRange = textView.Document.TranslateRangeTo(applyTarget.OriginalRange, TextRangeTrackingMode.ExtendForwardAndBackward);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Failure("The original apply target could not be resolved in the current document.");
        }

        var result = await _extensibility.Editor().EditAsync(batch => textView.Document.AsEditable(batch).Replace(translatedRange, newText), cancellationToken);
        return result.Succeeded ? new ApplyCodeResult { Success = true, Message = "Code applied." } : Failure("Visual Studio could not apply the Ollama code to the original target.");
    }

    private static ApplyCodeResult Failure(string message) => new() { Success = false, Message = message };
}
