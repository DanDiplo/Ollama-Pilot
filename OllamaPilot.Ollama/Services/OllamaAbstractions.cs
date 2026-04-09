using OllamaPilot.Ollama.Models;

namespace OllamaPilot.Ollama.Services;

public sealed class RequestOptions
{
    public int? NumCtx { get; set; }
    public int? NumPredict { get; set; }
    public float? Temperature { get; set; }
}

public sealed class Model
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ChatResponseStream
{
    public string Model { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Thinking { get; set; } = string.Empty;
    public bool Done { get; set; }
}

public interface IOllamaService
{
    Task<IReadOnlyList<Model>> ListLocalModelsAsync(string baseUrl, string? accessToken, CancellationToken cancellationToken);
    Chat CreateChatSession(string baseUrl, string model, string? accessToken, RequestOptions options, ThinkingDepth thinkingDepth, Action<ChatResponseStream> streamer);
}

public sealed class Chat
{
    private readonly IChatSession _session;

    internal Chat(IChatSession session)
    {
        _session = session;
    }

    public Task SendAsync(string message, CancellationToken cancellationToken) => _session.SendAsync(message, cancellationToken);
}

internal interface IChatSession
{
    Task SendAsync(string message, CancellationToken cancellationToken);
}
