using System.Threading.Channels;
using OllamaPilot.Core.Models;
using OllamaPilot.Ollama.Models;

namespace OllamaPilot.Ollama.Services;

public sealed class OllamaStreamingService : IOllamaStreamingService
{
    private readonly IOllamaService _ollamaService;
    private readonly OllamaSettingsService _settingsService;
    private readonly OllamaDiagnosticsService _diagnosticsService;

    public OllamaStreamingService(
        IOllamaService ollamaService,
        OllamaSettingsService settingsService,
        OllamaDiagnosticsService diagnosticsService)
    {
        _ollamaService = ollamaService;
        _settingsService = settingsService;
        _diagnosticsService = diagnosticsService;
    }

    public async IAsyncEnumerable<OllamaStreamingUpdate> StreamPromptAsync(
        string prompt,
        bool useChatModel,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        OllamaClientSettings settings = _settingsService.Settings;
        string selectedModel = useChatModel ? settings.SelectedChatModel : settings.SelectedModel;
        ThinkingDepth thinkingDepth = useChatModel ? settings.ChatThinkingDepth : ThinkingDepth.Off;
        var requestOptions = new RequestOptions
        {
            NumCtx = settings.ChatContextWindow,
            NumPredict = settings.MaxOutputTokens,
            Temperature = useChatModel ? settings.ChatTemperature : settings.DefaultTemperature
        };

        _diagnosticsService.LogInfo("stream.start", new Dictionary<string, object?>
        {
            ["useChatModel"] = useChatModel,
            ["model"] = selectedModel,
            ["thinkingDepth"] = thinkingDepth,
            ["temperature"] = requestOptions.Temperature,
            ["numCtx"] = requestOptions.NumCtx,
            ["numPredict"] = requestOptions.NumPredict,
            ["promptChars"] = prompt.Length,
            ["promptPreview"] = prompt
        });

        Channel<OllamaStreamingUpdate> channel = Channel.CreateUnbounded<OllamaStreamingUpdate>();
        int contentChunks = 0;
        int thinkingChunks = 0;
        int contentChars = 0;
        int thinkingChars = 0;
        Chat chat = _ollamaService.CreateChatSession(
            settings.BaseUrl,
            selectedModel,
            settings.AccessToken,
            requestOptions,
            thinkingDepth,
            response =>
            {
                if (!string.IsNullOrWhiteSpace(response.Thinking))
                {
                    thinkingChunks++;
                    thinkingChars += response.Thinking.Length;
                    if (thinkingChunks <= 3 || thinkingChunks % 25 == 0)
                    {
                        _diagnosticsService.LogInfo("stream.thinking", new Dictionary<string, object?>
                        {
                            ["model"] = selectedModel,
                            ["chunkIndex"] = thinkingChunks,
                            ["chunkChars"] = response.Thinking.Length,
                            ["thinkingCharsTotal"] = thinkingChars,
                            ["thinkingPreview"] = response.Thinking
                        });
                    }

                    channel.Writer.TryWrite(new OllamaStreamingUpdate { ThinkingChunk = response.Thinking });
                }

                if (!string.IsNullOrWhiteSpace(response.Content))
                {
                    contentChunks++;
                    contentChars += response.Content.Length;
                    if (contentChunks <= 3 || contentChunks % 25 == 0)
                    {
                        _diagnosticsService.LogInfo("stream.content", new Dictionary<string, object?>
                        {
                            ["model"] = selectedModel,
                            ["chunkIndex"] = contentChunks,
                            ["chunkChars"] = response.Content.Length,
                            ["contentCharsTotal"] = contentChars,
                            ["contentPreview"] = response.Content
                        });
                    }

                    channel.Writer.TryWrite(new OllamaStreamingUpdate { TextChunk = response.Content });
                }

                if (response.Done)
                {
                    _diagnosticsService.LogInfo("stream.completed", new Dictionary<string, object?>
                    {
                        ["model"] = selectedModel,
                        ["thinkingChunks"] = thinkingChunks,
                        ["contentChunks"] = contentChunks,
                        ["thinkingCharsTotal"] = thinkingChars,
                        ["contentCharsTotal"] = contentChars
                    });
                    channel.Writer.TryWrite(new OllamaStreamingUpdate { IsCompleted = true });
                    channel.Writer.TryComplete();
                }
            });

        _ = Task.Run(async () =>
        {
            try
            {
                await chat.SendAsync(prompt, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                _diagnosticsService.LogError("stream.failed", ex, new Dictionary<string, object?>
                {
                    ["useChatModel"] = useChatModel,
                    ["model"] = selectedModel,
                    ["promptChars"] = prompt.Length
                });
                channel.Writer.TryWrite(new OllamaStreamingUpdate { IsCompleted = true, ErrorMessage = ex.Message });
                channel.Writer.TryComplete(ex);
            }
        }, cancellationToken);

        await foreach (OllamaStreamingUpdate update in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return update;
        }
    }
}
