using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace OllamaPilot
{
    internal sealed class GenerateFileTestsCommand
    {
        /// <summary>
        /// Identifies the command with an ID of 0x0104 in the command set with GUID "d9bd5408-e04b-4cd1-95ac-5b6240ab8bd1".
        /// </summary>
        public const int CommandId = 0x0104;

        /// <summary>
        /// Represents the command set that contains the GenerateFileTestsCommand.
        /// </summary>
        public static readonly Guid CommandSet = new Guid("d9bd5408-e04b-4cd1-95ac-5b6240ab8bd1");

        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes the GenerateFileTestsCommand instance.
        /// </summary>
        /// <param name="package">The async package that contains the command service.</param>
        /// <param name="commandService">The OleMenuCommandService for managing menu commands.</param>
        public GenerateFileTestsCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Represents a static instance of the GenerateFileTestsCommand class.
        /// </summary>
        public static GenerateFileTestsCommand Instance { get; private set; }

        /// <summary>
        /// Initializes the static instance of the GenerateFileTestsCommand class using the provided async package.
        /// </summary>
        /// <param name="package">The async package that contains the command service.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new GenerateFileTestsCommand(package, commandService);
        }

        /// <summary>
        /// Executes the GenerateFileTestsCommand.
        /// </summary>
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
