using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections;
using System.ComponentModel.Design;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OllamaPilot
{
    internal sealed class ErrorListFixCommand
    {
        public const int CommandId = 0x0104;

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
            ThreadHelper.JoinableTaskFactory.Run(() => ExecuteAsync());
        }

        private async Task ExecuteAsync()
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

            if (string.IsNullOrWhiteSpace(errorInfo.FilePath))
            {
                var diagnosticPrompt = OllamaHelper.Instance.GetFixDiagnosticTemplate(errorInfo.Description, errorInfo.ProjectName);
                try
                {
                    VsHelpers.OpenChatWindow();
                    EventManager.OnCodeCommandExecuted(
                        $"Help fix this diagnostic in {errorInfo.ProjectName ?? "the current project"}: {errorInfo.Description}",
                        diagnosticPrompt);
                }
                catch (Exception ex)
                {
                    LLMErrorHandler.HandleException(ex, "Unable to open the LLM chat window.");
                }
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
        }

        private static ErrorInfo TryGetSelectedErrorInfo(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                LogErrorListState(dte, "TryGetSelectedErrorInfo");

                var ideSelection = TryGetInfoFromIdeSelection(dte);
                if (ideSelection != null)
                {
                    return ideSelection;
                }

                var errorList = dte.ToolWindows.ErrorList;
                if (errorList == null)
                {
                    return null;
                }

                var selectedItems = errorList.SelectedItems;
                foreach (var item in EnumerateCollection(selectedItems))
                {
                    var info = CreateErrorInfo(item);
                    if (info != null)
                    {
                        return info;
                    }
                }

                var errorItems = errorList.ErrorItems;
                foreach (var item in EnumerateCollection(errorItems))
                {
                    var isSelected = false;
                    try
                    {
                        isSelected = TryGetBool(item, "IsSelected");
                    }
                    catch
                    {
                        // Some providers may not surface IsSelected reliably.
                    }

                    if (!isSelected)
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
                var errorList = dte.ToolWindows.ErrorList;
                if (errorList == null)
                {
                    return null;
                }

                var errorItems = errorList.ErrorItems;
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
                foreach (var item in EnumerateCollection(errorItems))
                {
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

        private static ErrorInfo CreateErrorInfo(object itemObject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(itemObject is EnvDTE80.ErrorItem item))
            {
                item = TryGetProperty(itemObject, "Object") as EnvDTE80.ErrorItem
                    ?? itemObject as EnvDTE80.ErrorItem;
            }

            if (item == null)
            {
                return null;
            }

            string filePath = null;
            string description = null;
            string projectName = null;
            int? line = null;
            int? column = null;

            try { filePath = item.FileName; } catch { }
            try { description = item.Description; } catch { }
            try { projectName = item.Project; } catch { }
            try { line = item.Line; } catch { }
            try { column = item.Column; } catch { }

            if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
            {
                filePath = null;
            }

            if (string.IsNullOrWhiteSpace(filePath) && string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            return new ErrorInfo
            {
                Description = description,
                FilePath = filePath,
                ProjectName = projectName,
                Line = line,
                Column = column
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

                    var candidate = TryGetProperty(selectedItem, "Object") as EnvDTE80.ErrorItem;
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

        private static IEnumerable EnumerateCollection(object collection)
        {
            if (collection == null)
            {
                yield break;
            }

            if (collection is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        yield return item;
                    }
                }

                yield break;
            }

            var count = TryGetCount(collection);
            for (var index = 1; index <= count; index++)
            {
                var item = TryInvokeItem(collection, index);
                if (item != null)
                {
                    yield return item;
                }
            }
        }

        private static void LogErrorListState(DTE2 dte, string source)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{source}: inspecting Error List state.");
                sb.AppendLine($"ActiveWindow: {dte.ActiveWindow?.Caption ?? "<null>"}");
                sb.AppendLine($"ActiveDocument: {dte.ActiveDocument?.FullName ?? "<null>"}");

                var errorList = dte.ToolWindows.ErrorList;
                sb.AppendLine($"ErrorList available: {errorList != null}");
                if (errorList != null)
                {
                    sb.AppendLine($"SelectedItems type: {errorList.SelectedItems?.GetType().FullName ?? "<null>"}");
                    sb.AppendLine($"SelectedItems count: {TryGetCount(errorList.SelectedItems)}");
                    sb.AppendLine($"ErrorItems type: {errorList.ErrorItems?.GetType().FullName ?? "<null>"}");
                    sb.AppendLine($"ErrorItems count: {TryGetCount(errorList.ErrorItems)}");
                }

                sb.AppendLine($"DTE.SelectedItems count: {dte.SelectedItems?.Count ?? 0}");
                LLMErrorHandler.WriteLog(sb.ToString());
            }
            catch
            {
                // Logging should never affect command execution.
            }
        }

        private static object TryGetProperty(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

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
            if (instance == null)
            {
                return null;
            }

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
            if (value == null)
            {
                return 0;
            }

            if (value is int intCount)
            {
                return intCount;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
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

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return null;
            }
        }

        private sealed class ErrorInfo
        {
            public string Description { get; set; }

            public string FilePath { get; set; }

            public string ProjectName { get; set; }

            public int? Line { get; set; }

            public int? Column { get; set; }
        }
    }
}
