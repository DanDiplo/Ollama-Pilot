using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using OllamaSharp.Models;
using System.Windows.Threading;

namespace LLMCopilot
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class LLMMenuCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("97b2029e-4a4a-44a2-89bd-a85b80527fb0");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="LLMMenuCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private LLMMenuCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static LLMMenuCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in LLMMenuCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new LLMMenuCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread("Needs to be called on the UI thread.");

            string[] stop = {
                 "<|fim▁begin|>",
                 "<|fim▁hole|>",
                 "<|fim▁end|>",
                 "//",
                 @"\n\n",
                 @"\r\n\r\n"
            };

            var options = new RequestOptions
            {
                NumCtx = 4096,
                NumPredict = 128,
                Stop = stop,
                Temperature = 0.01f
            };

            var request = new GenerateCompletionRequest
            {
                Model = OllamaHelper.Instance.OllamaCompleteClient.SelectedModel,
                Prompt = @"<｜fim▁begin｜>def quick_sort(arr):
                        if len(arr) <= 1:
                            return arr
                        pivot = arr[0]
                        left = []
                        right = []
                        <｜fim▁hole｜>
                            if arr[i] < pivot:
                                left.append(arr[i])
                            else:
                                right.append(arr[i])
                        return quick_sort(left) + [pivot] + quick_sort(right)<｜fim▁end｜>",
                Options = options,
                Raw = true,
            };


            Task.Run(async () =>
            {
                try
                {
                    var result = await OllamaHelper.Instance.OllamaCompleteClient.GetCompletion(request);
                    VsShellUtilities.ShowMessageBox(
                        this.package,
                        result.Response,
                        "错误",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
                catch (Exception ex)
                {
                    LLMErrorHandler.HandleException(ex);
                }
            });


        }

    }

}

