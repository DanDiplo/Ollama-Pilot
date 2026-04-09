using OllamaPilot.Core.Models;

namespace OllamaPilot.Ollama.Services;

public interface IOllamaStreamingService
{
    IAsyncEnumerable<OllamaStreamingUpdate> StreamPromptAsync(string prompt, bool useChatModel, CancellationToken cancellationToken);
}
