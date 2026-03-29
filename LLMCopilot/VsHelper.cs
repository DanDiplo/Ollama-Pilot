using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using OllamaSharp.Models;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Task = System.Threading.Tasks.Task;
using Thread = System.Threading.Thread;

namespace OllamaPilot
{
    public static class VsHelpers
    {
        public static bool IsSending { get; set; } = false;
        private static readonly Thread _completionThread;
        private static readonly BlockingCollection<Func<CancellationToken, Task>> _taskQueue = new BlockingCollection<Func<CancellationToken, Task>>();
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static readonly object _lock = new object();
        private static int _completionRequestVersion;

        static VsHelpers()
        {
            _completionThread = new Thread(Run);
            _completionThread.IsBackground = true;
            _completionThread.Start();
        }

        [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Completion work is processed on a dedicated background thread and does not marshal back to the UI thread from this wait site.")]
        private static void Run()
        {
            foreach (var task in _taskQueue.GetConsumingEnumerable())
            {
                try
                {
                    var cts = new CancellationTokenSource();
                    lock (_lock)
                    {
                        _cancellationTokenSource.Cancel();
                        _cancellationTokenSource = cts;
                    }
                    task(cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    // Task was canceled, continue with next task
                }
            }
        }

        public static void EnqueueTask(Func<CancellationToken, Task> task)
        {
            lock (_lock)
            {
                _cancellationTokenSource.Cancel(); // 取消之前的所有任务
                while (_taskQueue.Count > 0)
                {
                    _taskQueue.Take();
                }
                _taskQueue.Add(task); // 添加新任务
            }
        }

        public static void CancelPendingCompletion()
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _completionRequestVersion);
                _cancellationTokenSource.Cancel();

                while (_taskQueue.Count > 0)
                {
                    _taskQueue.Take();
                }
            }
        }

        public static async Task<IWpfTextView> GetActiveTextViewAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var textManager = await serviceProvider.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
            if (textManager == null)
            {
                return null;
            }

