using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OllamaPilot.Services.Ollama;
using OllamaPilot.UI.Settings;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace OllamaPilot.Commands
{
    internal sealed class TestConnectionCommand
    {
        public const int CommandId = 0x2021;
        public static readonly Guid CommandSet = new Guid("d9bd5408-e04b-4cd1-95ac-5b6240ab8bd1");
        private readonly AsyncPackage package;

        private TestConnectionCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static TestConnectionCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new TestConnectionCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(() => ExecuteAsync());
        }

        private async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var options = (OptionPageGrid)package.GetDialogPage(typeof(OptionPageGrid));

            var result = await OllamaSettingsValidator.ValidateAsync(options, package.DisposalToken);
            var icon = result.Success ? OLEMSGICON.OLEMSGICON_INFO : OLEMSGICON.OLEMSGICON_WARNING;
            var title = result.Success ? "Ollama Pilot Connection OK" : "Ollama Pilot Connection Failed";

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            VsShellUtilities.ShowMessageBox(
                package,
                result.Message,
                title,
                icon,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
