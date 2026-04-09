using OllamaPilot.Ollama.Models;
using OllamaSharp;

namespace OllamaPilot.Ollama.Services;

public sealed class OllamaSharpService : IOllamaService
{
    public async Task<IReadOnlyList<Model>> ListLocalModelsAsync(string baseUrl, string? accessToken, CancellationToken cancellationToken)
    {
        using var client = CreateClient(baseUrl, accessToken, null);
        var models = await client.ListLocalModelsAsync(cancellationToken);
        return models.Select(model => new Model { Name = model.Name }).ToArray();
    }

    public Chat CreateChatSession(string baseUrl, string model, string? accessToken, RequestOptions options, ThinkingDepth thinkingDepth, Action<ChatResponseStream> streamer)
    {
        var session = new OllamaSharpChatSession(baseUrl, model, accessToken, options, thinkingDepth, streamer);
        return new Chat(session);
    }

    private static OllamaApiClient CreateClient(string baseUrl, string? accessToken, string? selectedModel)
    {
        var client = new OllamaApiClient(baseUrl ?? string.Empty, selectedModel ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            client.DefaultRequestHeaders["Authorization"] = "Bearer " + accessToken.Trim();
        }

        if (!string.IsNullOrWhiteSpace(selectedModel))
        {
            client.SelectedModel = selectedModel;
        }

        return client;
    }

    private sealed class OllamaSharpChatSession : IChatSession
    {
        private readonly OllamaApiClient _client;
        private readonly OllamaSharp.Chat _chat;
        private readonly Action<ChatResponseStream> _streamer;
        private readonly string _selectedModel;
        private readonly string? _accessToken;
        private readonly ThinkingDepth _thinkingDepth;

        public OllamaSharpChatSession(string baseUrl, string model, string? accessToken, RequestOptions options, ThinkingDepth thinkingDepth, Action<ChatResponseStream> streamer)
        {
            _client = CreateClient(baseUrl, accessToken, model);
            _chat = new OllamaSharp.Chat(_client)
            {
                Model = model,
                Options = new OllamaSharp.Models.RequestOptions
                {
                    NumCtx = options.NumCtx,
                    NumPredict = options.NumPredict,
                    Temperature = options.Temperature
                }
            };
            _streamer = streamer;
            _selectedModel = model;
            _accessToken = accessToken;
            _thinkingDepth = thinkingDepth;
            _chat.Think = ToThinkValue(thinkingDepth);
            _chat.OnThink += OnThink;
        }

        public async Task SendAsync(string message, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_accessToken))
            {
                _client.DefaultRequestHeaders["Authorization"] = "Bearer " + _accessToken.Trim();
            }

            _client.SelectedModel = _selectedModel;
            _chat.Model = _selectedModel;
            _chat.Think = ToThinkValue(_thinkingDepth);

            var enumerator = _chat.SendAsync(message, cancellationToken).GetAsyncEnumerator(cancellationToken);
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    _streamer(new ChatResponseStream
                    {
                        Model = _selectedModel,
                        Content = enumerator.Current ?? string.Empty,
                        Done = false
                    });
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            _streamer(new ChatResponseStream
            {
                Model = _selectedModel,
                Done = true
            });
        }

        private void OnThink(object? sender, string thinkingToken)
        {
            if (string.IsNullOrWhiteSpace(thinkingToken))
            {
                return;
            }

            _streamer(new ChatResponseStream
            {
                Model = _selectedModel,
                Thinking = thinkingToken,
                Done = false
            });
        }

        private static OllamaSharp.Models.Chat.ThinkValue? ToThinkValue(ThinkingDepth thinkingDepth) =>
            thinkingDepth switch
            {
                ThinkingDepth.Off => false,
                ThinkingDepth.Low => "low",
                ThinkingDepth.Medium => "medium",
                ThinkingDepth.High => "high",
                _ => false
            };
    }
}