            textManager.GetActiveView(1, null, out IVsTextView vTextView);
            if (vTextView == null)
            {
                return null;
            }

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
            var fullPath = await GetActiveDocumentPathAsync(serviceProvider);
            return string.IsNullOrWhiteSpace(fullPath) ? null : Path.GetFileName(fullPath);
        }

        public static async Task<string> GetActiveDocumentPathAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var textManager = await serviceProvider.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
            if (textManager == null)
            {
                return null;
            }

            textManager.GetActiveView(1, null, out IVsTextView vTextView);

            if (vTextView != null)
            {
                IVsTextLines textLines;
                vTextView.GetBuffer(out textLines);

                if (textLines is IPersistFileFormat persistFileFormat)
                {
                    persistFileFormat.GetCurFile(out string fullPath, out uint _);
                    return fullPath;
                }
            }

            return null;
        }

        public static async Task<string> GetActiveDocumentTextAsync(IAsyncServiceProvider serviceProvider)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var textView = await GetActiveTextViewAsync(serviceProvider);
            return textView?.TextSnapshot?.GetText();
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

            var allLines = File.ReadAllLines(filePath);

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
            var textManager = await serviceProvider.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
            if (textManager == null)
            {
                return null;
            }

            textManager.GetActiveView(1, null, out IVsTextView vTextView);
            if (vTextView != null)
            {
                IVsTextLines textLines;
                vTextView.GetBuffer(out textLines);
                return textLines;
            }
            return null;
        }

        public static Task<bool> InsertTextIntoActiveDocumentAsync(IAsyncServiceProvider serviceProvider, string text)
        {
            return ApplyTextToActiveDocumentAsync(serviceProvider, text, replaceSelection: false);
        }

        public static Task<bool> ReplaceSelectionInActiveDocumentAsync(IAsyncServiceProvider serviceProvider, string text)
        {
            return ApplyTextToActiveDocumentAsync(serviceProvider, text, replaceSelection: true);
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
            File.WriteAllText(siblingPath, text, Encoding.UTF8);

            var dte = await serviceProvider.GetServiceAsync(typeof(SDTE)) as DTE2;
            dte?.ItemOperations.OpenFile(siblingPath);
            return siblingPath;
        }

        private static async Task<bool> ApplyTextToActiveDocumentAsync(IAsyncServiceProvider serviceProvider, string text, bool replaceSelection, bool replaceAll = false)
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

            ITextSnapshot appliedSnapshot;
            int caretPosition;
            using (var edit = textView.TextBuffer.CreateEdit())
            {
                if (replaceSelection)
                {
                    var selectionSpan = textView.Selection.StreamSelectionSpan.SnapshotSpan;
                    caretPosition = selectionSpan.Start.Position + text.Length;
                    edit.Replace(selectionSpan, text);
                }
                else if (replaceAll)
                {
                    var fullSpan = new Span(0, textView.TextSnapshot.Length);
                    caretPosition = text.Length;
                    edit.Replace(fullSpan, text);
                }
                else
                {
                    var insertionPoint = textView.Caret.Position.BufferPosition.Position;
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
            textView.Selection.Clear();
            textView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(new SnapshotPoint(appliedSnapshot, boundedCaretPosition), 0));
            return true;
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

        public static string RemoveCommonSuffixPrefix(string A, string B)
        {
            B = B.TrimStart();
            int minLen = Math.Min(A.Length, B.Length);

            int commonLength = 0;
            for (int i = 1; i <= minLen; i++)
            {
                if (A.Substring(A.Length - i) == B.Substring(0, i))
                {
                    commonLength = i;
                }
            }

            if (commonLength > 0)
            {
                return A.Substring(0, A.Length - commonLength);
            }

            return A;
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

            var sb = new System.Text.StringBuilder();

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

            var sb = new System.Text.StringBuilder();

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
            char[] bracketEnding = { ')', ']', '}', ';' };
            return line.Trim().Any(c => bracketEnding.Contains(c));
        }

        private static int ComputeDistance(string str1, string str2)
        {
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

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = LLMCopilotProvider.Package.FindToolWindow(typeof(LLMChatWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        public static void CodeCompleteCommand()
        {
            var requestVersion = Interlocked.Increment(ref _completionRequestVersion);
            EnqueueTask(async (cancellationToken) =>
            {
                var delayMs = Math.Max(0, OllamaHelper.Instance.Options.AutoCompleteDelayMs);
                await Task.Delay(delayMs, cancellationToken);
                await CodeCompleteCommandAsync(requestVersion, cancellationToken);
            });
        }

        public static async Task CodeCompleteCommandAsync(int requestVersion, CancellationToken cancellationToken)
        {
            IsSending = true;

            try
            {
                if (requestVersion != Volatile.Read(ref _completionRequestVersion))
                {
                    return;
                }

                await LLMCopilotProvider.EnsurePackageLoadedAsync();

                var client = OllamaClientFactory.CreateClient();

                var Package = LLMCopilotProvider.Package;

                RequestOptions reqOps = OllamaHelper.Instance.CompRequestOptions;
                int nPrefixLines = OllamaHelper.EstimatePrefixLinesByCtx(reqOps.NumCtx);
                int nSuffixLines = OllamaHelper.EstimateSuffixLinesByCtx(reqOps.NumCtx);

                var textView = await VsHelpers.GetActiveTextViewAsync(Package);
                if (textView == null)
                {
                    return;
                }

                if (textView.Selection != null && !textView.Selection.IsEmpty)
                {
                    return;
                }

                string PrefixCode = VsHelpers.GetPrefixLines(textView, nPrefixLines);
                string SuffixCode = VsHelpers.GetSuffixLines(textView, nSuffixLines);
                if (string.IsNullOrWhiteSpace(PrefixCode) || string.IsNullOrWhiteSpace(PrefixCode.Trim()))
                {
                    return;
                }

                var options = OllamaHelper.Instance.Options;

                string template = $"{options.FimBegin}{PrefixCode}{options.FimHole}{SuffixCode}{options.FimEnd}";

                GenerateCompletionRequest req = new GenerateCompletionRequest
                {
                    Model = client.SelectedModel,
                    Prompt = template,
                    Options = reqOps,
                    Raw = true
                };

                var OldCaretPosition = textView.Caret.Position.BufferPosition;
                var oldSnapshotVersion = OldCaretPosition.Snapshot.Version.VersionNumber;
                var stopwatch = Stopwatch.StartNew();
                var resp = await client.GetCompletionAsync(req, cancellationToken);
                stopwatch.Stop();

                if (requestVersion != Volatile.Read(ref _completionRequestVersion))
                {
                    return;
                }

                var NewCaretPosition = textView.Caret.Position.BufferPosition;
                if (NewCaretPosition.CompareTo(OldCaretPosition) != 0
                    || NewCaretPosition.Snapshot.Version.VersionNumber != oldSnapshotVersion)
                {
                    return;
                }

                var comp_text = resp.Response;
                comp_text = StopAtSimilarLine(comp_text, SuffixCode);
                comp_text = RemoveCommonSuffixPrefix(comp_text, SuffixCode);
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
                IsSending = false;
            }
        }

        public static void ShowInfo(string message)
        {
            ShowMessage(message, OLEMSGICON.OLEMSGICON_INFO);
        }

        public static void ShowError(string message)
        {
            ShowMessage(message, OLEMSGICON.OLEMSGICON_WARNING);
        }

        private static void ShowMessage(string message, OLEMSGICON icon)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (LLMCopilotProvider.Package == null)
                {
                    return;
                }

                VsShellUtilities.ShowMessageBox(
                    LLMCopilotProvider.Package,
                    message,
                    "LLMCopilot",
                    icon,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            });
        }
    }
}
