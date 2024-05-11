using Newtonsoft.Json;

namespace OllamaSharp.Models.Chat
{
    public class ChatResponseStream
    {
        /// <summary>
        /// Model identifier of the response.
        /// </summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>
        /// Timestamp when the response was created.
        /// </summary>
        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        /// <summary>
        /// The message component of the response.
        /// </summary>
        [JsonProperty("message")]
        public Message Message { get; set; }

        /// <summary>
        /// Indicates whether the chat interaction is completed.
        /// </summary>
        [JsonProperty("done")]
        public bool Done { get; set; }
    }
}
