using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Threading.Tasks;

namespace OllamaPilot
{
    internal static class SelectedCodeCommandExecutor
    {
        public static async Task ExecuteAsync(AsyncPackage package, Func<string, string, string> createPrompt, GeneratedResponseGuard responseGuard = GeneratedResponseGuard.None, AssistantActionCapabilities assistantActions = AssistantActionCapabilities.Discussion)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            IWpfTextView textView;
            try
            {
                textView = await VsHelpers.GetActiveTextViewAsync(package);
            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex, "Unable to read the active editor.");
                return;
            }

            if (textView == null)
            {
                VsHelpers.ShowInfo("Open a code editor and select some code first.");
                return;
            }

            var selectedText = VsHelpers.GetSelectedText(textView);
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                VsHelpers.ShowInfo("Select some code first.");
                return;
            }

            try
            {
                var fileName = await VsHelpers.GetActiveDocumentFileNameAsync(package) ?? string.Empty;
                var prompt = createPrompt(selectedText, fileName);
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    VsHelpers.ShowError("Unable to build the prompt for the selected action.");
                    return;
                }

                VsHelpers.OpenChatWindow();
                await System.Threading.Tasks.Task.Yield();
                EventManager.OnCodeCommandExecuted(prompt, null, selectedText, responseGuard, assistantActions);
            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex, "Unable to send the selected code to Ollama Pilot.");
            }
        }
    }
}
