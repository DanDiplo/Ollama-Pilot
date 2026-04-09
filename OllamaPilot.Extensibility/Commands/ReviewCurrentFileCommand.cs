using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using OllamaPilot.Core.Models;
using OllamaPilot.Core.Services;
using OllamaPilot.Extensibility.Models;
using OllamaPilot.Extensibility.Services;
using OllamaPilot.Extensibility.Windows;
using OllamaPilot.Ollama.Services;

namespace OllamaPilot.Extensibility.Commands;

[VisualStudioContribution]
internal sealed class ReviewCurrentFileCommand : OllamaCommandBase
{
    public ReviewCurrentFileCommand(VisualStudioExtensibility extensibility, CodeContextService codeContextService, IOllamaStreamingService streamingService, OllamaPromptService promptService, OllamaResponseParser responseParser, OllamaResultStore resultStore, OllamaResultWindowState windowState, OllamaSettingsService settingsService, OllamaDiagnosticsService diagnosticsService)
        : base(extensibility, codeContextService, streamingService, promptService, responseParser, resultStore, windowState, settingsService, diagnosticsService) { }

    protected override OllamaActionType ActionType => OllamaActionType.ReviewCurrentFile;
    protected override ResponseParsingMode ParsingMode => ResponseParsingMode.TextOnly;

    public override CommandConfiguration CommandConfiguration => new("Review Current File")
    {
        Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.StatusInformation, IconSettings.IconAndText)
    };

    protected override async Task<CodeContext?> GetCodeContextAsync(IClientContext context, CancellationToken cancellationToken)
    {
        CodeContext? current = await GetCurrentContextAsync(context, includeFullDocumentText: true, cancellationToken);
        if (current is null)
        {
            return null;
        }

        int maxChars = GetPromptContextWindowChars();
        return new CodeContext
        {
            FilePath = current.FilePath,
            Language = current.Language,
            DocumentText = OllamaPromptService.PrepareDocumentForPrompt(current.DocumentText, maxChars, reviewMode: true),
            ApplyTarget = current.ApplyTarget
        };
    }
}
