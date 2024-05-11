using System.Collections.Generic;
using Newtonsoft.Json;

namespace OllamaSharp.Models.Chat
{
    /// <summary>
    /// https://github.com/jmorganca/ollama/blob/main/docs/api.md#generate-a-chat-completion
    /// </summary>
    public class ChatRequest
    {
        /// <summary>
        /// The model name (required)
        /// </summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>
        /// The messages of the chat, this can be used to keep a chat memory
        /// </summary>
        [JsonProperty("messages")]
        public IList<Message> Messages { get; set; }

        /// <summary>
        /// Additional model parameters listed in the documentation for the Modelfile such as temperature
        /// </summary>
        [JsonProperty("options", NullValueHandling = NullValueHandling.Ignore)]
        public RequestOptions Options { get; set; }

        /// <summary>
        /// The full prompt or prompt template (overrides what is defined in the Modelfile)
        /// </summary>
        [JsonProperty("template", NullValueHandling = NullValueHandling.Ignore)]
        public string Template { get; set; }

        /// <summary>
        /// Gets or sets the KeepAlive property, which decides how long a given model should stay loaded.
        /// </summary>
        [JsonProperty("keep_alive", NullValueHandling = NullValueHandling.Ignore)]
        public string KeepAlive { get; set; }

        /// <summary>
        /// The format to return a response in. Currently only accepts "json" or null.
        /// </summary>
        [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
        public string Format { get; set; }

        /// <summary>
        /// If false the response will be returned as a single response object, rather than a stream of objects
        /// </summary>
        [JsonProperty("stream")]
        public bool Stream { get; set; } = true;
    }
}
