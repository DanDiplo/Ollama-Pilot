using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OllamaPilot.Commands;
using OllamaPilot.Infrastructure;
using OllamaPilot.Services.Ollama;
using OllamaPilot.UI.Chat;
using OllamaPilot.UI.Settings;
using Task = System.Threading.Tasks.Task;

namespace OllamaPilot.Package
{
    public static class LLMCopilotProvider
    {
        public static async Task EnsurePackageLoadedAsync()
        {
            if (Package != null)
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (await Microsoft.VisualStudio.Shell.ServiceProvider.GetGlobalServiceAsync(typeof(SVsShell)) is IVsShell shell)
            {
                var packageGuid = new Guid(LLMCopilotPackage.PackageGuidString);
                shell.LoadPackage(ref packageGuid, out IVsPackage _);
            }
        }

        public static AsyncPackage Package { get; set; }
    }

    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(LLMCopilotPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(LLMChatWindow))]
    [ProvideOptionPage(typeof(OptionPageGrid),
    "LLMCopilot", "常规", 0, 0, true)]
    [ProvideAutoLoad(UIContextGuids.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]  // 当没有打开解决方案时也加载包
    public sealed class LLMCopilotPackage : AsyncPackage
    {
        /// <summary>
        /// LLMCopilotPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "93399514-383b-4900-9615-9f4ad8b809e6";

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            LLMCopilotProvider.Package = this;
            LLMErrorHandler.Initialize(this);
            StartOllamaModelWarmup();
            await ExplainCommand.InitializeAsync(this);
            await ExplainErrorCommand.InitializeAsync(this);
            await ReviewFileCommand.InitializeAsync(this);
            await GenerateFileTestsCommand.InitializeAsync(this);
            await ErrorListFixCommand.InitializeAsync(this);
            await LLMChatWindowCommand.InitializeAsync(this);
            await CodeCompleteCommand.InitializeAsync(this);
            await FindBugCommand.InitializeAsync(this);
            await OptimizeCodeCommand.InitializeAsync(this);
            await AddCommentCommand.InitializeAsync(this);
            await UnitTestCommand.InitializeAsync(this);
            await SettingsCommand.InitializeAsync(this);
            await TestConnectionCommand.InitializeAsync(this);
            await SummarizeChangesCommand.InitializeAsync(this);
        }

        [SuppressMessage("Usage", "VSSDK007:Await/join tasks created from ThreadHelper.JoinableTaskFactory.RunAsync", Justification = "Ollama model metadata warmup should not delay VSIX startup.")]
        private void StartOllamaModelWarmup()
        {
            _ = this.JoinableTaskFactory.RunAsync(async delegate
            {
                await Task.Run(() => OllamaHelper.Instance.InitModelCtxAsync());
            });
        }

        #endregion
    }
}
