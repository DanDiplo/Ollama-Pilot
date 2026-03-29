using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System;

namespace OllamaPilot
{
    internal static class SelectedCodeCommandExecutor
    {
        public static void Execute(AsyncPackage package, Func<string, string, string> createPrompt)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ThreadHelper.JoinableTaskFactory.Run(async delegate
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

                var fileName = await VsHelpers.GetActiveDocumentFileNameAsync(package) ?? string.Empty;
                var prompt = createPrompt(selectedText, fileName);
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    VsHelpers.ShowError("Unable to build the prompt for the selected action.");
                    return;
                }

                try
                {
                    VsHelpers.OpenChatWindow();
                    EventManager.OnCodeCommandExecuted(prompt);
                }
                catch (Exception ex)
                {
                    LLMErrorHandler.HandleException(ex, "Unable to open the LLM chat window.");
                }
            });
        }
    }
}
