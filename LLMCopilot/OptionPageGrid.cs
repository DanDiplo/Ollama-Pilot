using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace OllamaPilot
{
    public enum ResponseLanguage
    { 
        English,
        Chinese,
    }

    public enum AutoCompleteTriggerMode
    {
        ManualOnly,
        Smart,
        Aggressive,
    }

    public class OptionPageGrid : DialogPage
    {
        private string baseUrl = "http://localhost:11434";
        private string completeModel = "deepseek-coder:6.7b";
        private string chatModel = "deepseek-coder:6.7b";
        private bool enableAutoComplete = false;
        private string fim_begin = "<｜fim▁begin｜>";
        private string fim_end = "<｜fim▁end｜>";
        private string fim_hole = "<｜fim▁hole｜>";
        private ResponseLanguage language = ResponseLanguage.English;
        private string access_token = string.Empty;
        private int chat_ctx_size = 4096;
        private int complete_ctx_size = 2048;
        private AutoCompleteTriggerMode autoCompleteTriggerMode = AutoCompleteTriggerMode.Smart;
        private int autoCompleteDelayMs = 350;
        private int autoCompleteMinPrefixLength = 3;

        public event EventHandler SettingsChanged;

        [Category("LLMCopilot")]
        [DisplayName("Base URL")]
        [Description("Ollama Base URL.")]
        public string BaseUrl
        {
            get { return baseUrl; }
            set { baseUrl = value; }
        }

        [Category("LLMCopilot")]
        [DisplayName("LLM response Language")]
        [Description("Language used in LLM's response(if available)")]
        public ResponseLanguage Language
        {
            get { return language; }
            set { language = value; }
        }

        protected void OnSettingsChanged()
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public OllamaValidationResult ValidateSettings()
        {
            return OllamaSettingsValidator.Validate(this);
        }

        public void PersistSettings()
        {
            SaveSettingsToStorage();
            OnSettingsChanged();
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (!IsValidUrl(BaseUrl))
            {
                VsShellUtilities.ShowMessageBox(
                    this.Site,
                    "The URL provided is invalid, please try again.",
                    "Invalid URL",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                e.ApplyBehavior = ApplyKind.Cancel;
            }

            if (e.ApplyBehavior != ApplyKind.Cancel)
            {
                var validation = OllamaSettingsValidator.Validate(this);
                if (!validation.Success)
                {
                    VsShellUtilities.ShowMessageBox(
                        this.Site,
                        validation.Message,
                        "LLMCopilot Settings",
                        OLEMSGICON.OLEMSGICON_WARNING,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                    e.ApplyBehavior = ApplyKind.Cancel;
                }
            }

            if (e.ApplyBehavior == ApplyKind.Cancel)
            {
                return;
            }

            OnSettingsChanged();
            base.OnApply(e);
        }

        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri result)
                   && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }

        [Category("LLMCopilot")]
        [DisplayName("Code Complete Model")]
        [Description("LLM model name used for code complete")]
        public string CompleteModel
        {
            get { return completeModel; }
            set { 
                completeModel = value;
            }
        }

        [Category("LLMCopilot")]
        [DisplayName("complete ctx length")]
        [Description("Context length for complete model")]
        public int CompleteCtxSize
        {
            get { return complete_ctx_size; }
            set
            {
                complete_ctx_size = value;
            }
        }

        [Category("LLMCopilot")]
        [DisplayName("Chat model")]
        [Description("LLM model name used for chat")]
        public string ChatModel
        {
            get { return chatModel; }
            set { 
                chatModel = value;
            }
        }

        [Category("LLMCopilot")]
        [DisplayName("chat ctx length")]
        [Description("Context length for chat model")]
        public int ChatCtxSize
        {
            get { return chat_ctx_size; }
            set
            {
                chat_ctx_size = value;
            }
        }

        [Category("LLMCopilot")]
        [DisplayName("Enable Auto Complete")]
        [Description("Auto complete code when you typing")]
        public bool EnableAutoComplete
        {
            get { return enableAutoComplete; }
            set
            {
                enableAutoComplete = value;
            }
        }

        [Category("LLMCopilot")]
        [DisplayName("Auto Complete Trigger Mode")]
        [Description("ManualOnly triggers on Tab. Smart uses punctuation and Tab. Aggressive also reacts to Enter.")]
        public AutoCompleteTriggerMode AutoCompleteTriggerMode
        {
            get { return autoCompleteTriggerMode; }
            set { autoCompleteTriggerMode = value; }
        }

        [Category("LLMCopilot")]
        [DisplayName("Auto Complete Delay (ms)")]
        [Description("Delay before requesting inline completion after a trigger.")]
        public int AutoCompleteDelayMs
        {
            get { return autoCompleteDelayMs; }
            set { autoCompleteDelayMs = value; }
        }

        [Category("LLMCopilot")]
        [DisplayName("Auto Complete Min Prefix")]
        [Description("Minimum non-whitespace prefix length before automatic completion can trigger.")]
        public int AutoCompleteMinPrefixLength
        {
            get { return autoCompleteMinPrefixLength; }
            set { autoCompleteMinPrefixLength = value; }
        }

        [Category("LLMCopilot")]
        [DisplayName("Fim begin token")]
        [Description("Fill in the middle begin Token for code complete LLM model You have selected")]
        public string FimBegin
        {
            get { return fim_begin; }
            set
            {
                fim_begin = value;
            }
        }

        [Category("LLMCopilot")]
        [DisplayName("Fim end token")]
        [Description("Fill in the middle end Token for code complete LLM model You have selected")]
        public string FimEnd
        {
            get { return fim_end; }
            set
            {
                fim_end = value;
            }
        }

        [Category("LLMCopilot")]
        [DisplayName("Fim hole token")]
        [Description("Fill in the middle hole Token for code complete LLM model You have selected")]
        public string FimHole
        {
            get { return fim_hole; }
            set
            {
                fim_hole = value;
            }
        }

        [Category("LLMCopilot")]
        [DisplayName("Reverse Proxy Access Token")]
        [Description("Bearer access token used for reverse proxy to connect to your Ollama server")]
        public string AccessToken
        {
            get { return access_token; }
            set { access_token = value; }
        }
    }
}
