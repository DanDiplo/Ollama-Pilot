using OllamaPilot.Core.Models;

namespace OllamaPilot.Ollama.Services;

public interface IOllamaChatService
{
    IAsyncEnumerable<OllamaStreamingUpdate> StreamChatAsync(
        string userPrompt,
        IReadOnlyList<OllamaChatTurn> turns,
        CodeContext? codeContext,
        CancellationToken cancellationToken);
}
