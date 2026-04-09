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
using OllamaPilot.Ollama.Models;
using OllamaPilot.Ollama.Services;

namespace OllamaPilot.Extensibility.Commands;

internal abstract class OllamaCommandBase : Command
{
    private readonly CodeContextService _codeContextService;
    private readonly IOllamaStreamingService _streamingService;
    private readonly OllamaPromptService _promptService;
    private readonly OllamaResponseParser _responseParser;
    private readonly OllamaResultStore _resultStore;
    private readonly OllamaResultWindowState _windowState;
    private readonly OllamaSettingsService _settingsService;
    private readonly OllamaDiagnosticsService _diagnosticsService;

    protected OllamaCommandBase(
        VisualStudioExtensibility extensibility,
        CodeContextService codeContextService,
        IOllamaStreamingService streamingService,
        OllamaPromptService promptService,
        OllamaResponseParser responseParser,
        OllamaResultStore resultStore,
        OllamaResultWindowState windowState,
        OllamaSettingsService settingsService,
        OllamaDiagnosticsService diagnosticsService)
        : base(extensibility)
    {
        _codeContextService = codeContextService;
        _streamingService = streamingService;
        _promptService = promptService;
        _responseParser = responseParser;
        _resultStore = resultStore;
        _windowState = windowState;
        _settingsService = settingsService;
        _diagnosticsService = diagnosticsService;
    }

    protected abstract OllamaActionType ActionType { get; }
    protected virtual string ActionDisplayName => ActionType.ToString();
    protected virtual string NoCodeAvailableMessage => "No code is available. Select some code or open a file first.";
    protected virtual ResponseParsingMode ParsingMode => ResponseParsingMode.PreferCode;

    protected virtual async Task<CodeContext?> GetCodeContextAsync(IClientContext context, CancellationToken cancellationToken)
        => await _codeContextService.GetCurrentContextAsync(context, cancellationToken);

    protected virtual string BuildPrompt(CodeContext codeContext) => _promptService.BuildActionPrompt(ActionType, codeContext);

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        CodeContext? codeContext = await GetCodeContextAsync(context, cancellationToken);
        if (codeContext is null || string.IsNullOrWhiteSpace(codeContext.EffectiveCode))
        {
            await this.Extensibility.Shell().ShowPromptAsync(NoCodeAvailableMessage, PromptOptions.OK, cancellationToken);
            return;
        }

        await this.Extensibility.Shell().ShowToolWindowAsync<OllamaResultToolWindow>(activate: true, cancellationToken);
        _windowState.BeginStreamingRequest(ActionDisplayName, codeContext);

        var builder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        string prompt = BuildPrompt(codeContext);
        string? error = null;
        OllamaClientSettings settings = _settingsService.Settings;

        _diagnosticsService.LogInfo("command.request", new Dictionary<string, object?>
        {
            ["action"] = ActionDisplayName,
            ["model"] = settings.SelectedModel,
            ["filePath"] = codeContext.FilePath,
            ["language"] = codeContext.Language,
            ["selectedChars"] = codeContext.SelectedText.Length,
            ["documentChars"] = codeContext.DocumentText.Length,
            ["promptChars"] = prompt.Length,
            ["promptPreview"] = prompt
        });

        await foreach (OllamaStreamingUpdate update in _streamingService.StreamPromptAsync(prompt, useChatModel: false, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(update.ThinkingChunk))
            {
                thinkingBuilder.Append(update.ThinkingChunk);
                _windowState.Data.ThinkingText = thinkingBuilder.ToString();
            }

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

        var parsed = _responseParser.Parse(builder.ToString(), ActionDisplayName, codeContext.FilePath, codeContext.ApplyTarget, ParsingMode);
        _diagnosticsService.LogInfo("command.response", new Dictionary<string, object?>
        {
            ["action"] = ActionDisplayName,
            ["model"] = settings.SelectedModel,
            ["responseChars"] = builder.Length,
            ["thinkingChars"] = thinkingBuilder.Length,
            ["hasCodeBlock"] = parsed.HasCodeBlock,
            ["isApplyReady"] = parsed.IsApplyReady,
            ["language"] = parsed.Language,
            ["responsePreview"] = builder.ToString(),
            ["codePreview"] = parsed.CodeBlock
        });
        _resultStore.SetResult(parsed);
        _windowState.CompleteStreamingRequest(parsed);
        if (ParsingMode == ResponseParsingMode.CodeRequired && !parsed.IsApplyReady)
        {
            _windowState.Data.Status = $"Model did not return apply-ready code. See log: {_diagnosticsService.LogFilePath}";
        }
    }

    protected int GetPromptContextWindowChars()
    {
        int chatCtx = Math.Max(1024, _settingsService.Settings.ChatContextWindow);
        return chatCtx * 4;
    }

    protected Task<CodeContext?> GetCurrentContextAsync(IClientContext context, bool includeFullDocumentText, CancellationToken cancellationToken)
        => _codeContextService.GetCurrentContextAsync(context, includeFullDocumentText, cancellationToken);
}
