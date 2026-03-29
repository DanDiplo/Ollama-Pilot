using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace OllamaPilot
{
    internal sealed class ExplainErrorCommand
    {
        public const int CommandId = 0x0101;

        public static readonly Guid CommandSet = new Guid("d9bd5408-e04b-4cd1-95ac-5b6240ab8bd1");

        private readonly AsyncPackage package;

        private ExplainErrorCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static ExplainErrorCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ExplainErrorCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

                var textView = await VsHelpers.GetActiveTextViewAsync(package);
                if (textView == null)
                {
                    VsHelpers.ShowInfo("Open a code editor first.");
                    return;
                }

                var selectedText = VsHelpers.GetSelectedText(textView);
                var codeContext = !string.IsNullOrWhiteSpace(selectedText)
                    ? selectedText
                    : VsHelpers.GetContextAroundCaret(textView, 6, 6);

                if (string.IsNullOrWhiteSpace(codeContext))
                {
                    VsHelpers.ShowInfo("Select code or place the caret on the problem line first.");
                    return;
                }

                var currentLine = VsHelpers.GetCurrentLineText(textView);
                var fileName = await VsHelpers.GetActiveDocumentFileNameAsync(package) ?? string.Empty;
                var prompt = OllamaHelper.Instance.GetDiagnoseErrorsTemplate(codeContext, fileName, currentLine);
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    VsHelpers.ShowError("Unable to build the diagnostic prompt.");
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
