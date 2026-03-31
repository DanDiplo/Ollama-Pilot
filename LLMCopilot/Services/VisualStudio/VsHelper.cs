using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using OllamaPilot.Infrastructure;
using OllamaPilot.Package;
using OllamaPilot.Services.Ollama;
using OllamaPilot.UI.Chat;
using OllamaPilot.UI.Settings;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace OllamaPilot.Services.VisualStudio
{
    public static class VsHelpers
    {
        private static readonly IOllamaService ollamaService = new OllamaSharpService();
        private static int _isSending;
        public static bool IsSending => Volatile.Read(ref _isSending) == 1;
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static int _completionRequestVersion;

        public static void CancelPendingCompletion()
        {
            Interlocked.Increment(ref _completionRequestVersion);
            ReplaceCompletionCancellationToken(cancelPrevious: true);
        }

        public static void SetSendingState(bool isSending)
        {
            Interlocked.Exchange(ref _isSending, isSending ? 1 : 0);
        }

        public static async Task<IWpfTextView> GetActiveTextViewAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return await GetActiveTextViewOnUiThreadAsync(serviceProvider);
        }

        /// <summary>
        /// Retrieves the file name of the currently active document.
        /// Returns null if no active document or its path is empty.
        /// </summary>
        public static async Task<string> GetActiveDocumentFileNameAsync(IAsyncServiceProvider serviceProvider)
        {
            // Asynchronously obtain the full path of the active document.
            var fullPath = await GetActiveDocumentPathAsync(serviceProvider);
            // Return the file name if the path is valid; otherwise, return null.
            return string.IsNullOrWhiteSpace(fullPath) ? null : Path.GetFileName(fullPath);
        }

        public static async Task<string> GetActiveDocumentPathAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var textManager = await GetTextManagerAsync(serviceProvider);
            if (textManager != null)
            {
                var fullPath = TryGetActiveDocumentPathFromTextManager(textManager);
                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    return fullPath;
                }
            }

            return await GetOpenDocumentPathOnUiThreadAsync(serviceProvider);
        }

        public static async Task<string> GetActiveDocumentTextAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var textView = await GetActiveTextViewOnUiThreadAsync(serviceProvider);
            if (textView != null)
            {
                var snapshotText = textView.TextSnapshot?.GetText();
                if (!string.IsNullOrWhiteSpace(snapshotText))
                {
                    return snapshotText;
                }
            }

            return await GetOpenDocumentTextOnUiThreadAsync(serviceProvider);
        }

        public static string GetSelectedText(IWpfTextView textView)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (textView == null || textView.Selection == null || textView.Selection.IsEmpty)
            {
                return null;
            }

            return textView.Selection.StreamSelectionSpan.GetText();
        }

        public static string GetCurrentLinePrefix(IWpfTextView textView)
        {
            if (textView == null)
            {
                return string.Empty;
            }

            var caretPosition = textView.Caret.Position.BufferPosition;
            var currentLine = caretPosition.GetContainingLine();
            return currentLine.GetText().Substring(0, caretPosition.Position - currentLine.Start.Position);
        }

        public static string GetCurrentLineText(IWpfTextView textView)
        {
            if (textView == null)
            {
                return string.Empty;
            }

            var caretPosition = textView.Caret.Position.BufferPosition;
            return caretPosition.GetContainingLine().GetText();
        }

        public static string GetContextAroundCaret(IWpfTextView textView, int linesBefore, int linesAfter)
        {
            if (textView == null)
            {
                return string.Empty;
            }

            var caretPosition = textView.Caret.Position.BufferPosition;
            var snapshot = caretPosition.Snapshot;
            var currentLine = snapshot.GetLineFromPosition(caretPosition);
            var startLine = Math.Max(0, currentLine.LineNumber - Math.Max(0, linesBefore));
            var endLine = Math.Min(snapshot.LineCount - 1, currentLine.LineNumber + Math.Max(0, linesAfter));
            var builder = new StringBuilder();

            for (var lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                builder.AppendLine(snapshot.GetLineFromLineNumber(lineNumber).GetText());
            }

            return builder.ToString().TrimEnd();
        }

        public static string GetFileContext(string filePath, int? lineNumber, int linesBefore, int linesAfter)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return string.Empty;
            }

            string[] allLines;
            try
            {
                allLines = File.ReadLines(filePath).ToArray();
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }

            if (allLines.Length == 0)
            {
                return string.Empty;
            }

            if (!lineNumber.HasValue || lineNumber.Value <= 0)
            {
                return string.Join(Environment.NewLine, allLines.Take(Math.Min(allLines.Length, linesBefore + linesAfter + 1)));
            }

            var zeroBasedLine = Math.Max(0, lineNumber.Value - 1);
            var startLine = Math.Max(0, zeroBasedLine - Math.Max(0, linesBefore));
            var endLine = Math.Min(allLines.Length - 1, zeroBasedLine + Math.Max(0, linesAfter));
            var builder = new StringBuilder();

            for (var index = startLine; index <= endLine; index++)
            {
                builder.AppendLine(allLines[index]);
            }

            return builder.ToString().TrimEnd();
        }

        public static void OpenFileAndSelectContext(DTE2 dte, string filePath, int? lineNumber, int? columnNumber, int linesBefore, int linesAfter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (dte == null || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            Window window = dte.ItemOperations.OpenFile(filePath);
            window?.Activate();

            if (!(dte.ActiveDocument?.Selection is TextSelection selection))
            {
                return;
            }

            var startLine = Math.Max(1, (lineNumber ?? 1) - Math.Max(0, linesBefore));
            var endLine = Math.Max(startLine, (lineNumber ?? startLine) + Math.Max(0, linesAfter));
            var startColumn = Math.Max(1, columnNumber ?? 1);

            selection.MoveToLineAndOffset(startLine, startColumn, false);
            selection.MoveToLineAndOffset(endLine, 1, true);
            selection.EndOfLine(true);
        }

        public static bool HasSufficientPrefix(IWpfTextView textView, int minimumPrefixLength)
        {
            if (textView == null)
            {
                return false;
            }

            if (minimumPrefixLength <= 0)
            {
                return true;
            }

            var prefix = GetCurrentLinePrefix(textView);
            return prefix.Count(c => !char.IsWhiteSpace(c)) >= minimumPrefixLength;
        }

        public static bool ShouldTriggerAutoComplete(IWpfTextView textView, char? typedChar, OptionPageGrid options, uint nCmdID)
        {
            if (textView == null || options == null || !options.EnableAutoComplete)
            {
                return false;
            }

            if (textView.Selection != null && !textView.Selection.IsEmpty)
            {
                return false;
            }

            var triggerMode = options.AutoCompleteTriggerMode;
            if (triggerMode == AutoCompleteTriggerMode.ManualOnly)
            {
                return nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB;
            }

            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB)
            {
                return true;
            }

            if (!HasSufficientPrefix(textView, options.AutoCompleteMinPrefixLength))
            {
                return false;
            }

            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN)
            {
                return triggerMode == AutoCompleteTriggerMode.Aggressive;
            }

            if (nCmdID != (uint)VSConstants.VSStd2KCmdID.TYPECHAR || !typedChar.HasValue)
            {
                return false;
            }

            var completionTriggerChars = ".>:=([{,";
            return completionTriggerChars.IndexOf(typedChar.Value) >= 0;
        }

        public static async Task<IVsTextLines> GetActiveTextLinesAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var textManager = await GetTextManagerAsync(serviceProvider);
            if (textManager == null)
            {
                return null;
            }

            IVsTextView vTextView;
            if (ErrorHandler.Failed(textManager.GetActiveView(1, null, out vTextView)))
            {
                return null;
            }

            if (vTextView != null)
            {
                IVsTextLines textLines;
                if (ErrorHandler.Failed(vTextView.GetBuffer(out textLines)))
                {
                    return null;
                }

                return textLines;
            }
            return null;
        }

        public static Task<bool> InsertTextIntoActiveDocumentAsync(IAsyncServiceProvider serviceProvider, string text)
        {
            return ApplyTextToActiveDocumentAsync(serviceProvider, text, replaceSelection: false, formatInsertedText: true);
        }

        public static Task<bool> ReplaceSelectionInActiveDocumentAsync(IAsyncServiceProvider serviceProvider, string text)
        {
            return ApplyTextToActiveDocumentAsync(serviceProvider, text, replaceSelection: true, formatInsertedText: true);
        }

        public static Task<bool> ReplaceActiveDocumentAsync(IAsyncServiceProvider serviceProvider, string text)
        {
            return ApplyTextToActiveDocumentAsync(serviceProvider, text, replaceSelection: false, replaceAll: true);
        }

        public static async Task<string> CreateSiblingFileFromActiveDocumentAsync(IAsyncServiceProvider serviceProvider, string text)
        {
            if (serviceProvider == null || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var activeDocumentPath = await GetActiveDocumentPathAsync(serviceProvider);
            if (string.IsNullOrWhiteSpace(activeDocumentPath))
            {
                return null;
            }

            var siblingPath = BuildSiblingFilePath(activeDocumentPath, text);
            await Task.Run(() => File.WriteAllText(siblingPath, text, Encoding.UTF8));

            var dte = await serviceProvider.GetServiceAsync(typeof(SDTE)) as DTE2;
            dte?.ItemOperations.OpenFile(siblingPath);
            return siblingPath;
        }

        private static async Task<bool> ApplyTextToActiveDocumentAsync(IAsyncServiceProvider serviceProvider, string text, bool replaceSelection, bool replaceAll = false, bool formatInsertedText = false)
        {
            if (serviceProvider == null || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var textView = await GetActiveTextViewAsync(serviceProvider);
            if (textView == null)
            {
                return false;
            }

            if (replaceSelection && (textView.Selection == null || textView.Selection.IsEmpty))
            {
                return false;
            }

            var originalSnapshot = textView.TextSnapshot;
            ITextSnapshot appliedSnapshot;
            int caretPosition;
            int selectionStart;
            int selectionLength;
            using (var edit = textView.TextBuffer.CreateEdit())
            {
                if (replaceSelection)
                {
                    var selectionSpan = textView.Selection.StreamSelectionSpan.SnapshotSpan;
                    selectionStart = selectionSpan.Start.Position;
                    selectionLength = text.Length;
                    caretPosition = selectionSpan.Start.Position + text.Length;
                    edit.Replace(selectionSpan, text);
                }
                else if (replaceAll)
                {
                    var fullSpan = new Span(0, textView.TextSnapshot.Length);
                    selectionStart = 0;
                    selectionLength = text.Length;
                    caretPosition = text.Length;
                    edit.Replace(fullSpan, text);
                }
                else
                {
                    var insertionPoint = textView.Caret.Position.BufferPosition.Position;
                    selectionStart = insertionPoint;
                    selectionLength = text.Length;
                    caretPosition = insertionPoint + text.Length;
                    edit.Insert(insertionPoint, text);
                }

                appliedSnapshot = edit.Apply();
            }

            if (appliedSnapshot == null)
            {
                return false;
            }

            var boundedCaretPosition = Math.Max(0, Math.Min(caretPosition, appliedSnapshot.Length));
            textView.Caret.MoveTo(new SnapshotPoint(appliedSnapshot, boundedCaretPosition));
            var finalSelectionStart = selectionStart;
            var finalSelectionLength = selectionLength;
            var trackingSelectionSpan = CreateTrackingSpan(originalSnapshot, selectionStart, selectionLength);
            textView.Selection.Clear();

            if (formatInsertedText && selectionLength > 0)
            {
                var boundedSelectionStart = Math.Max(0, Math.Min(selectionStart, appliedSnapshot.Length));
                var boundedSelectionLength = Math.Max(0, Math.Min(selectionLength, appliedSnapshot.Length - boundedSelectionStart));
                if (boundedSelectionLength > 0)
                {
                    textView.Selection.Select(
                        new SnapshotSpan(new SnapshotPoint(appliedSnapshot, boundedSelectionStart), boundedSelectionLength),
                        false);

                    await TryFormatSelectionAsync(serviceProvider);

                    var currentSnapshot = textView.TextSnapshot;
                    var updatedSelectionSpan = GetTrackedSelectionSpan(currentSnapshot, trackingSelectionSpan, boundedSelectionStart, boundedSelectionLength);
                    var updatedSelectionStart = updatedSelectionSpan.Start.Position;
                    var updatedSelectionLength = updatedSelectionSpan.Length;
                    var updatedCaretPosition = Math.Max(0, Math.Min(updatedSelectionStart + updatedSelectionLength, currentSnapshot.Length));
                    finalSelectionStart = updatedSelectionStart;
                    finalSelectionLength = updatedSelectionLength;
                    textView.Caret.MoveTo(new SnapshotPoint(currentSnapshot, updatedCaretPosition));
                }
            }

            var finalSnapshot = textView.TextSnapshot;
            var revealStart = Math.Max(0, Math.Min(finalSelectionStart, finalSnapshot.Length));
            var revealLength = Math.Max(0, Math.Min(finalSelectionLength, finalSnapshot.Length - revealStart));

            if (replaceSelection && revealLength > 0)
            {
                textView.Selection.Select(
                    new SnapshotSpan(new SnapshotPoint(finalSnapshot, revealStart), revealLength),
                    false);
            }
            else
            {
                textView.Selection.Clear();
            }

            var revealSpan = new SnapshotSpan(
                new SnapshotPoint(finalSnapshot, replaceSelection ? revealStart : Math.Max(0, Math.Min(boundedCaretPosition, finalSnapshot.Length))),
                replaceSelection ? revealLength : 0);
            textView.ViewScroller.EnsureSpanVisible(revealSpan);
            await ActivateEditorAsync(serviceProvider, textView);
            return true;
        }

        private static async Task ActivateEditorAsync(IAsyncServiceProvider serviceProvider, IWpfTextView textView)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var dte = await serviceProvider.GetServiceAsync(typeof(SDTE)) as DTE2;
                dte?.ActiveDocument?.Activate();
                dte?.ActiveWindow?.Activate();
            }
            catch (Exception ex)
            {
                LLMErrorHandler.WriteLog($"ActivateEditorAsync window activation failed: {ex}");
            }

            try
            {
                textView?.VisualElement?.Focus();
            }
            catch (Exception ex)
            {
                LLMErrorHandler.WriteLog($"ActivateEditorAsync focus failed: {ex}");
            }
        }

        private static async Task<string> GetOpenDocumentPathOnUiThreadAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var dte = await serviceProvider.GetServiceAsync(typeof(SDTE)) as DTE2;
                if (dte == null)
                {
                    return null;
                }

                if (dte?.ActiveDocument != null && !string.IsNullOrWhiteSpace(dte.ActiveDocument.FullName))
                {
                    return dte.ActiveDocument.FullName;
                }
            }
            catch (Exception ex)
            {
                LLMErrorHandler.WriteLog($"GetOpenDocumentPathAsync fallback failed: {ex}");
            }

            return null;
        }

        private static async Task<string> GetOpenDocumentTextOnUiThreadAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var dte = await serviceProvider.GetServiceAsync(typeof(SDTE)) as DTE2;
                if (dte == null)
                {
                    return null;
                }

                if (dte?.ActiveDocument?.Object("TextDocument") is TextDocument textDocument)
                {
                    var startPoint = textDocument.StartPoint.CreateEditPoint();
                    return startPoint.GetText(textDocument.EndPoint);
                }
            }
            catch (Exception ex)
            {
                LLMErrorHandler.WriteLog($"GetOpenDocumentTextAsync fallback failed: {ex}");
            }

            return null;
        }

        private static async Task TryFormatSelectionAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var dte = await serviceProvider.GetServiceAsync(typeof(SDTE)) as DTE2;
                dte?.ExecuteCommand("Edit.FormatSelection");
            }
            catch (Exception ex)
            {
                LLMErrorHandler.WriteLog($"TryFormatSelectionAsync failed: {ex}");
            }
        }

        private static string BuildSiblingFilePath(string activeDocumentPath, string content)
        {
            var directory = Path.GetDirectoryName(activeDocumentPath);
            var extension = Path.GetExtension(activeDocumentPath);
            var baseName = Path.GetFileNameWithoutExtension(activeDocumentPath);
            var preferredName = GetPreferredSiblingFileName(baseName, extension, content);
            var candidatePath = Path.Combine(directory, preferredName);

            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }

            var numberedBase = Path.GetFileNameWithoutExtension(preferredName);
            var numberedExtension = Path.GetExtension(preferredName);
            for (var index = 2; index < 1000; index++)
            {
                var numberedCandidate = Path.Combine(directory, $"{numberedBase}.{index}{numberedExtension}");
                if (!File.Exists(numberedCandidate))
                {
                    return numberedCandidate;
                }
            }

            return Path.Combine(directory, $"{numberedBase}.{DateTime.Now:yyyyMMddHHmmss}{numberedExtension}");
        }

        private static string GetPreferredSiblingFileName(string baseName, string extension, string content)
        {
            if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
            {
                return ContentLooksLikeTests(content)
                    ? $"{baseName}.Tests.cs"
                    : $"{baseName}.Generated.cs";
            }

            if (string.Equals(extension, ".ts", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".tsx", StringComparison.OrdinalIgnoreCase))
            {
                return ContentLooksLikeTests(content)
                    ? $"{baseName}.test{extension}"
                    : $"{baseName}.generated{extension}";
            }

            if (string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".jsx", StringComparison.OrdinalIgnoreCase))
            {
                return ContentLooksLikeTests(content)
                    ? $"{baseName}.test{extension}"
                    : $"{baseName}.generated{extension}";
            }

            if (string.Equals(extension, ".py", StringComparison.OrdinalIgnoreCase))
            {
                return ContentLooksLikeTests(content)
                    ? $"test_{baseName}.py"
                    : $"{baseName}_generated.py";
            }

            return $"{baseName}.generated{extension}";
        }

        private static bool ContentLooksLikeTests(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            return Regex.IsMatch(content, @"\b[Test(Method|Case)?]\b|\bFact\b|\bTheory\b|\bAssert\.", RegexOptions.IgnoreCase)
                || Regex.IsMatch(content, @"\bdescribe\(|\bit\(|\btest\(", RegexOptions.IgnoreCase)
                || Regex.IsMatch(content, @"\bpytest\b|\bassert\b", RegexOptions.IgnoreCase);
        }

        public static string RemoveCommonSuffixPrefix(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return a;
            }

            b = b.TrimStart();
            int minLen = Math.Min(a.Length, b.Length);

            int commonLength = 0;
            for (int i = 1; i <= minLen; i++)
            {
                bool isMatch = true;
                int start = a.Length - i;
                for (int j = 0; j < i; j++)
                {
                    if (a[start + j] != b[j])
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    commonLength = i;
                }
            }

            if (commonLength > 0)
            {
                return a.Substring(0, a.Length - commonLength);
            }

            return a;
        }

        public static string GetPrefixLines(IWpfTextView textView, int n)
        {
            if (textView == null)
            {
                return null;
            }

            var caretPosition = textView.Caret.Position.BufferPosition;
            var snapshot = caretPosition.Snapshot;

            var currentLine = snapshot.GetLineFromPosition(caretPosition);
            var currentLineText = currentLine.GetText().Substring(0, caretPosition.Position - currentLine.Start.Position);

            var startLineIndex = Math.Max(0, currentLine.LineNumber - (n - 1));

            var sb = new StringBuilder();

            for (int i = startLineIndex; i < currentLine.LineNumber; i++)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                sb.AppendLine(line.GetText());
            }

            // Append the current line up to the caret position
            sb.Append(currentLineText);

            return sb.ToString();
        }


        public static string GetSuffixLines(IWpfTextView textView, int n)
        {
            if (textView == null)
            {
                return null;
            }

            var caretPosition = textView.Caret.Position.BufferPosition;
            var snapshot = caretPosition.Snapshot;

            var currentLine = snapshot.GetLineFromPosition(caretPosition);
            var currentLineText = currentLine.GetText().Substring(caretPosition.Position - currentLine.Start.Position);

            var endLineIndex = Math.Min(snapshot.LineCount - 1, currentLine.LineNumber + (n - 1));

            var sb = new StringBuilder();

            // Append the current line from caret position to the end
            sb.AppendLine(currentLineText);

            for (int i = currentLine.LineNumber + 1; i <= endLineIndex; i++)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                sb.AppendLine(line.GetText());
            }

            return sb.ToString();
        }


        public static string GetSourceCodeType(string fileName)
        {
            var extension = Path.GetExtension(fileName ?? string.Empty);
            if (string.IsNullOrEmpty(extension))
            {
                return "plaintext";
            }

            switch (extension.ToLowerInvariant())
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
                case ".jsx":
                    return "jsx";
                case ".html":
                case ".htm":
                    return "html";
                case ".cshtml":
                case ".razor":
                    return "razor";
                case ".css":
                    return "css";
                case ".scss":
                    return "scss";
                case ".sass":
                    return "sass";
                case ".less":
                    return "less";
                case ".java":
                    return "java";
                case ".ts":
                    return "typescript";
                case ".tsx":
                    return "tsx";
                case ".json":
                    return "json";
                case ".yaml":
                case ".yml":
                    return "yaml";
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
                case ".ps1":
                case ".psm1":
                case ".psd1":
                    return "powershell";
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

        public static string StopAtSimilarLine(string stream, string line)
        {
            // 拆分第一个字符串，考虑不同操作系统的换行符
            string[] lines = stream.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // 第二个字符串只取拆分后，不为全是空白或换行符的行
            string[] filteredLines = line.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();
            if (filteredLines.Length == 0)
            {
                return stream;
            }

            line = filteredLines[0];
            line = line.Trim();
            bool lineIsBracketEnding = IsBracketEnding(line);

            StringBuilder result = new StringBuilder();

            foreach (string nextLine in lines)
            {
                if (lineIsBracketEnding && line.Trim() == nextLine.Trim())
                {
                    break;
                }

                bool lineQualifies = nextLine.Length > 4 && line.Length > 4;
                if (lineQualifies && ComputeDistance(nextLine.Trim(), line) / (double)line.Length < 0.1)
                {
                    break;
                }
                result.AppendLine(nextLine);
            }

            return result.ToString();
        }

        private static bool IsBracketEnding(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var trimmed = line.TrimEnd();
            var last = trimmed[trimmed.Length - 1];
            return last == ')' || last == ']' || last == '}' || last == ';';
        }

        private static int ComputeDistance(string str1, string str2)
        {
            if (string.Equals(str1, str2, StringComparison.Ordinal))
            {
                return 0;
            }

            if (Math.Abs(str1.Length - str2.Length) > 3)
            {
                return int.MaxValue;
            }

            if (str1.Length > 300 || str2.Length > 300)
            {
                return int.MaxValue;
            }

            int[,] dp = new int[str1.Length + 1, str2.Length + 1];

            for (int i = 0; i <= str1.Length; i++)
            {
                dp[i, 0] = i;
            }

            for (int j = 0; j <= str2.Length; j++)
            {
                dp[0, j] = j;
            }

            for (int i = 1; i <= str1.Length; i++)
            {
                for (int j = 1; j <= str2.Length; j++)
                {
                    int cost = (str1[i - 1] == str2[j - 1]) ? 0 : 1;
                    dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
                }
            }

            return dp[str1.Length, str2.Length];
        }

        public static void OpenChatWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (LLMCopilotProvider.Package == null)
            {
                ShowError("Unable to open the chat window.");
                return;
            }

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = LLMCopilotProvider.Package.FindToolWindow(typeof(LLMChatWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                LLMErrorHandler.WriteLog("OpenChatWindow could not create or locate the chat tool window.");
                ShowError("Unable to open the chat window.");
                return;
            }

            var windowFrame = window.Frame as IVsWindowFrame;
            if (windowFrame == null)
            {
                LLMErrorHandler.WriteLog("OpenChatWindow could not cast the tool window frame.");
                ShowError("Unable to open the chat window.");
                return;
            }

            var hr = windowFrame.Show();
            if (ErrorHandler.Failed(hr))
            {
                LLMErrorHandler.WriteLog($"OpenChatWindow failed with HRESULT 0x{hr:X8}.");
                ShowError("Unable to open the chat window.");
            }
        }

        [SuppressMessage("Usage", "VSSDK007:Await/join tasks created from ThreadHelper.JoinableTaskFactory.RunAsync", Justification = "Autocomplete scheduling is intentionally fire-and-forget and self-observing.")]
        [SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "Autocomplete scheduling is intentionally fire-and-forget and self-observing.")]
        public static void CodeCompleteCommand()
        {
            var requestVersion = Interlocked.Increment(ref _completionRequestVersion);
            var cancellationToken = ReplaceCompletionCancellationToken(cancelPrevious: true).Token;
            var scheduledCompletion = ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    var delayMs = Math.Max(0, OllamaHelper.Instance.Options.AutoCompleteDelayMs);
                    await Task.Delay(delayMs, cancellationToken);
                    await CodeCompleteCommandAsync(requestVersion, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            });
            _ = scheduledCompletion.Task;
        }

        public static async Task CodeCompleteCommandAsync(int requestVersion, CancellationToken cancellationToken)
        {
            SetSendingState(true);

            try
            {
                if (requestVersion != Volatile.Read(ref _completionRequestVersion))
                {
                    return;
                }

                await LLMCopilotProvider.EnsurePackageLoadedAsync();

                var package = LLMCopilotProvider.Package;

                RequestOptions reqOps = OllamaHelper.Instance.CompRequestOptions;
                int nPrefixLines = OllamaHelper.EstimatePrefixLinesByCtx(reqOps.NumCtx);
                int nSuffixLines = OllamaHelper.EstimateSuffixLinesByCtx(reqOps.NumCtx);

                var textView = await VsHelpers.GetActiveTextViewAsync(package);
                if (textView == null)
                {
                    return;
                }

                if (textView.Selection != null && !textView.Selection.IsEmpty)
                {
                    return;
                }

                string prefixCode = VsHelpers.GetPrefixLines(textView, nPrefixLines);
                string suffixCode = VsHelpers.GetSuffixLines(textView, nSuffixLines);
                if (string.IsNullOrWhiteSpace(prefixCode) || string.IsNullOrWhiteSpace(prefixCode.Trim()))
                {
                    return;
                }

                var options = OllamaHelper.Instance.Options;

                string template = $"{options.FimBegin}{prefixCode}{options.FimHole}{suffixCode}{options.FimEnd}";

                GenerateCompletionRequest req = new GenerateCompletionRequest
                {
                    Model = options.CompleteModel,
                    Prompt = template,
                    Options = reqOps,
                    Raw = true
                };

                var oldCaretPosition = textView.Caret.Position.BufferPosition;
                var oldSnapshotVersion = oldCaretPosition.Snapshot.Version.VersionNumber;
                var stopwatch = Stopwatch.StartNew();
                var resp = await ollamaService.GenerateCompletionAsync(options.BaseUrl, options.AccessToken, req, cancellationToken);
                stopwatch.Stop();

                if (requestVersion != Volatile.Read(ref _completionRequestVersion))
                {
                    return;
                }

                var newCaretPosition = textView.Caret.Position.BufferPosition;
                if (newCaretPosition.CompareTo(oldCaretPosition) != 0
                    || newCaretPosition.Snapshot.Version.VersionNumber != oldSnapshotVersion)
                {
                    return;
                }

                var comp_text = resp.Response;
                comp_text = StopAtSimilarLine(comp_text, suffixCode);
                comp_text = RemoveCommonSuffixPrefix(comp_text, suffixCode);
                comp_text = comp_text?.TrimEnd();
                if (string.IsNullOrWhiteSpace(comp_text))
                {
                    return;
                }

                var statusText = $"{options.CompleteModel} - {stopwatch.ElapsedMilliseconds} ms";
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LLMAdornmentFactory.CreateAdornment(textView);
                var adornment = LLMAdornmentFactory.GetCurrentAdornment();
                if (adornment != null)
                {
                    adornment.UpdatePrediction(comp_text, statusText);
                }
            }
            catch (OperationCanceledException)
            {
                //do nothing
                //LLMErrorHandler.WriteLog("Code completion was canceled.");
            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex);
            }
            finally
            {
                SetSendingState(false);
            }
        }

        private static CancellationTokenSource ReplaceCompletionCancellationToken(bool cancelPrevious)
        {
            var newCancellationTokenSource = new CancellationTokenSource();
            var previousCancellationTokenSource = Interlocked.Exchange(ref _cancellationTokenSource, newCancellationTokenSource);
            if (previousCancellationTokenSource != null)
            {
                if (cancelPrevious)
                {
                    previousCancellationTokenSource.Cancel();
                }

                previousCancellationTokenSource.Dispose();
            }

            return newCancellationTokenSource;
        }

        private static async Task<IVsTextManager> GetTextManagerAsync(IAsyncServiceProvider serviceProvider)
        {
            return await serviceProvider.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
        }

        private static async Task<IWpfTextView> GetActiveTextViewOnUiThreadAsync(IAsyncServiceProvider serviceProvider)
        {
            var textManager = await GetTextManagerAsync(serviceProvider);
            if (textManager == null)
            {
                return null;
            }

            IVsTextView vTextView;
            if (ErrorHandler.Failed(textManager.GetActiveView(1, null, out vTextView)))
            {
                return null;
            }

            if (vTextView == null)
            {
                return null;
            }

            var userData = vTextView as IVsUserData;
            if (userData == null)
            {
                return null;
            }

            var guidViewHost = DefGuidList.guidIWpfTextViewHost;
            object holder;
            var hr = userData.GetData(ref guidViewHost, out holder);
            if (ErrorHandler.Failed(hr))
            {
                return null;
            }

            var viewHost = holder as IWpfTextViewHost;
            return viewHost?.TextView;
        }

        private static string TryGetActiveDocumentPathFromTextManager(IVsTextManager textManager)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsTextView vTextView;
            if (ErrorHandler.Failed(textManager.GetActiveView(1, null, out vTextView)))
            {
                return null;
            }

            if (vTextView == null)
            {
                return null;
            }

            IVsTextLines textLines;
            if (ErrorHandler.Failed(vTextView.GetBuffer(out textLines)))
            {
                return null;
            }

            var persistFileFormat = textLines as IPersistFileFormat;
            if (persistFileFormat == null)
            {
                return null;
            }

            string fullPath;
            uint formatIndex;
            if (ErrorHandler.Failed(persistFileFormat.GetCurFile(out fullPath, out formatIndex)))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(fullPath) ? null : fullPath;
        }

        private static ITrackingSpan CreateTrackingSpan(ITextSnapshot snapshot, int start, int length)
        {
            if (snapshot == null || snapshot.Length == 0)
            {
                return null;
            }

            var boundedStart = Math.Max(0, Math.Min(start, snapshot.Length));
            var boundedLength = Math.Max(0, Math.Min(length, snapshot.Length - boundedStart));
            return snapshot.CreateTrackingSpan(new Span(boundedStart, boundedLength), SpanTrackingMode.EdgeInclusive);
        }

        private static SnapshotSpan GetTrackedSelectionSpan(ITextSnapshot snapshot, ITrackingSpan trackingSpan, int fallbackStart, int fallbackLength)
        {
            if (trackingSpan != null)
            {
                return trackingSpan.GetSpan(snapshot);
            }

            var boundedStart = Math.Max(0, Math.Min(fallbackStart, snapshot.Length));
            var boundedLength = Math.Max(0, Math.Min(fallbackLength, snapshot.Length - boundedStart));
            return new SnapshotSpan(new SnapshotPoint(snapshot, boundedStart), boundedLength);
        }

        public static void ShowInfo(string message)
        {
            ShowMessage(message, OLEMSGICON.OLEMSGICON_INFO);
        }

        public static void ShowError(string message)
        {
            ShowMessage(message, OLEMSGICON.OLEMSGICON_WARNING);
        }

        [SuppressMessage("Usage", "VSSDK007:Await/join tasks created from ThreadHelper.JoinableTaskFactory.RunAsync", Justification = "Message boxes are intentionally posted back to the UI thread without blocking the caller.")]
        [SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "Message boxes are intentionally posted back to the UI thread without blocking the caller.")]
        private static void ShowMessage(string message, OLEMSGICON icon)
        {
            var notificationTask = ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (LLMCopilotProvider.Package == null)
                {
                    return;
                }

                VsShellUtilities.ShowMessageBox(
                    LLMCopilotProvider.Package,
                    message,
                    "Ollama Pilot",
                    icon,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            });
            _ = notificationTask.Task;
        }
    }
}
