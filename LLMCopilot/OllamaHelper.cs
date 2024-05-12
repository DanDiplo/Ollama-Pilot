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

namespace LLMCopilot
{
    public sealed class OllamaHelper
    {
        public string CodeCompleteTemplate = $"'''{0}''', Complete this code, response ONLY code in plain text:";
        // 在 OllamaHelper 类中
        public delegate void MessageReceivedHandler(string content);
        public event MessageReceivedHandler OnMessageReceived;

        private static readonly Lazy<OllamaHelper> lazy = new Lazy<OllamaHelper>(() => new OllamaHelper());

        public static OllamaHelper Instance { get { return lazy.Value; } }

        private OllamaApiClient ollamaChatClient;

        private OllamaApiClient ollamaCompleteClient;
       
        public Chat Chat { get; private set; }

        private OllamaHelper()
        {
            var package = ServiceProvider.Package;
            var options = (OptionPageGrid)package.GetDialogPage(typeof(OptionPageGrid));
            string baseUrl = options.BaseUrl;
            string completeModel = options.CompleteModel;
            string chatModel = options.ChatModel;

            ollamaChatClient = new OllamaApiClient(baseUrl);
            ollamaChatClient.SelectedModel = chatModel;
            Chat = new Chat(ollamaChatClient, OnChatResponseReceived);

            ollamaCompleteClient = new OllamaApiClient(baseUrl);
            ollamaCompleteClient.SelectedModel = completeModel;
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
