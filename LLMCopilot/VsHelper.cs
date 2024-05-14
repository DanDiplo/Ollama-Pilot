using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace LLMCopilot
{
    public static class VsHelpers
    {
        public static async Task<IWpfTextView> GetActiveTextViewAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var textManager = await serviceProvider.GetServiceAsync<SVsTextManager, IVsTextManager>();
            textManager.GetActiveView(1, null, out IVsTextView vTextView);
            IVsUserData userData = vTextView as IVsUserData;
            if (userData == null)
                return null;

            Guid guidViewHost = DefGuidList.guidIWpfTextViewHost;
            userData.GetData(ref guidViewHost, out object holder);
            IWpfTextViewHost viewHost = (IWpfTextViewHost)holder;
            return viewHost?.TextView;
        }

        public static async Task<string> GetActiveDocumentFileNameAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var textManager = await serviceProvider.GetServiceAsync<SVsTextManager, IVsTextManager>();
            textManager.GetActiveView(1, null, out IVsTextView vTextView);

            if (vTextView != null)
            {
                IVsTextLines textLines;
                vTextView.GetBuffer(out textLines);

                if (textLines is IPersistFileFormat persistFileFormat)
                {
                    persistFileFormat.GetCurFile(out string fullPath, out uint _);
                    return Path.GetFileName(fullPath);
                }
            }

            return null;
        }

        public static void OpenChatWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = ServiceProvider.Package.FindToolWindow(typeof(LLMChatWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }

    public class CommandExecutor
    {
       
        public static async Task PostCommandAsync(IAsyncServiceProvider serviceProvider,  Guid commandGroup, uint commandId)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                IVsUIShell uiShell = await serviceProvider.GetServiceAsync(typeof(SVsUIShell)) as IVsUIShell;
                if (uiShell == null) return;

                int hr = uiShell.PostExecCommand(ref commandGroup, commandId, 0, null);
                ErrorHandler.ThrowOnFailure(hr);
            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex);
            }
        }        
    }
}
