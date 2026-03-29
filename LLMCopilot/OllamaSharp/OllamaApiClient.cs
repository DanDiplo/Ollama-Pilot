using OllamaSharp.Models;
using OllamaSharp.Streamer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using OllamaSharp.Models.Chat;
using System.Threading;
using System.Net.Http.Headers;

namespace OllamaSharp
{
    public class OllamaApiClient : IOllamaApiClient
    {
        public class Configuration
        {
            public Uri Uri { get; set; }
            public string Model { get; set; }
        }

        private readonly HttpClient _client;
        public Configuration Config { get; }
        public string SelectedModel { get; set; }

        public void SetAuthorizationHeader(string value)
        {
            if (string.IsNullOrEmpty(value)) 
                return;

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", value);
        }

        public OllamaApiClient(string uriString, string defaultModel = "")
            : this(new Uri(uriString), defaultModel)
        {
        }

        public OllamaApiClient(Uri uri, string defaultModel = "")
            : this(new Configuration { Uri = uri, Model = defaultModel })
        {
        }

        public OllamaApiClient(Configuration config)
            : this(new HttpClient() { BaseAddress = config.Uri }, config.Model)
        {
        }

        public OllamaApiClient(HttpClient client, string defaultModel = "")
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            SelectedModel = defaultModel;
        }

        public async Task CreateModelAsync(CreateModelRequest request, IResponseStreamer<CreateStatus> streamer, CancellationToken cancellationToken = default)
        {
            await StreamPostAsync("api/create", request, streamer, cancellationToken);
        }

