using System.Text;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;
using OllamaPilot.Core.Models;
using OllamaPilot.Core.Services;
using OllamaPilot.Extensibility.Models;
using OllamaPilot.Extensibility.Services;
using OllamaPilot.Ollama.Services;

namespace OllamaPilot.Extensibility.Windows;

internal sealed class OllamaResultWindowState
{
    public OllamaResultWindowData Data { get; }

    private readonly CodeContextService _codeContextService;
    private readonly IOllamaChatService _chatService;
    private readonly OllamaResponseParser _responseParser;
    private readonly OllamaResultStore _resultStore;
    private readonly EditorApplyService _editorApplyService;
    private readonly OllamaDiagnosticsService _diagnosticsService;
    private readonly List<OllamaChatTurn> _turns = [];
    private CancellationTokenSource? _activeStreamingCts;
    private const int MaxTurnsToSend = 6;

    public OllamaResultWindowState(
        CodeContextService codeContextService,
        IOllamaChatService chatService,
        OllamaResponseParser responseParser,
        OllamaResultStore resultStore,
        EditorApplyService editorApplyService,
        OllamaSettingsService settingsService,
        OllamaDiagnosticsService diagnosticsService)
    {
        _codeContextService = codeContextService;
        _chatService = chatService;
        _responseParser = responseParser;
        _resultStore = resultStore;
        _editorApplyService = editorApplyService;
        _diagnosticsService = diagnosticsService;

        Data = new OllamaResultWindowData
        {
            IncludeSelection = settingsService.Settings.IncludeSelectionByDefault,
            SendCommand = new AsyncCommand(SendAsync) { CanExecute = true },
            StopCommand = new AsyncCommand(StopAsync) { CanExecute = false },
            ClearCommand = new AsyncCommand(ClearAsync) { CanExecute = true },
            CopyCodeCommand = new AsyncCommand(CopyCodeAsync) { CanExecute = false },
            CopyResponseCommand = new AsyncCommand(CopyResponseAsync) { CanExecute = false },
            ApplyCodeCommand = new AsyncCommand(ApplyCodeAsync) { CanExecute = false }
        };

        Data.PropertyChanged += (_, _) => RefreshCommandStates();
        RefreshCommandStates();
    }

    public void BeginStreamingRequest(string actionName, CodeContext? context)
    {
        Data.ActionName = actionName;
        Data.SourceFilePath = context?.FilePath ?? string.Empty;
        Data.Status = "Thinking...";
        Data.IsStreaming = true;
        Data.StreamingText = string.Empty;
        Data.ThinkingText = string.Empty;
        Data.Explanation = string.Empty;
        Data.CodeBlock = string.Empty;
        Data.Language = string.Empty;
        Data.FullText = string.Empty;
        RefreshCommandStates();
    }

    public void CompleteStreamingRequest(OllamaParsedResponse parsed)
    {
        _resultStore.SetResult(parsed);
        string formattedExplanation = ResponseTextFormatter.FormatForDisplay(parsed.Explanation);
        string formattedFullText = ResponseTextFormatter.FormatForDisplay(parsed.FullText);
        Data.Status = "Done";
        Data.IsStreaming = false;
        Data.StreamingText = parsed.HasCodeBlock && !string.IsNullOrWhiteSpace(formattedExplanation) ? formattedExplanation : formattedFullText;
        Data.Explanation = formattedExplanation;
        Data.CodeBlock = parsed.CodeBlock;
        Data.Language = parsed.Language ?? string.Empty;
        Data.FullText = formattedFullText;
        RefreshCommandStates();
    }

    public void FailStreamingRequest(string message)
    {
        Data.Status = "Error";
        Data.IsStreaming = false;
        Data.StreamingText = message;
        Data.FullText = message;
        RefreshCommandStates();
    }

    private List<OllamaChatTurn> GetRecentTurns() =>
        _turns.Count <= MaxTurnsToSend ? _turns : _turns.Skip(_turns.Count - MaxTurnsToSend).ToList();

    private void AddTurn(string role, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _turns.Add(new OllamaChatTurn { Role = role, Text = text, Timestamp = DateTimeOffset.UtcNow });
        var builder = new StringBuilder();
        foreach (OllamaChatTurn turn in _turns)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine().AppendLine();
            }

