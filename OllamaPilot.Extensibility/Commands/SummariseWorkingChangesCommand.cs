using System.Text;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using OllamaPilot.Core.Models;
using OllamaPilot.Core.Services;
using OllamaPilot.Extensibility.Models;
using OllamaPilot.Extensibility.Services;
using OllamaPilot.Extensibility.ToolWindows;
using OllamaPilot.Extensibility.Windows;
using OllamaPilot.Ollama.Services;

namespace OllamaPilot.Extensibility.Commands;

[VisualStudioContribution]
internal sealed class SummariseWorkingChangesCommand : Command
{
    private readonly CodeContextService _codeContextService;
    private readonly IOllamaStreamingService _streamingService;
    private readonly OllamaPromptService _promptService;
    private readonly OllamaResponseParser _responseParser;
    private readonly OllamaResultStore _resultStore;
    private readonly OllamaResultWindowState _windowState;
    private readonly OllamaDiagnosticsService _diagnosticsService;

    public SummariseWorkingChangesCommand(VisualStudioExtensibility extensibility, CodeContextService codeContextService, IOllamaStreamingService streamingService, OllamaPromptService promptService, OllamaResponseParser responseParser, OllamaResultStore resultStore, OllamaResultWindowState windowState, OllamaDiagnosticsService diagnosticsService)
        : base(extensibility)
    {
        _codeContextService = codeContextService;
        _streamingService = streamingService;
        _promptService = promptService;
        _responseParser = responseParser;
        _resultStore = resultStore;
        _windowState = windowState;
        _diagnosticsService = diagnosticsService;
    }

    public override CommandConfiguration CommandConfiguration => new("Summarise Working Changes")
    {
        Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.History, IconSettings.IconAndText)
    };

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        CodeContext? current = await _codeContextService.GetCurrentContextAsync(context, cancellationToken);
        GitChangesContext? changes = await GitService.TryGetChangesContextAsync(current?.FilePath, cancellationToken);
        if (changes is null)
        {
            await this.Extensibility.Shell().ShowPromptAsync("No Git changes were found from the active document context.", PromptOptions.OK, cancellationToken);
            return;
        }

        await this.Extensibility.Shell().ShowToolWindowAsync<OllamaResultToolWindow>(activate: true, cancellationToken);
        _windowState.BeginStreamingRequest("Summarise Working Changes", null);

        string prompt = _promptService.BuildSummarizeChangesPrompt(changes);
        var builder = new StringBuilder();
        string? error = null;

        _diagnosticsService.LogInfo("summary.request", new Dictionary<string, object?>
        {
            ["repositoryRoot"] = changes.RepositoryRoot,
            ["statusChars"] = changes.StatusText.Length,
            ["diffChars"] = changes.DiffText.Length,
            ["promptChars"] = prompt.Length,
            ["promptPreview"] = prompt
        });

        await foreach (OllamaStreamingUpdate update in _streamingService.StreamPromptAsync(prompt, useChatModel: false, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(update.TextChunk))
            {
                builder.Append(update.TextChunk);
                _windowState.Data.StreamingText = builder.ToString();
                _windowState.Data.Status = "Streaming...";
            }

            if (update.IsCompleted)
            {
                error = update.ErrorMessage;
            }
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            _windowState.FailStreamingRequest(error);
            return;
        }

        var parsed = _responseParser.Parse(builder.ToString(), "Summarise Working Changes", changes.RepositoryRoot, parsingMode: ResponseParsingMode.TextOnly);
        _diagnosticsService.LogInfo("summary.response", new Dictionary<string, object?>
        {
            ["responseChars"] = builder.Length,
            ["hasCodeBlock"] = parsed.HasCodeBlock,
            ["responsePreview"] = builder.ToString()
        });
        _resultStore.SetResult(parsed);
        _windowState.CompleteStreamingRequest(parsed);
    }
}
