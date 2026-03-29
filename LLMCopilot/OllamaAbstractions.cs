using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OllamaPilot
{
    public sealed class RequestOptions
    {
        public int? NumCtx { get; set; }
        public int? NumPredict { get; set; }
        public float? Temperature { get; set; }
        public string[] Stop { get; set; }
    }

    public class GenerateCompletionRequest
    {
        public string Model { get; set; }
        public string Prompt { get; set; }
        public string Suffix { get; set; }
        public RequestOptions Options { get; set; }
        public bool Raw { get; set; }
    }

    public sealed class GenerateCompletionResult
    {
        public string Response { get; set; }
    }

    public sealed class Model
    {
        public string Name { get; set; }
    }

    public sealed class ShowModelResponse
    {
        public string Parameters { get; set; }
        public string Template { get; set; }
    }

    public sealed class ChatResponseStream
    {
        public string Model { get; set; }
        public Message Message { get; set; }
        public bool Done { get; set; }
    }

    public class Message
    {
        public Message(ChatRole role, string content, string[] images)
        {
            Role = role;
            Content = content;
            Images = images;
        }

        public Message(ChatRole role, string[] images)
        {
            Role = role;
            Images = images;
        }

        public Message(ChatRole? role, string content)
        {
            Role = role;
            Content = content;
        }

        public Message()
        {
        }

        public ChatRole? Role { get; set; }
        public string Content { get; set; }
        public string[] Images { get; set; }
    }

    public readonly struct ChatRole : IEquatable<ChatRole>
    {
        private const string AssistantValue = "assistant";
        private const string SystemValue = "system";
        private const string UserValue = "user";
        private readonly string _value;

        public ChatRole(string role)
        {
            _value = role ?? throw new ArgumentNullException(nameof(role));
        }

        public static ChatRole System { get; } = new ChatRole(SystemValue);
        public static ChatRole Assistant { get; } = new ChatRole(AssistantValue);
        public static ChatRole User { get; } = new ChatRole(UserValue);

        public static bool operator ==(ChatRole left, ChatRole right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChatRole left, ChatRole right)
        {
            return !left.Equals(right);
        }

        public static implicit operator ChatRole(string value)
        {
            return new ChatRole(value);
        }

        public override bool Equals(object obj)
        {
            return obj is ChatRole other && Equals(other);
        }

        public bool Equals(ChatRole other)
        {
            return string.Equals(_value, other._value, StringComparison.InvariantCultureIgnoreCase);
        }

        public override int GetHashCode()
        {
            return StringComparer.InvariantCultureIgnoreCase.GetHashCode(_value ?? string.Empty);
        }

        public override string ToString()
        {
            return _value ?? string.Empty;
        }
    }

    public interface IOllamaService
    {
        Chat CreateChatSession(string baseUrl, string model, string accessToken, RequestOptions options, Action<ChatResponseStream> streamer);
        Task<IReadOnlyList<Model>> ListLocalModelsAsync(string baseUrl, string accessToken, CancellationToken cancellationToken);
        Task<ShowModelResponse> ShowModelInformationAsync(string baseUrl, string accessToken, string model, CancellationToken cancellationToken);
        Task<GenerateCompletionResult> GenerateCompletionAsync(string baseUrl, string accessToken, GenerateCompletionRequest request, CancellationToken cancellationToken);
    }

    public sealed class Chat
    {
        private readonly IChatSession _session;

        internal Chat(IChatSession session)
        {
            _session = session;
        }

        public string SelectedModel
        {
            get { return _session.SelectedModel; }
            set { _session.SelectedModel = value; }
        }

        public RequestOptions Options
        {
            get { return _session.Options; }
            set { _session.Options = value; }
        }

        public string AccessToken
        {
            get { return _session.AccessToken; }
            set { _session.AccessToken = value; }
        }

        public Task SendAsync(string message, CancellationToken cancellationToken)
        {
            return _session.SendAsync(message, cancellationToken);
        }
    }

    internal interface IChatSession
    {
        string SelectedModel { get; set; }
        RequestOptions Options { get; set; }
        string AccessToken { get; set; }
        Task SendAsync(string message, CancellationToken cancellationToken);
    }
}
