using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows.Controls;
using System.Windows;
using OllamaSharp.Models;
using Task = System.Threading.Tasks.Task;
using System.Text.RegularExpressions;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Microsoft.VisualStudio;

namespace LLMCopilot
{
    public sealed class OllamaHelper
    {
        public string CodeCompleteTemplate = $"<|fim▁begin|>{0}<|fim▁hole|>{1}<|fim▁end|>";
        // 在 OllamaHelper 类中
        public delegate void MessageReceivedHandler(string content);
        public event MessageReceivedHandler OnMessageReceived;

        private static readonly Lazy<OllamaHelper> lazy = new Lazy<OllamaHelper>(() => new OllamaHelper());

        public static OllamaHelper Instance { get { return lazy.Value; } }

        private OllamaApiClient ollamaChatClient;

        private OllamaApiClient ollamaCompleteClient;

        private bool initNumCtx = false;
        private static readonly int defaultContext = 4096;
        private static readonly int defaultDeepSeekContext = 16384;
        private static readonly int defaultCodeLineLength = 80;
        private static readonly double PrefixCodeLinePercent = 0.8;
        private static readonly double SuffixCodeLinePercent = 1 - PrefixCodeLinePercent;

        private RequestOptions requestOptions;

        private readonly string[] stop = {
                 "<|fim▁begin|>",
                 "<|fim▁hole|>",
                 "<|fim▁end|>",
                 "//",
                 @"\n\n",
                 @"\r\n\r\n",
                "<|EOT|>",
                "<｜begin▁of▁sentence｜>",
                "<｜end▁of▁sentence｜>"
            };


        public Chat Chat { get; private set; }

        private OptionPageGrid options;

        private OllamaHelper()
        {
            var package = ServiceProvider.Package;
            options = (OptionPageGrid)package.GetDialogPage(typeof(OptionPageGrid));

            requestOptions = new RequestOptions {
                NumCtx = 4096,
                NumPredict = 128,
                Stop = stop,
                Temperature = 0.01f
            };

            string baseUrl = options.BaseUrl;
            string completeModel = options.CompleteModel;
            string chatModel = options.ChatModel;

            ollamaChatClient = new OllamaApiClient(baseUrl);
            ollamaChatClient.SelectedModel = chatModel;
            Chat = new Chat(ollamaChatClient, OnChatResponseReceived);

            ollamaCompleteClient = new OllamaApiClient(baseUrl);
            ollamaCompleteClient.SelectedModel = completeModel;

            options.SettingsChanged += Options_SettingsChanged;

            Task.Run(async () => await this.InitModelCtx());
        }

        ~OllamaHelper()
        {
            options.SettingsChanged -= Options_SettingsChanged;
        }

        public string GetExplainCodeTemplate(string code, string file)
        {
            string template = $@"```cpp
{code}
```
explain this code from `{file}`, response in {options.Language}";

            return template;
        }

        public string GetFindBugTemplate(string code, string file)
        {
            string template = $@"```cpp
{code}
```
find bug in this code from `{file}`, response in {options.Language}";

            return template;
        }

        public string GetOptimizeCodeTemplate(string code, string file)
        {
            string template = $@"```cpp
{code}
```
Optimize this code from `{file}`, response in {options.Language}";

            return template;
        }

        public static int EstimateTokensByChars(string str)
        {
            return str.Length / 4;
        }

        public static int EstimatePrefixLinesByCtx(int nCtx)
        {
            return Convert.ToInt32(nCtx * PrefixCodeLinePercent) / defaultCodeLineLength;
        }

        public static int EstimateSuffixLinesByCtx(int nCtx)
        {
            return Convert.ToInt32(nCtx * SuffixCodeLinePercent) / defaultCodeLineLength;
        }

        private void Options_SettingsChanged(object sender, EventArgs e)
        {
            this.SetOllamaOptions();
        }

        private void SetOllamaOptions()
        {
            string baseUrl = options.BaseUrl;
            string completeModel = options.CompleteModel;
            string chatModel = options.ChatModel;

            ollamaChatClient = new OllamaApiClient(baseUrl);
            ollamaChatClient.SelectedModel = chatModel;
            Chat = new Chat(ollamaChatClient, OnChatResponseReceived);

            ollamaCompleteClient = new OllamaApiClient(baseUrl);
            ollamaCompleteClient.SelectedModel = completeModel;
            initNumCtx = false;
            Task.Run(async () => await this.InitModelCtx());
        }

        private async Task InitModelCtx()
        {
            try
            {
                if (initNumCtx)
                {
                    return;
                }
                var chatModelInfo = await OllamaChatClient.ShowModelInformation(ollamaChatClient.SelectedModel);
                var compModelInfo = await ollamaCompleteClient.ShowModelInformation(ollamaCompleteClient.SelectedModel);

                Func<string, string, int> GetCtx = (string parameters, string model) =>
                {
                    int num_ctx = model.ToLower().Contains("deepseek")? defaultDeepSeekContext: defaultContext; 
                    if (!string.IsNullOrEmpty(parameters))
                    {
                        var match = Regex.Match(parameters, @"PARAMETER\s+num_ctx\s+(\d+)");
                        if (match.Success && match.Groups.Count > 1)
                        {
                            int.TryParse(match.Groups[1].Value, out num_ctx);
                        }
                    }
                    return num_ctx;
                };

                int chatCtx = GetCtx(chatModelInfo.Parameters, ollamaChatClient.SelectedModel);
                Chat.Options.NumCtx = chatCtx;

                int CompCtx = GetCtx(compModelInfo.Parameters, ollamaCompleteClient.SelectedModel);
                requestOptions.NumCtx = CompCtx;

                initNumCtx = true;
            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex);
            }

        }

        private void OnChatResponseReceived(ChatResponseStream response)
        {
            if (response.Message != null && response.Message.Content != null)
            {
                OnMessageReceived?.Invoke(response.Message.Content);
            }
        }


        public OllamaApiClient OllamaChatClient
        {
            get { return ollamaChatClient; }
        }

        public OllamaApiClient OllamaCompleteClient
        {
            get { return ollamaCompleteClient; }
        }
    }

    public static class EventManager
    {
        public static event EventHandler<CommandExecutedEventArgs> CodeCommandExecuted;

        public static void OnCodeCommandExecuted(string selectedText)
        {
            CodeCommandExecuted?.Invoke(null, new CommandExecutedEventArgs(selectedText));
        }
    }

    public class CommandExecutedEventArgs : EventArgs
    {
        public string SelectedText { get; }

        public CommandExecutedEventArgs(string selectedText)
        {
            SelectedText = selectedText;
        }
    }

    public class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate UserTemplate { get; set; }
        public DataTemplate AssistantTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var message = item as MyMessage;
            if (message != null)
            {
                return message.Role == ChatRole.User ? UserTemplate : AssistantTemplate;
            }
            return base.SelectTemplate(item, container);
        }
    }
}
