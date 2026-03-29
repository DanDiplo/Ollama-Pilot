using Microsoft.VisualStudio.Shell;
using System;
using System.IO;

namespace OllamaPilot
{
    internal static class CurrentDocumentCommandExecutor
    {
        public static void Execute(
            AsyncPackage package,
            Func<string, string, string> createPrompt,
            Func<string, string> createVisibleMessage,
            string emptyDocumentMessage)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ThreadHelper.JoinableTaskFactory.Run(async delegate
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
                    return;
                }

                if (string.IsNullOrWhiteSpace(documentText))
                {
                    VsHelpers.ShowInfo(emptyDocumentMessage);
                    return;
                }

                var promptText = PrepareDocumentForPrompt(documentText);
                var prompt = createPrompt(promptText, documentPath ?? string.Empty);
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    VsHelpers.ShowError("Unable to build the prompt for the current document.");
                    return;
                }

                var visibleMessage = createVisibleMessage(Path.GetFileName(documentPath ?? "current file"));
                if (documentText.Length > promptText.Length)
                {
                    visibleMessage += " The file was trimmed to fit the current chat context.";
                }

                try
                {
                    VsHelpers.OpenChatWindow();
                    EventManager.OnCodeCommandExecuted(visibleMessage, prompt);
                }
                catch (Exception ex)
                {
                    LLMErrorHandler.HandleException(ex, "Unable to open the LLM chat window.");
                }
            });
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
