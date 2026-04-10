using OllamaPilot.Ollama.Models;
using OllamaPilot.Core.Models;

namespace OllamaPilot.Ollama.Services;

public interface IOllamaStreamingService
{
    IAsyncEnumerable<OllamaStreamingUpdate> StreamPromptAsync(string prompt, bool useChatModel, CancellationToken cancellationToken, ThinkingDepth? thinkingDepthOverride = null);
    IAsyncEnumerable<OllamaStreamingUpdate> StreamStructuredChatAsync(string systemPrompt, string userPrompt, bool useChatModel, CancellationToken cancellationToken, ThinkingDepth? thinkingDepthOverride = null);
}
