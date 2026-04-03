using Microsoft.VisualStudio.Shell;
using OllamaPilot.Commands.Executors;
using OllamaPilot.Services.Ollama;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace OllamaPilot.Commands
{
    internal sealed class ReviewFileCommand
    {
        public const int CommandId = 0x0102;
        public static readonly Guid CommandSet = new Guid("d9bd5408-e04b-4cd1-95ac-5b6240ab8bd1");

        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewFileCommand"/> class.
        /// </summary>
        /// <param name="package">The asynchronous package that owns this command.</param>
        /// <param name="commandService">The OLE menu command service to register the command with.</param>
        private ReviewFileCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            // Ensure the package is not null.
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            // Ensure the command service is not null.
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            // Define the command ID using the globally unique identifier (GUID) and the command's unique identifier.
            var menuCommandID = new CommandID(CommandSet, CommandId);
            // Create a new menu command instance, associating it with the Execute method.
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            // Add the newly created menu item to the command service, making it visible in the Visual Studio UI.
            commandService.AddCommand(menuItem);
        }

        public static ReviewFileCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ReviewFileCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ThreadHelper.JoinableTaskFactory.Run(() => CurrentDocumentCommandExecutor.ExecuteAsync(
                this.package,
                OllamaHelper.Instance.GetReviewFileTemplate,
                fileName => $"Review the current file: {fileName}",
                "Open a code file first.",
                DocumentPromptProfile.Review,
                AssistantActionCapabilities.Discussion));
        }
    }
}
