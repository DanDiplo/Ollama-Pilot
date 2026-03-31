using OllamaPilot.UI.Settings;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OllamaPilot.Services.Ollama
{
    public sealed class OllamaSharpService : IOllamaService
    {
        public Chat CreateChatSession(string baseUrl, string model, string accessToken, RequestOptions options, ThinkingDepth thinkingDepth, Action<ChatResponseStream> streamer)
        {
            var session = new OllamaSharpChatSession(baseUrl, model, accessToken, options, thinkingDepth, streamer);
            return new Chat(session);
        }

        /// <summary>
        /// Retrieves a list of local models from the specified service.
        /// </summary>
        /// <param name="baseUrl">The base URL of the service.</param>
        /// <param name="accessToken">The access token for authentication.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async task containing the list of local models.</returns>
        public async Task<IReadOnlyList<Model>> ListLocalModelsAsync(string baseUrl, string accessToken, CancellationToken cancellationToken)
        {
            using (var client = CreateClient(baseUrl, accessToken, null))
            {
                var models = await client.ListLocalModelsAsync(cancellationToken);
                return models.Select(m => new Model { Name = m.Name }).ToList();
            }
        }

        public async Task<ShowModelResponse> ShowModelInformationAsync(string baseUrl, string accessToken, string model, CancellationToken cancellationToken)
        {
            using (var client = CreateClient(baseUrl, accessToken, model))
            {
                var response = await client.ShowModelAsync(new OllamaSharp.Models.ShowModelRequest { Model = model }, cancellationToken);
                return new ShowModelResponse
                {
                    Parameters = response.Parameters,
                    Template = response.Template
                };
            }
        }

        /// <summary>
        /// Asynchronously generates a completion by streaming responses from the Ollama API.
        /// </summary>
        /// <param name="baseUrl">The base URL of the Ollama server.</param>
        /// <param name="accessToken">The access token for authentication.</param>
        /// <param name="request">The request containing model, prompt, and options.</param>
        /// <param name="cancellationToken">A token to monitor cancellation.</param>
        /// <returns>A task representing the asynchronous operation, containing the generated completion result.</returns>
        public async Task<GenerateCompletionResult> GenerateCompletionAsync(string baseUrl, string accessToken, GenerateCompletionRequest request, CancellationToken cancellationToken)
        {
            using (var client = CreateClient(baseUrl, accessToken, request?.Model))
            {
                var generateRequest = new OllamaSharp.Models.GenerateRequest
                {
                    Model = request != null ? request.Model : null,
                    Prompt = request != null ? request.Prompt : null,
                    Suffix = request != null ? request.Suffix : null,
                    Options = ToPackageRequestOptions(request != null ? request.Options : null),
                    Raw = request != null ? (bool?)request.Raw : null,
                    Stream = true
                };

                var builder = new System.Text.StringBuilder();
                var enumerator = client.GenerateAsync(generateRequest, cancellationToken).GetAsyncEnumerator(cancellationToken);
                try
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        var item = enumerator.Current;
                        if (item != null && item.Response != null)
                        {
                            builder.Append(item.Response);
                        }
                    }
                }
                finally
                {
                    await enumerator.DisposeAsync();
                }

                return new GenerateCompletionResult { Response = builder.ToString() };
            }
        }

        /// <summary>
        /// Creates and configures a new Ollama API client instance with the provided settings.
        /// </summary>
        /// <param name="baseUrl">The base URL for the Ollama API server.</param>
        /// <param name="accessToken">The access token for authorization headers.</param>
        /// <param name="selectedModel">The model name to use for API requests.</param>
        /// <returns>A configured <see cref="OllamaApiClient"/> instance ready for use.</returns>
        private static OllamaApiClient CreateClient(string baseUrl, string accessToken, string selectedModel)
        {
            var client = new OllamaApiClient(baseUrl ?? string.Empty, selectedModel ?? string.Empty);
            ApplyAuthorizationHeader(client, accessToken);
            if (!string.IsNullOrWhiteSpace(selectedModel))
            {
                client.SelectedModel = selectedModel;
            }

            return client;
        }

        private static void ApplyAuthorizationHeader(OllamaApiClient client, string accessToken)
        {
            if (client == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                client.DefaultRequestHeaders.Remove("Authorization");
                return;
            }

            client.DefaultRequestHeaders["Authorization"] = "Bearer " + accessToken.Trim();
        }

        private static OllamaSharp.Models.RequestOptions ToPackageRequestOptions(RequestOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return new OllamaSharp.Models.RequestOptions
            {
                NumCtx = options.NumCtx,
                NumPredict = options.NumPredict,
                Temperature = options.Temperature,
                Stop = options.Stop
            };
        }

        private static ChatRole ToLocalRole(OllamaSharp.Models.Chat.ChatRole role)
        {
            return new ChatRole(role.ToString());
        }

        private sealed class OllamaSharpChatSession : IChatSession
        {
            private readonly OllamaApiClient _client;
            private readonly OllamaSharp.Chat _chat;
            private readonly Action<ChatResponseStream> _streamer;
            private readonly ThinkingDepth _thinkingDepth;

            public OllamaSharpChatSession(string baseUrl, string model, string accessToken, RequestOptions options, ThinkingDepth thinkingDepth, Action<ChatResponseStream> streamer)
            {
                _client = CreateClient(baseUrl, accessToken, model);
                _chat = new OllamaSharp.Chat(_client);
                _streamer = streamer;
                _thinkingDepth = thinkingDepth;
                SelectedModel = model;
                Options = options;
                AccessToken = accessToken;
                _chat.Think = ToPackageThinkingDepth(_thinkingDepth);
                _chat.OnThink += Chat_OnThink;
            }

            public string SelectedModel
            {
                get { return _chat.Model; }
                set
                {
                    _chat.Model = value;
                    _client.SelectedModel = value;
                }
            }

            public RequestOptions Options
            {
                get { return FromPackageRequestOptions(_chat.Options); }
                set { _chat.Options = ToPackageRequestOptions(value); }
            }

            public string AccessToken { get; set; }

            public async Task SendAsync(string message, CancellationToken cancellationToken)
            {
                ApplyAuthorizationHeader(_client, AccessToken);
                _client.SelectedModel = SelectedModel;
                _chat.Model = SelectedModel;
                _chat.Think = ToPackageThinkingDepth(_thinkingDepth);

                var enumerator = _chat.SendAsync(message, cancellationToken).GetAsyncEnumerator(cancellationToken);
                try
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        var token = enumerator.Current;
                        if (_streamer != null)
                        {
                            _streamer(new ChatResponseStream
                            {
                                Model = SelectedModel,
                                Message = new Message(ChatRole.Assistant, token),
                                Done = false
                            });
                        }
                    }
                }
                finally
                {
                    await enumerator.DisposeAsync();
                }

                if (_streamer != null)
                {
                    _streamer(new ChatResponseStream
                    {
                        Model = SelectedModel,
                        Message = new Message(ChatRole.Assistant, string.Empty),
                        Done = true
                    });
                }
            }

            private void Chat_OnThink(object sender, string thinkingToken)
            {
                if (_streamer == null || string.IsNullOrEmpty(thinkingToken))
                {
                    return;
                }

                _streamer(new ChatResponseStream
                {
                    Model = SelectedModel,
                    Message = new Message
                    {
                        Role = ChatRole.Assistant,
                        Thinking = thinkingToken
                    },
                    Done = false
                });
            }

            private static RequestOptions FromPackageRequestOptions(OllamaSharp.Models.RequestOptions options)
            {
                if (options == null)
                {
                    return null;
                }

                return new RequestOptions
                {
                    NumCtx = options.NumCtx,
                    NumPredict = options.NumPredict,
                    Temperature = options.Temperature,
                    Stop = options.Stop
                };
            }
        }

        private static OllamaSharp.Models.Chat.ThinkValue? ToPackageThinkingDepth(ThinkingDepth thinkingDepth)
        {
            switch (thinkingDepth)
            {
                case ThinkingDepth.Low:
                    return "low";
                case ThinkingDepth.Medium:
                    return "medium";
                case ThinkingDepth.High:
                    return "high";
                default:
                    return false;
            }
        }
    }
}
