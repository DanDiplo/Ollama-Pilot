using Newtonsoft.Json;

namespace OllamaSharp.Models
{
    /// <summary>
    /// https://github.com/jmorganca/ollama/blob/main/docs/api.md#copy-a-model
    /// </summary>
    public class CopyModelRequest
    {
        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("destination")]
        public string Destination { get; set; }
    }
}
