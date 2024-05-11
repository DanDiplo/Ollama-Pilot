using Newtonsoft.Json;

namespace OllamaSharp.Models
{
    /// <summary>
    /// https://github.com/jmorganca/ollama/blob/main/docs/api.md#show-model-information
    /// </summary>
    public class ShowModelRequest
    {
        /// <summary>
        /// The name of the model to show
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class ShowModelResponse
    {
        [JsonProperty("license")]
        public string License { get; set; }

        [JsonProperty("modelfile")]
        public string Modelfile { get; set; }

        [JsonProperty("parameters")]
        public string Parameters { get; set; }

        [JsonProperty("template")]
        public string Template { get; set; }
    }
}
