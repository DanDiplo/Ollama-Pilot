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
        // 在 OllamaHelper 类中
        public delegate void MessageReceivedHandler(string content);
        public event MessageReceivedHandler OnMessageReceived;

        private static readonly Lazy<OllamaHelper> lazy = new Lazy<OllamaHelper>(() => new OllamaHelper());

        public static OllamaHelper Instance { get { return lazy.Value; } }

        private OllamaApiClient ollamaClient;
       
        public Chat Chat { get; private set; }

        private OllamaHelper()
        {
            ollamaClient = new OllamaApiClient("http://localhost:11434");
            ollamaClient.SelectedModel ="deepseek-coder:6.7b";
            Chat = new Chat(ollamaClient, OnChatResponseReceived);
        }

        private void OnChatResponseReceived(ChatResponseStream response)
        {
            if (response.Message != null && response.Message.Content != null)
            {
                OnMessageReceived?.Invoke(response.Message.Content);
            }
        }


        public OllamaApiClient OllamaClient
        {
            get { return ollamaClient; }
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
