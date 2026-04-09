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
internal sealed class ExplainCodeCommand : OllamaCommandBase
{
    public ExplainCodeCommand(VisualStudioExtensibility extensibility, CodeContextService codeContextService, IOllamaStreamingService streamingService, OllamaPromptService promptService, OllamaResponseParser responseParser, OllamaResultStore resultStore, OllamaResultWindowState windowState, OllamaSettingsService settingsService, OllamaDiagnosticsService diagnosticsService)
        : base(extensibility, codeContextService, streamingService, promptService, responseParser, resultStore, windowState, settingsService, diagnosticsService) { }

    protected override OllamaActionType ActionType => OllamaActionType.ExplainCode;
    protected override ResponseParsingMode ParsingMode => ResponseParsingMode.TextOnly;

    public override CommandConfiguration CommandConfiguration => new("Explain Selected Code")
    {
        Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.Code, IconSettings.IconAndText)
    };
}
