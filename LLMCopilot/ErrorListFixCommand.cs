using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace OllamaPilot
{
    internal sealed class ErrorListFixCommand
    {
        public const int CommandId = 0x0102;

        public static readonly Guid CommandSet = new Guid("d9bd5408-e04b-4cd1-95ac-5b6240ab8bd1");

        private readonly AsyncPackage package;

        private ErrorListFixCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static ErrorListFixCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ErrorListFixCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

                var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;
                if (dte == null)
                {
                    VsHelpers.ShowError("Unable to access the Visual Studio error list.");
                    return;
                }

                var errorInfo = TryGetSelectedErrorInfo(dte) ?? TryGetFallbackErrorInfo(dte);
                if (errorInfo == null)
                {
                    VsHelpers.ShowInfo("Select an item in the Error List first.");
                    return;
                }

                VsHelpers.OpenFileAndSelectContext(dte, errorInfo.FilePath, errorInfo.Line, errorInfo.Column, 6, 6);
                var codeContext = VsHelpers.GetFileContext(errorInfo.FilePath, errorInfo.Line, 6, 6);
                if (string.IsNullOrWhiteSpace(codeContext))
                {
                    VsHelpers.ShowInfo("Unable to read code around the selected error.");
                    return;
                }

                var prompt = OllamaHelper.Instance.GetFixErrorTemplate(
                    codeContext,
                    errorInfo.FilePath,
                    errorInfo.Description,
                    errorInfo.Line,
                    errorInfo.Column);

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    VsHelpers.ShowError("Unable to build the error-fix prompt.");
                    return;
                }

                try
                {
                    VsHelpers.OpenChatWindow();
                    EventManager.OnCodeCommandExecuted(prompt);
                }
                catch (Exception ex)
                {
                    LLMErrorHandler.HandleException(ex, "Unable to open the LLM chat window.");
                }
            });
        }

        private static ErrorInfo TryGetSelectedErrorInfo(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var ideSelection = TryGetInfoFromIdeSelection(dte);
                if (ideSelection != null)
                {
                    return ideSelection;
                }

                dynamic errorList = dte.ToolWindows.ErrorList;
                var selectedItems = TryGetProperty(errorList, "SelectedItems");
                var count = TryGetCount(selectedItems);
                if (count > 0)
                {
                    for (var index = 1; index <= count; index++)
                    {
                        var item = TryInvokeItem(selectedItems, index);
                        var info = CreateErrorInfo(item);
                        if (info != null)
                        {
                            return info;
                        }
                    }
                }

                var errorItems = TryGetProperty(errorList, "ErrorItems");
                var errorCount = TryGetCount(errorItems);
                for (var index = 1; index <= errorCount; index++)
                {
                    var item = TryInvokeItem(errorItems, index);
                    if (!TryGetBool(item, "IsSelected"))
                    {
                        continue;
                    }

                    var info = CreateErrorInfo(item);
                    if (info != null)
                    {
                        return info;
                    }
                }
            }
            catch
            {
                // Fall through to alternate resolution paths.
            }

            return null;
        }

        private static ErrorInfo TryGetFallbackErrorInfo(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                dynamic errorList = dte.ToolWindows.ErrorList;
                var errorItems = TryGetProperty(errorList, "ErrorItems");
                var errorCount = TryGetCount(errorItems);
                if (errorCount <= 0)
                {
                    return null;
                }

                string activeDocument = null;
                int? activeLine = null;
                if (dte.ActiveDocument != null)
                {
                    activeDocument = dte.ActiveDocument.FullName;
                    if (dte.ActiveDocument.Selection is TextSelection selection)
                    {
                        activeLine = selection.ActivePoint.Line;
                    }
                }

                ErrorInfo bestMatch = null;
                for (var index = 1; index <= errorCount; index++)
                {
                    var item = TryInvokeItem(errorItems, index);
                    var info = CreateErrorInfo(item);
                    if (info == null)
                    {
                        continue;
                    }

                    if (bestMatch == null)
                    {
                        bestMatch = info;
                    }

                    if (string.IsNullOrWhiteSpace(activeDocument))
                    {
                        continue;
                    }

                    if (!string.Equals(
                        Path.GetFullPath(info.FilePath ?? string.Empty),
                        Path.GetFullPath(activeDocument),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!activeLine.HasValue || !info.Line.HasValue || Math.Abs(info.Line.Value - activeLine.Value) <= 2)
                    {
                        return info;
                    }
                }

                return bestMatch;
            }
            catch
            {
                return null;
            }
        }

        private static ErrorInfo CreateErrorInfo(object item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (item == null)
            {
                return null;
            }

            var filePath = TryGetString(item, "FileName");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                filePath = TryGetString(item, "Document");
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            return new ErrorInfo
            {
                Description = TryGetString(item, "Description"),
                FilePath = filePath,
                Line = TryGetInt(item, "Line"),
                Column = TryGetInt(item, "Column")
            };
        }

        private static ErrorInfo TryGetInfoFromIdeSelection(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (dte.SelectedItems == null || dte.SelectedItems.Count <= 0)
                {
                    return null;
                }

                for (var index = 1; index <= dte.SelectedItems.Count; index++)
                {
                    var selectedItem = dte.SelectedItems.Item(index);
                    if (selectedItem == null)
                    {
                        continue;
                    }

                    var candidate = TryGetProperty(selectedItem, "Object");
                    var info = CreateErrorInfo(candidate);
                    if (info != null)
                    {
                        return info;
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static object TryGetProperty(object instance, string propertyName)
        {
            try
            {
                return instance.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.GetProperty,
                    null,
                    instance,
                    null);
            }
            catch
            {
                return null;
            }
        }

        private static object TryInvokeItem(object instance, int index)
        {
            try
            {
                return instance.GetType().InvokeMember(
                    "Item",
                    BindingFlags.InvokeMethod,
                    null,
                    instance,
                    new object[] { index });
            }
            catch
            {
                return null;
            }
        }

        private static int TryGetCount(object instance)
        {
            var value = TryGetProperty(instance, "Count");
            return value is int count ? count : 0;
        }

        private static bool TryGetBool(object instance, string propertyName)
        {
            var value = TryGetProperty(instance, propertyName);
            return value is bool boolValue && boolValue;
        }

        private static string TryGetString(object instance, string propertyName)
        {
            var value = TryGetProperty(instance, propertyName);
            return value?.ToString();
        }

        private static int? TryGetInt(object instance, string propertyName)
        {
            var value = TryGetProperty(instance, propertyName);
            if (value == null)
            {
                return null;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (int.TryParse(value.ToString(), out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private sealed class ErrorInfo
        {
            public string Description { get; set; }

            public string FilePath { get; set; }

            public int? Line { get; set; }

            public int? Column { get; set; }
        }
    }
}
