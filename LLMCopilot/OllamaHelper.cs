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

        private static readonly Lazy<OllamaHelper> lazy = new Lazy<OllamaHelper>(() => new OllamaHelper());

        public static OllamaHelper Instance { get { return lazy.Value; } }

        private static readonly int defaultContext = 4096;
        private static readonly int defaultDeepSeekContext = 16384;
        private static readonly int defaultCodeLineLength = 80;
        private static readonly double PrefixCodeLinePercent = 0.8;
        private static readonly double SuffixCodeLinePercent = 1 - PrefixCodeLinePercent;

        public RequestOptions CompRequestOptions { get; private set; }
        public RequestOptions ChatRequestOptions { get; private set; }

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

        public OptionPageGrid Options { get; private set; }


        private OllamaHelper()
        {
            var package = ServiceProvider.Package;
            Options = (OptionPageGrid)package.GetDialogPage(typeof(OptionPageGrid));

            CompRequestOptions = new RequestOptions {
                NumCtx = 4096,
                NumPredict = 128,
                Stop = stop,
                Temperature = 0.01f
            };

            ChatRequestOptions = new RequestOptions
            {
                NumCtx = 4096,
                NumPredict = 1024,
                Temperature = 0.7f
            };

            Options.SettingsChanged += Options_SettingsChanged;
        }

        ~OllamaHelper()
        {
            Options.SettingsChanged -= Options_SettingsChanged;
        }

        public string GetExplainCodeTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            string template = $@"```{code_type}
{code}
```
explain this code from `{file}`, response in {Options.Language}";

            return template;
        }

        public string GetFindBugTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            string template = $@"```{code_type}
{code}
```
find bug in this code from `{file}`, response in {Options.Language}";

            return template;
        }

        public string GetOptimizeCodeTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            string template = $@"```{code_type}
{code}
```
Optimize this code from `{file}`, response in {Options.Language}";

            return template;
        }

        public string GetAddCommentTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            string template = $@"```{code_type}
{code}
```
Add comments in Google style for this code from `{file}`,  response Only the code in a markdown box";

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
            string baseUrl = Options.BaseUrl;
            string completeModel = Options.CompleteModel;
            string chatModel = Options.ChatModel;
            Task.Run(async () => await this.InitModelCtx());
        }

        public async Task InitModelCtx()
        {
            try
            {
                var client = OllamaClientFactory.CreateClient();
                var chatModelInfo = await client.ShowModelInformation(Options.ChatModel);
                var compModelInfo = await client.ShowModelInformation(Options.CompleteModel);

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

                int chatCtx = GetCtx(chatModelInfo.Parameters, Options.ChatModel);
                ChatRequestOptions.NumCtx = chatCtx;

                int CompCtx = GetCtx(compModelInfo.Parameters, Options.CompleteModel);
                CompRequestOptions.NumCtx = CompCtx;
            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex);
            }

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

    public class OllamaClientFactory
    {
        public static OllamaApiClient CreateClient()
        {
            var options = OllamaHelper.Instance.Options;
            var ollamaApiClient = new OllamaApiClient(options.BaseUrl);
            ollamaApiClient.SelectedModel = options.ChatModel;
            return ollamaApiClient;
        }

        public static Chat CreateChat(Action<ChatResponseStream> streamer)
        {
            var ollamaApiClient = CreateClient();
            var chat = new Chat(ollamaApiClient, streamer, OllamaHelper.Instance.ChatRequestOptions);

            return chat;
        }
    }
}
