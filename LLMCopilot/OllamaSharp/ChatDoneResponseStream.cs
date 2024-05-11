using Newtonsoft.Json;

namespace OllamaSharp.Models.Chat
{
    public class ChatDoneResponseStream : ChatResponseStream
    {
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
