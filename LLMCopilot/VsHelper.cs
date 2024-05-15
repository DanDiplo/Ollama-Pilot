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

        public static async Task<IVsTextLines> GetActiveTextLinesAsync(IAsyncServiceProvider serviceProvider)
        {
            var textView = await GetActiveTextViewAsync(serviceProvider);
            var textManager = await serviceProvider.GetServiceAsync<SVsTextManager, IVsTextManager>();
            textManager.GetActiveView(1, null, out IVsTextView vTextView);
            if (vTextView != null)
            {
                IVsTextLines textLines;
                vTextView.GetBuffer(out textLines);
                return textLines;
            }
            return null;
        }

        public static async Task<string> GetPrefixLinesAsync(IAsyncServiceProvider serviceProvider, int n)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var textView = await GetActiveTextViewAsync(serviceProvider);

            if (textView == null)
            {
                return null;
            }

            var caretPosition = textView.Caret.Position.BufferPosition;
            var snapshot = caretPosition.Snapshot;

            var startLine = snapshot.GetLineNumberFromPosition(caretPosition.Position);
            var startLineIndex = Math.Max(0, startLine - n);
            var endLineIndex = startLine;

            var sb = new System.Text.StringBuilder();

            for (int i = startLineIndex; i <= endLineIndex; i++)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                sb.AppendLine(line.GetText());
            }

            return sb.ToString();
        }

        public static async Task<string> GetSuffixLinesAsync(IAsyncServiceProvider serviceProvider, int n)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var textView = await GetActiveTextViewAsync(serviceProvider);

            if (textView == null)
            {
                return null;
            }

            var caretPosition = textView.Caret.Position.BufferPosition;
            var snapshot = caretPosition.Snapshot;

            var startLine = snapshot.GetLineNumberFromPosition(caretPosition.Position);
            var startLineIndex = startLine;
            var endLineIndex = Math.Min(snapshot.LineCount - 1, startLine + n);

            var sb = new System.Text.StringBuilder();

            for (int i = startLineIndex; i <= endLineIndex; i++)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                sb.AppendLine(line.GetText());
            }

            return sb.ToString();
        }

        public static string GetSourceCodeType(string fileName)
        {
            var extension = System.IO.Path.GetExtension(fileName).ToLower();
            switch (extension)
            {
                case ".py":
                    return "python";
                case ".cpp":
                case ".c":
                case ".h":
                    return "cpp";
                case ".cs":
                    return "csharp";
                case ".js":
                    return "javascript";
                case ".html":
                case ".htm":
                    return "html";
                case ".css":
                    return "css";
                case ".java":
                    return "java";
                case ".ts":
                    return "typescript";
                case ".json":
                    return "json";
                case ".xml":
                    return "xml";
                case ".sql":
                    return "sql";
                case ".rb":
                    return "ruby";
                case ".php":
                    return "php";
                case ".swift":
                    return "swift";
                case ".go":
                    return "go";
                case ".rs":
                    return "rust";
                case ".kt":
                case ".kts":
                    return "kotlin";
                case ".sh":
                    return "bash";
                case ".bat":
                    return "batch";
                case ".md":
                    return "markdown";
                case ".r":
                    return "r";
                case ".pl":
                    return "perl";
                case ".lua":
                    return "lua";
                // Add more cases as needed
                default:
                    return "plaintext"; // Default to plaintext for unknown types
            }
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
