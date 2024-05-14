using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace LLMCopilot
{
    public enum ResponseLanguage
    { 
        English,
        Chinese,
    }
    public class OptionPageGrid : DialogPage
    {
        private string baseUrl = "http://localhost:11434";
        private string completeModel = "deepseek-coder:6.7b";
        private string chatModel = "deepseek-coder:6.7b";
        private ResponseLanguage language = ResponseLanguage.English;

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

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (!IsValidUrl(BaseUrl))
            {
                // 如果 URL 无效，显示错误消息并阻止页面关闭
                VsShellUtilities.ShowMessageBox(
                    this.Site,
                    "The URL provided is invalid, please try again.",
                    "IVALID URL",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                e.ApplyBehavior = ApplyKind.Cancel;
            }
            else
            {
                OnSettingsChanged();
                base.OnApply(e);
            }
        }

        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri result)
                   && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }

        [Category("LLMCopilot")]
        [DisplayName("Complete Model")]
        [Description("LLM model name used for code complete")]
        public string CompleteModel
        {
            get { return completeModel; }
            set { 
                completeModel = value;
            }
        }

        [Category("LLMCopilot")]
        [DisplayName("Chat Model")]
        [Description("LLM model name used for chat")]
        public string ChatModel
        {
            get { return chatModel; }
            set { 
                chatModel = value;
            }
        }
    }
}
