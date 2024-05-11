using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace OllamaSharp.Models
{
    /// <summary>
    /// https://github.com/jmorganca/ollama/blob/main/docs/api.md#list-local-models
    /// </summary>
    public class ListModelsResponse
    {
        [JsonProperty("models")]
        public Model[] Models { get; set; }
    }

    [DebuggerDisplay("{Name}")]
    public class Model
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("modified_at")]
        public DateTime ModifiedAt { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("digest")]
        public string Digest { get; set; }

        [JsonProperty("details")]
        public Details Details { get; set; }
    }

    public class Details
    {
        [JsonProperty("parent_model")]
        public string ParentModel { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("family")]
        public string Family { get; set; }

        [JsonProperty("families")]
        public string[] Families { get; set; }

        [JsonProperty("parameter_size")]
        public string ParameterSize { get; set; }

        [JsonProperty("quantization_level")]
        public string QuantizationLevel { get; set; }
    }
}
