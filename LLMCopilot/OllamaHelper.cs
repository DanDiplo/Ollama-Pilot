using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace LLMCopilot
{
    public sealed class OllamaHelper
    {
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
               
            }
        }

        public OllamaApiClient OllamaClient
        {
            get { return ollamaClient; }
        }
    }
}
