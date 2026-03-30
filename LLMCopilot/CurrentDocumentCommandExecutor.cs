using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OllamaPilot
{
    internal sealed class CurrentDocumentChatRequest
    {
        public string Prompt { get; set; }
        public string VisibleMessage { get; set; }
    }

    internal static class CurrentDocumentCommandExecutor
    {
        public static async Task ExecuteAsync(
            AsyncPackage package,
            Func<string, string, string> createPrompt,
            Func<string, string> createVisibleMessage,
            string emptyDocumentMessage,
            AssistantActionCapabilities assistantActions = AssistantActionCapabilities.Discussion)
        {
            var request = await TryCreateRequestAsync(package, createPrompt, createVisibleMessage, emptyDocumentMessage);
            if (request == null)
            {
                return;
            }

            try
            {
                VsHelpers.OpenChatWindow();
                await System.Threading.Tasks.Task.Yield();
                EventManager.OnCodeCommandExecuted(request.VisibleMessage, request.Prompt, assistantActions: assistantActions);
            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex, "Unable to send the current document to Ollama Pilot.");
            }
        }

        public static async Task<CurrentDocumentChatRequest> TryCreateRequestAsync(
            AsyncPackage package,
            Func<string, string, string> createPrompt,
            Func<string, string> createVisibleMessage,
            string emptyDocumentMessage)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            string documentText;
            string documentPath;
            try
            {
                documentText = await VsHelpers.GetActiveDocumentTextAsync(package);
                documentPath = await VsHelpers.GetActiveDocumentPathAsync(package);
            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex, "Unable to read the active document.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(documentText))
            {
                VsHelpers.ShowInfo(emptyDocumentMessage);
                return null;
            }

            try
            {
                var promptText = PrepareDocumentForPrompt(documentText);
                var prompt = createPrompt(promptText, documentPath ?? string.Empty);
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    VsHelpers.ShowError("Unable to build the prompt for the current document.");
                    return null;
                }

                var visibleMessage = createVisibleMessage(Path.GetFileName(documentPath ?? "current file"));
                if (documentText.Length > promptText.Length)
                {
                    visibleMessage += " The file was trimmed to fit the current chat context.";
                }

                return new CurrentDocumentChatRequest
                {
                    Prompt = prompt,
                    VisibleMessage = visibleMessage
                };
            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex, "Unable to prepare the current document for Ollama Pilot.");
                return null;
            }
        }

        internal static string PrepareDocumentForPrompt(string documentText)
        {
            if (string.IsNullOrWhiteSpace(documentText))
            {
                return string.Empty;
            }

            var chatCtx = Math.Max(1024, OllamaHelper.Instance.Options.ChatCtxSize);
            var maxChars = Math.Max(3000, (int)(OllamaHelper.EstimateCharsByTokens(chatCtx) * 0.65));
            if (documentText.Length <= maxChars)
            {
                return documentText;
            }

            var headLength = Math.Max(1000, maxChars / 2);
            var tailLength = Math.Max(1000, maxChars - headLength);
            if (headLength + tailLength >= documentText.Length)
            {
                return documentText;
            }

            return string.Concat(
                documentText.Substring(0, headLength),
                Environment.NewLine,
                Environment.NewLine,
                "// ... file content trimmed for prompt size ...",
                Environment.NewLine,
                Environment.NewLine,
                documentText.Substring(documentText.Length - tailLength, tailLength));
        }
    }
}
