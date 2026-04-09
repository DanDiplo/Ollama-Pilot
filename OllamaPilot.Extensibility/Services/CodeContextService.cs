using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using OllamaPilot.Core.Helpers;
using OllamaPilot.Core.Models;
using OllamaPilot.Extensibility.Models;

namespace OllamaPilot.Extensibility.Services;

internal sealed class CodeContextService
{
    public async Task<CodeContext?> GetCurrentContextAsync(IClientContext clientContext, CancellationToken cancellationToken)
        => await GetCurrentContextAsync(clientContext, includeFullDocumentText: false, cancellationToken);

    public async Task<CodeContext?> GetCurrentContextAsync(IClientContext clientContext, bool includeFullDocumentText, CancellationToken cancellationToken)
    {
        using var textView = await clientContext.GetActiveTextViewAsync(cancellationToken);
        if (textView is null)
        {
            return null;
        }

        bool hasSelection = !textView.Selection.IsEmpty;
        string selectedText = textView.Selection.Extent.CopyToString();
        string documentText = !hasSelection || includeFullDocumentText ? textView.Document.Text.CopyToString() : string.Empty;
        string? filePath = textView.FilePath;
        TextRange applyRange = hasSelection ? textView.Selection.Extent : textView.Document.Text;

        return new CodeContext
        {
            SelectedText = selectedText,
            DocumentText = documentText,
            FilePath = filePath,
            Language = LanguageHelper.InferLanguageFromFilePath(filePath),
            ApplyTarget = new ApplyTarget
            {
                Kind = hasSelection ? ApplyTargetKind.Selection : ApplyTargetKind.WholeDocument,
                FilePath = filePath,
                OriginalRange = applyRange
            }
        };
    }
}
