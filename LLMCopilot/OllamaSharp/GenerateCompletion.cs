using Newtonsoft.Json;

namespace OllamaSharp.Models
{
    /// <summary>
    /// https://github.com/jmorganca/ollama/blob/main/docs/api.md#generate-a-completion
    /// </summary>
    public class GenerateCompletionRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("options", NullValueHandling = NullValueHandling.Ignore)]
        public RequestOptions Options { get; set; }

        [JsonProperty("images", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Images { get; set; }

        [JsonProperty("system", NullValueHandling = NullValueHandling.Ignore)]
        public string System { get; set; }

        [JsonProperty("template", NullValueHandling = NullValueHandling.Ignore)]
        public string Template { get; set; }

        [JsonProperty("context", NullValueHandling = NullValueHandling.Ignore)]
        public long[] Context { get; set; }

        [JsonProperty("keep_alive", NullValueHandling = NullValueHandling.Ignore)]
        public string KeepAlive { get; set; }

        [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
        public string Format { get; set; }

        [JsonProperty("stream")]
        public bool Stream { get; set; } = true;

        [JsonProperty("raw")]
        public bool Raw { get; set; }
    }

    public class GenerateCompletionResponseStream
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("response")]
        public string Response { get; set; }

        [JsonProperty("done")]
        public bool Done { get; set; }
    }

    public class GenerateCompletionDoneResponseStream : GenerateCompletionResponseStream
    {
        [JsonProperty("context")]
        public long[] Context { get; set; }

        [JsonProperty("total_duration")]
        public long TotalDuration { get; set; }

        [JsonProperty("load_duration")]
        public long LoadDuration { get; set; }

        [JsonProperty("prompt_eval_count")]
        public int PromptEvalCount { get; set; }

        [JsonProperty("prompt_eval_duration")]
        public long PromptEvalDuration { get; set; }

        [JsonProperty("eval_count")]
        public int EvalCount { get; set; }

        [JsonProperty("eval_duration")]
        public long EvalDuration { get; set; }
    }
}
