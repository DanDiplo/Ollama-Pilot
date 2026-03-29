using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace OllamaPilot
{
    internal sealed class GenerateFileTestsCommand
    {
        public const int CommandId = 0x0104;
        public static readonly Guid CommandSet = new Guid("d9bd5408-e04b-4cd1-95ac-5b6240ab8bd1");

        private readonly AsyncPackage package;

        private GenerateFileTestsCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static GenerateFileTestsCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new GenerateFileTestsCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            CurrentDocumentCommandExecutor.Execute(
                this.package,
                OllamaHelper.Instance.GetGenerateFileTestsTemplate,
                fileName => $"Generate tests for the current file: {fileName}",
                "Open a code file first.");
        }
    }
}
