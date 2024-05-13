using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace LLMCopilot
{
    public enum ResponseLanguage
    { 
        EN,
        CN,
    }
    public class OptionPageGrid : DialogPage
    {
        private string baseUrl = "http://localhost:11434";
        private string completeModel = "deepseek-coder:6.7b";
        private string chatModel = "deepseek-coder:6.7b";
        private ResponseLanguage language = ResponseLanguage.EN;

        public event EventHandler SettingsChanged;

        [Category("LLMCopilot")]
        [DisplayName("Base URL")]
        [Description("Ollama API的Base URL.")]
        public string BaseUrl
        {
            get { return baseUrl; }
            set { baseUrl = value; }
        }

        [Category("LLMCopilot")]
        [DisplayName("LLM response Language")]
        [Description("大模型回复的语言（如果可用）")]
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
                    "您输入的URL不合法，请重新输入",
                    "非法URL",
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
        [Description("用于代码补全的模型名称。")]
        public string CompleteModel
        {
            get { return completeModel; }
            set { 
                completeModel = value;
            }
        }

        [Category("LLMCopilot")]
        [DisplayName("Chat Model")]
        [Description("用于聊天窗口的模型名称。")]
        public string ChatModel
        {
            get { return chatModel; }
            set { 
                chatModel = value;
            }
        }
    }
}
