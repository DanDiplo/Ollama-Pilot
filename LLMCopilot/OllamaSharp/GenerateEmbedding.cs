using Newtonsoft.Json;

namespace OllamaSharp.Models
{
    /// <summary>
    /// https://github.com/jmorganca/ollama/blob/main/docs/api.md#generate-embeddings
    /// </summary>
    public class GenerateEmbeddingRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("options")]
        public string Options { get; set; }
    }

    public class GenerateEmbeddingResponse
    {
        [JsonProperty("embedding")]
        public double[] Embedding { get; set; }
    }
}
