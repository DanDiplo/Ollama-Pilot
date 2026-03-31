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

    internal enum DocumentPromptProfile
    {
        Default,
        Review
    }

    internal static class CurrentDocumentCommandExecutor
    {
        public static async Task ExecuteAsync(
            AsyncPackage package,
            Func<string, string, string> createPrompt,
            Func<string, string> createVisibleMessage,
            string emptyDocumentMessage,
            DocumentPromptProfile promptProfile = DocumentPromptProfile.Default,
            AssistantActionCapabilities assistantActions = AssistantActionCapabilities.Discussion)
        {
            var request = await TryCreateRequestAsync(package, createPrompt, createVisibleMessage, emptyDocumentMessage, promptProfile);
            if (request == null)
            {
                return;
            }

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
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
            string emptyDocumentMessage,
            DocumentPromptProfile promptProfile = DocumentPromptProfile.Default)
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
                var promptText = PrepareDocumentForPrompt(documentText, promptProfile);
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

        internal static string PrepareDocumentForPrompt(string documentText, DocumentPromptProfile promptProfile = DocumentPromptProfile.Default)
        {
            if (string.IsNullOrWhiteSpace(documentText))
            {
                return string.Empty;
            }

            var chatCtx = Math.Max(1024, OllamaHelper.Instance.Options.ChatCtxSize);
            var usageFraction = promptProfile == DocumentPromptProfile.Review ? 0.45 : 0.65;
            var minimumChars = promptProfile == DocumentPromptProfile.Review ? 2200 : 3000;
            var maxChars = Math.Max(minimumChars, (int)(OllamaHelper.EstimateCharsByTokens(chatCtx) * usageFraction));
            if (documentText.Length <= maxChars)
            {
                return documentText;
            }

            var headFraction = promptProfile == DocumentPromptProfile.Review ? 0.35 : 0.5;
            var headLength = Math.Max(800, (int)(maxChars * headFraction));
            var tailLength = Math.Max(800, maxChars - headLength);
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
