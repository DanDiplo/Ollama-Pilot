using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace LLMCopilot
{
    internal sealed class GenerateFileTestsCommand
    {
        public const int CommandId = 0x0104;
        public static readonly Guid CommandSet = new Guid("97b2029e-4a4a-44a2-89bd-a85b80527fb0");

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
