using Newtonsoft.Json;

namespace OllamaSharp.Models
{
    /// <summary>
    /// https://github.com/jmorganca/ollama/blob/main/docs/api.md#push-a-model
    /// </summary>
    public class PushRequest
    {
        /// <summary>
        /// Name of the model to push in the form of <namespace>/<model>:<tag>
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("insecure")]
        public bool Insecure { get; set; }

        [JsonProperty("stream")]
        public bool Stream { get; set; }
    }

    public class PushStatus
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("digest")]
        public string Digest { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }
    }
}