        public async Task DeleteModelAsync(string model, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, "api/delete")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new DeleteModelRequest { Name = model }), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            finally
            {
                response.Dispose();
            }
        }

        public async Task<IEnumerable<Model>> ListLocalModelsAsync(CancellationToken cancellationToken = default)
        {
            var data = await GetAsync<ListModelsResponse>("api/tags", cancellationToken);
            return data.Models;
        }

        public async Task<ShowModelResponse> ShowModelInformationAsync(string model, CancellationToken cancellationToken = default)
        {
            return await PostAsync<ShowModelRequest, ShowModelResponse>("api/show", new ShowModelRequest { Name = model }, cancellationToken);
        }

        public async Task CopyModelAsync(CopyModelRequest request, CancellationToken cancellationToken = default)
        {
            await PostAsync("api/copy", request, cancellationToken);
        }

        public async Task PullModelAsync(PullModelRequest request, IResponseStreamer<PullStatus> streamer, CancellationToken cancellationToken = default)
        {
            await StreamPostAsync("api/pull", request, streamer, cancellationToken);
        }

        public async Task PushModelAsync(PushRequest request, IResponseStreamer<PushStatus> streamer, CancellationToken cancellationToken = default)
        {
            await StreamPostAsync("api/push", request, streamer, cancellationToken);
        }

        public async Task<GenerateEmbeddingResponse> GenerateEmbeddingsAsync(GenerateEmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            return await PostAsync<GenerateEmbeddingRequest, GenerateEmbeddingResponse>("api/embeddings", request, cancellationToken);
        }

        public async Task<ConversationContext> StreamCompletionAsync(GenerateCompletionRequest request, IResponseStreamer<GenerateCompletionResponseStream> streamer, CancellationToken cancellationToken = default)
        {
            return await GenerateCompletionAsync(request, streamer, cancellationToken);
        }

        public async Task<ConversationContextWithResponse> GetCompletionAsync(GenerateCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var builder = new StringBuilder();
            var result = await GenerateCompletionAsync(request, new ActionResponseStreamer<GenerateCompletionResponseStream>(status => builder.Append(status.Response)), cancellationToken);
            return new ConversationContextWithResponse(builder.ToString(), result.Context);
        }

        public async Task<IEnumerable<Message>> SendChatAsync(ChatRequest chatRequest, IResponseStreamer<ChatResponseStream> streamer, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "api/chat")
            {
                Content = new StringContent(JsonConvert.SerializeObject(chatRequest), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _client.SendAsync(request, chatRequest.Stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, cancellationToken);
            try
            {
                response.EnsureSuccessStatusCode();
                return await ProcessStreamedChatResponseAsync(chatRequest, response, streamer, cancellationToken);
            }
            finally
            {
                response.Dispose();
            }
        }

        private async Task<ConversationContext> GenerateCompletionAsync(GenerateCompletionRequest generateRequest, IResponseStreamer<GenerateCompletionResponseStream> streamer, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "api/generate")
            {
                Content = new StringContent(JsonConvert.SerializeObject(generateRequest), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _client.SendAsync(request, generateRequest.Stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, cancellationToken);
            try
            {
                response.EnsureSuccessStatusCode();
                return await ProcessStreamedCompletionResponseAsync(response, streamer, cancellationToken);
            }
            finally
            {
                response.Dispose();
            }
        }

        private async Task<TResponse> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await _client.GetAsync(endpoint, cancellationToken);
            try
            {
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TResponse>(responseBody);
            }
            finally
            {
                response.Dispose();
            }
        }

        private async Task PostAsync<TRequest>(string endpoint, TRequest request, CancellationToken cancellationToken)
        {
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _client.PostAsync(endpoint, content, cancellationToken);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            finally
            {
                response.Dispose();
            }
        }

        private async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken cancellationToken)
        {
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _client.PostAsync(endpoint, content, cancellationToken);
            try
            {
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TResponse>(responseBody);
            }
            finally
            {
                response.Dispose();
            }
        }

        private async Task StreamPostAsync<TRequest, TResponse>(string endpoint, TRequest requestModel, IResponseStreamer<TResponse> streamer, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonConvert.SerializeObject(requestModel), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            try
            {
                response.EnsureSuccessStatusCode();
                await ProcessStreamedResponseAsync(response, streamer, cancellationToken);
            }
            finally
            {
                response.Dispose();
            }
        }

        private static async Task ProcessStreamedResponseAsync<TLine>(HttpResponseMessage response, IResponseStreamer<TLine> streamer, CancellationToken cancellationToken)
        {
            Stream stream = await response.Content.ReadAsStreamAsync();
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    string line = await reader.ReadLineAsync();
                    var streamedResponse = JsonConvert.DeserializeObject<TLine>(line);
                    streamer.Stream(streamedResponse);
                }
            }
        }

        private static async Task<ConversationContext> ProcessStreamedCompletionResponseAsync(HttpResponseMessage response, IResponseStreamer<GenerateCompletionResponseStream> streamer, CancellationToken cancellationToken)
        {
            Stream stream = await response.Content.ReadAsStreamAsync();
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    string line = await reader.ReadLineAsync();
                    var streamedResponse = JsonConvert.DeserializeObject<GenerateCompletionResponseStream>(line);
                    streamer.Stream(streamedResponse);

                    if (streamedResponse?.Done ?? false)
                    {
                        var doneResponse = JsonConvert.DeserializeObject<GenerateCompletionDoneResponseStream>(line);
                        return new ConversationContext(doneResponse.Context);
                    }
                }
            }
            return new ConversationContext(Array.Empty<long>());
        }

        private static async Task<IEnumerable<Message>> ProcessStreamedChatResponseAsync(ChatRequest chatRequest, HttpResponseMessage response, IResponseStreamer<ChatResponseStream> streamer, CancellationToken cancellationToken)
        {
            Stream stream = await response.Content.ReadAsStreamAsync();
            using (StreamReader reader = new StreamReader(stream))
            {
                ChatRole? responseRole = null;
                var responseContent = new StringBuilder();

                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    string line = await reader.ReadLineAsync();

                    var streamedResponse = JsonConvert.DeserializeObject<ChatResponseStream>(line);

                    // keep the streamed content to build the last message
                    // to return the list of messages
                    if (responseRole == null)
                    {
                        responseRole = streamedResponse?.Message?.Role;
                    }
                    responseContent.Append(streamedResponse?.Message?.Content ?? "");

                    streamer.Stream(streamedResponse);

                    if (streamedResponse?.Done ?? false)
                    {
                        var doneResponse = JsonConvert.DeserializeObject<ChatDoneResponseStream>(line);
                        var messages = chatRequest.Messages.ToList();
                        messages.Add(new Message(responseRole, responseContent.ToString()));
                        return messages;
                    }
                }
            }
            return Array.Empty<Message>();
        }

    }

    public class ConversationContext
    {
        public long[] Context { get; private set; }

        public ConversationContext(long[] context)
        {
            Context = context;
        }
    }

    public class ConversationContextWithResponse : ConversationContext
    {
        public string Response { get; private set; }

        public ConversationContextWithResponse(string response, long[] context) : base(context)
        {
            Response = response;
        }
    }

}
