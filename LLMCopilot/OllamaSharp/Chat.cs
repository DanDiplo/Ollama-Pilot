using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using OllamaSharp.Streamer;

namespace OllamaSharp.Models.Chat
{
    public class Chat
    {
        private List<Message> _messages = new List<Message>();

        public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

        public IOllamaApiClient Client { get; private set; }

        public RequestOptions Options { get; set; }

        public IResponseStreamer<ChatResponseStream> Streamer { get; private set; }

        public Chat(IOllamaApiClient client, Action<ChatResponseStream> streamer, RequestOptions options)
            : this(client, new ActionResponseStreamer<ChatResponseStream>(streamer), options)
        {
        }

        public Chat(IOllamaApiClient client, IResponseStreamer<ChatResponseStream> streamer, RequestOptions options)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (streamer == null) throw new ArgumentNullException(nameof(streamer));

            Client = client;
            Streamer = streamer;

            Options = options;
        }

        public Task<IEnumerable<Message>> SendAsync(string message, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendAsync(message, null, cancellationToken);
        }

        public Task<IEnumerable<Message>> SendAsync(string message, IEnumerable<string> imagesAsBase64, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendAsAsync(ChatRole.User, message, imagesAsBase64, cancellationToken);
        }

        public Task<IEnumerable<Message>> SendAsAsync(ChatRole role, string message, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendAsAsync(role, message, null, cancellationToken);
        }

        public async Task<IEnumerable<Message>> SendAsAsync(ChatRole role, string message, IEnumerable<string> imagesAsBase64, CancellationToken cancellationToken = default(CancellationToken))
        {
            _messages.Add(new Message(role, message, imagesAsBase64?.ToArray()));

            var request = new ChatRequest
            {
                Messages = _messages.ToList(),
                Model = Client.SelectedModel,
                Stream = true,
                Options = Options
            };

            var answer = await Client.SendChatAsync(request, Streamer, cancellationToken);
            _messages = answer.ToList();
            return _messages;
        }
    }
}
