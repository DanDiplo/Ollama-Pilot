using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace OllamaPilot
{
    internal sealed class SummarizeChangesCommand
    {
        public const int CommandId = 0x2022;
        public static readonly Guid CommandSet = new Guid("d9bd5408-e04b-4cd1-95ac-5b6240ab8bd1");

        private readonly AsyncPackage package;

        private SummarizeChangesCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static SummarizeChangesCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new SummarizeChangesCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var changesContext = await GitContextHelper.TryGetChangesContextAsync(this.package);
                if (changesContext == null)
                {
                    VsHelpers.ShowInfo("No Git changes were found from the current solution or document context.");
                    return;
                }

                var prompt = OllamaHelper.Instance.GetSummarizeChangesTemplate(
                    changesContext.RepositoryRoot,
                    changesContext.StatusText,
                    changesContext.DiffText);

                var repoName = Path.GetFileName(changesContext.RepositoryRoot);
                VsHelpers.OpenChatWindow();
                EventManager.OnCodeCommandExecuted($"Summarize the current Git changes for {repoName}.", prompt);
            });
        }
    }
}