            builder.AppendLine($"{turn.Role}:");
            builder.AppendLine(turn.Text);
        }

        Data.ConversationText = builder.ToString();
    }

    private async Task SendAsync(object? parameter, IClientContext clientContext, CancellationToken cancellationToken)
    {
        string userPrompt = Data.UserPrompt?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            Data.Status = "Enter a prompt first.";
            RefreshCommandStates();
            return;
        }

        CodeContext? codeContext = null;
        if (Data.IncludeSelection)
        {
            codeContext = await _codeContextService.GetCurrentContextAsync(clientContext, cancellationToken);
            if (codeContext is not null && string.IsNullOrWhiteSpace(codeContext.EffectiveCode))
            {
                codeContext = null;
            }
        }

        _activeStreamingCts?.Dispose();
        _activeStreamingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        BeginStreamingRequest("Chat", codeContext);
        AddTurn("User", userPrompt);

        var contentBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        string? error = null;

        try
        {
            _diagnosticsService.LogInfo("chat.request", new Dictionary<string, object?>
            {
                ["turnCount"] = _turns.Count,
                ["includeSelection"] = Data.IncludeSelection,
                ["filePath"] = codeContext?.FilePath,
                ["language"] = codeContext?.Language,
                ["promptChars"] = userPrompt.Length,
                ["promptPreview"] = userPrompt
            });

            await foreach (OllamaStreamingUpdate update in _chatService.StreamChatAsync(userPrompt, GetRecentTurns(), codeContext, _activeStreamingCts.Token))
            {
                if (!string.IsNullOrWhiteSpace(update.ThinkingChunk))
                {
                    thinkingBuilder.Append(update.ThinkingChunk);
                    Data.ThinkingText = thinkingBuilder.ToString();
                }

                if (!string.IsNullOrWhiteSpace(update.TextChunk))
                {
                    contentBuilder.Append(update.TextChunk);
                    Data.StreamingText = contentBuilder.ToString();
                    Data.Status = "Streaming...";
                }

                if (update.IsCompleted)
                {
                    error = update.ErrorMessage;
                }
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                FailStreamingRequest(error);
                return;
            }

            string fullText = contentBuilder.ToString();
            var parsed = _responseParser.Parse(fullText, "Chat", codeContext?.FilePath, codeContext?.ApplyTarget, ResponseParsingMode.PreferCode);
            _diagnosticsService.LogInfo("chat.response", new Dictionary<string, object?>
            {
                ["responseChars"] = fullText.Length,
                ["thinkingChars"] = thinkingBuilder.Length,
                ["hasCodeBlock"] = parsed.HasCodeBlock,
                ["isApplyReady"] = parsed.IsApplyReady,
                ["language"] = parsed.Language,
                ["responsePreview"] = fullText,
                ["codePreview"] = parsed.CodeBlock
            });
            CompleteStreamingRequest(parsed);
            AddTurn("Assistant", !string.IsNullOrWhiteSpace(parsed.Explanation) ? parsed.Explanation : parsed.FullText);
            Data.UserPrompt = string.Empty;
        }
        catch (OperationCanceledException)
        {
            Data.Status = "Cancelled";
            Data.IsStreaming = false;
        }
        catch (Exception ex)
        {
            FailStreamingRequest(ex.Message);
        }
        finally
        {
            _activeStreamingCts?.Dispose();
            _activeStreamingCts = null;
            RefreshCommandStates();
        }
    }

    private async Task StopAsync(object? parameter, IClientContext clientContext, CancellationToken cancellationToken)
    {
        if (_activeStreamingCts is not null)
        {
            await _activeStreamingCts.CancelAsync();
        }
    }

    private Task ClearAsync(object? parameter, IClientContext clientContext, CancellationToken cancellationToken)
    {
        Data.Status = "Ready";
        Data.StreamingText = string.Empty;
        Data.ThinkingText = string.Empty;
        Data.Explanation = string.Empty;
        Data.CodeBlock = string.Empty;
        Data.FullText = string.Empty;
        Data.ConversationText = string.Empty;
        _turns.Clear();
        RefreshCommandStates();
        return Task.CompletedTask;
    }

    private Task CopyCodeAsync(object? parameter, IClientContext clientContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Data.CodeBlock))
        {
            Data.Status = "No code to copy.";
            RefreshCommandStates();
            return Task.CompletedTask;
        }

        return CopyTextToClipboardAsync(Data.CodeBlock, "Code copied.");
    }

    private Task CopyResponseAsync(object? parameter, IClientContext clientContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Data.FullText))
        {
            Data.Status = "No response to copy.";
            RefreshCommandStates();
            return Task.CompletedTask;
        }

        return CopyTextToClipboardAsync(Data.FullText, "Response copied.");
    }

    private async Task ApplyCodeAsync(object? parameter, IClientContext clientContext, CancellationToken cancellationToken)
    {
        var applyTarget = _resultStore.Current.ApplyTarget as ApplyTarget;
        ApplyCodeResult result = await _editorApplyService.ReplaceGeneratedCodeAsync(clientContext, applyTarget, Data.CodeBlock, cancellationToken);
        Data.Status = result.Message;
        RefreshCommandStates();
    }

    private void RefreshCommandStates()
    {
        bool hasCode = !string.IsNullOrWhiteSpace(Data.CodeBlock);
        bool hasResponse = !string.IsNullOrWhiteSpace(Data.FullText);
        bool isStreaming = Data.IsStreaming;
        bool canApply = !isStreaming && hasCode && _resultStore.Current.IsApplyReady && _resultStore.Current.ApplyTarget is ApplyTarget;

        if (Data.SendCommand is not null) Data.SendCommand.CanExecute = !isStreaming;
        if (Data.StopCommand is not null) Data.StopCommand.CanExecute = isStreaming;
        if (Data.ClearCommand is not null) Data.ClearCommand.CanExecute = !isStreaming || hasResponse;
        if (Data.CopyCodeCommand is not null) Data.CopyCodeCommand.CanExecute = !isStreaming && hasCode;
        if (Data.CopyResponseCommand is not null) Data.CopyResponseCommand.CanExecute = hasResponse;
        if (Data.ApplyCodeCommand is not null) Data.ApplyCodeCommand.CanExecute = canApply;
    }

    private Task CopyTextToClipboardAsync(string text, string successStatus)
    {
        try
        {
            var thread = new Thread(() => Clipboard.SetText(text));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Data.Status = successStatus;
        }
        catch
        {
            Data.Status = "Clipboard copy failed.";
        }

        RefreshCommandStates();
        return Task.CompletedTask;
    }
}
