using Newtonsoft.Json;

namespace OllamaSharp.Models
{
    /// <summary>
    /// https://github.com/jmorganca/ollama/blob/main/docs/api.md#delete-a-model
    /// </summary>
    public class DeleteModelRequest
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
