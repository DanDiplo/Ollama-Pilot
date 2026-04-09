using OllamaPilot.Core.Models;
using OllamaPilot.Core.Services;

namespace OllamaPilot.Ollama.Services;

public sealed class OllamaChatService : IOllamaChatService
{
    private readonly IOllamaStreamingService _streamingService;
    private readonly OllamaPromptService _promptService;

    public OllamaChatService(IOllamaStreamingService streamingService, OllamaPromptService promptService)
    {
        _streamingService = streamingService;
        _promptService = promptService;
    }

    public IAsyncEnumerable<OllamaStreamingUpdate> StreamChatAsync(
        string userPrompt,
        IReadOnlyList<OllamaChatTurn> turns,
        CodeContext? codeContext,
        CancellationToken cancellationToken)
    {
        string prompt = _promptService.BuildChatSystemPrompt(userPrompt, turns, codeContext);
        return _streamingService.StreamPromptAsync(prompt, useChatModel: true, cancellationToken);
    }
}
