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
internal sealed class AddCommentsCommand : OllamaCommandBase
{
    public AddCommentsCommand(VisualStudioExtensibility extensibility, CodeContextService codeContextService, IOllamaStreamingService streamingService, OllamaPromptService promptService, OllamaResponseParser responseParser, OllamaResultStore resultStore, OllamaResultWindowState windowState, OllamaSettingsService settingsService, OllamaDiagnosticsService diagnosticsService)
        : base(extensibility, codeContextService, streamingService, promptService, responseParser, resultStore, windowState, settingsService, diagnosticsService) { }

    protected override OllamaActionType ActionType => OllamaActionType.AddComments;
    protected override ResponseParsingMode ParsingMode => ResponseParsingMode.CodeRequired;

    public override CommandConfiguration CommandConfiguration => new("Add Comments")
    {
        Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.AddComment, IconSettings.IconAndText)
    };
}
