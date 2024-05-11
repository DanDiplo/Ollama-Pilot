using Newtonsoft.Json;

namespace OllamaSharp.Models
{
    /// <summary>
    /// https://github.com/jmorganca/ollama/blob/main/docs/api.md#pull-a-model
    /// </summary>
    public class PullModelRequest
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("insecure")]
        public bool Insecure { get; set; }
    }

    public class PullStatus
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("digest")]
        public string Digest { get; set; }

        [JsonProperty("total")]
        public long Total { get; set; }

        [JsonProperty("completed")]
        public long Completed { get; set; }

        [JsonIgnore]
        public double Percent => Total == 0 ? 100.0 : (double)Completed * 100 / Total;
    }
}
