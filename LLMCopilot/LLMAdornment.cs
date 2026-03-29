using ICSharpCode.AvalonEdit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OllamaPilot
{
    public class LLMAdornment
    {
        private readonly IWpfTextView _view;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly TextEditor _textEditor; // 用于显示预测结果
        private string _originalPredictionText; // 用于存储原始预测文本
        public SnapshotPoint Pos { get; private set; }

        public IWpfTextView View
        {
            get { return _view; }
        }

        public LLMAdornment(IWpfTextView view)
        {
            _view = view;
            _adornmentLayer = view.GetAdornmentLayer("LLMAdornment");

            _view.LayoutChanged += OnLayoutChanged;

            var defaultTextProperties = _view.FormattedLineSource.DefaultTextProperties;
            var typeface = defaultTextProperties.Typeface;
            var fontRenderingSize = defaultTextProperties.FontRenderingEmSize;
            var foregroundBrush = defaultTextProperties.ForegroundBrush as SolidColorBrush;

            // 初始化 TextEditor，但不添加到 _adornmentLayer 中
            _textEditor = new TextEditor
            {
                IsReadOnly = true,
                ShowLineNumbers = false,
                Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)) { Opacity = 0.92 },
                FontFamily = typeface.FontFamily,
                FontSize = fontRenderingSize,
                Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212)),
                WordWrap = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, // 隐藏垂直滚动条
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, // 隐藏水平滚动条
                Opacity = 0.96
            };

            _textEditor.TextArea.SelectionBrush = new SolidColorBrush(Colors.Transparent);
            _textEditor.MaxWidth = CalculateMaxWidth();
            _textEditor.MaxHeight = fontRenderingSize * 12;

            // 设置不可编辑
            _textEditor.IsHitTestVisible = false;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
        }

        private double CalculateMaxWidth()
        {
            var defaultTextProperties = _view.FormattedLineSource.DefaultTextProperties;
            double fontSize = defaultTextProperties.FontRenderingEmSize; // 获取字体大小

            // 假设每个字符的平均宽度是字体大小的1倍（根据实际情况调整）
            double averageCharWidth = fontSize;
            return averageCharWidth * 80;
        }

        private void CreateVisuals(SnapshotPoint position)
        {
            if (_textEditor != null)
            {
                // 获取光标所在行
                var line = position.GetContainingLine();
                var lineTextViewLine = _view.GetTextViewLineContainingBufferPosition(position);

                // 重新设置位置
                var caretPosition = _view.Caret.Position.BufferPosition;
                var caretTop = _view.Caret.ContainingTextViewLine.Top;
                var caretLeft = _view.Caret.Left;

                Canvas.SetLeft(_textEditor, caretLeft);
                Canvas.SetTop(_textEditor, caretTop);

                // 设置 ZIndex 确保其显示在顶部
                Canvas.SetZIndex(_textEditor, 32766);

                var span = new SnapshotSpan(position, 0);

                // 移除已有的父级
                if (_textEditor.Parent is Panel parent)
                {
                    parent.Children.Remove(_textEditor);
                }

                _adornmentLayer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, span, null, _textEditor, null);
            }
        }

        /// <summary>
        /// Updates the prediction text and status for the adornment.
        /// </summary>
        /// <param name="prediction">The new prediction text to display.</param>
        /// <param name="statusText">Optional status text to show as a tooltip.</param>
        public void UpdatePrediction(string prediction, string statusText = null)
        {
            // Store the original prediction text
            _originalPredictionText = prediction;

            if (_textEditor != null)
            {
                // Run an asynchronous task on the main thread
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // Update the TextEditor's text and tooltip
                    _textEditor.Text = SplitLines(prediction.TrimStart());
                    _textEditor.ToolTip = statusText;

                    // Get the current caret position in the text view
                    var caretPosition = _view.Caret.Position.BufferPosition;

                    // Update the position of the adornment
                    Pos = caretPosition;

                    // Create and display the adornment visuals at the caret position
                    CreateVisuals(caretPosition);

                    // Refocus the text view element
                    _view.VisualElement.Focus();
                });
            }
        }

        private string SplitLines(string text)
        {
            /// <summary>
            /// Splits the input text into lines using regular expressions to match common newline characters.
            /// </summary>
            /// <param name="text">The input text to be split.</param>
            /// <returns>A string with lines joined by the system's default newline character.</returns>

            // Split the text using regex to handle different newline formats: \n, \r\n, or \r
            var lines = Regex.Split(text, "\r\n|\r|\n");

            // Join the split lines back into a single string using the system's default newline character
            return string.Join(Environment.NewLine, lines);
        }


        public string GetPredictionText()
        {
            return _originalPredictionText ?? string.Empty;
        }
    }

    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    public class LLMAdornmentFactory : IWpfTextViewCreationListener
    {
        [Export(typeof(AdornmentLayerDefinition))]
        [Name("LLMAdornment")]
        [Order(After = PredefinedAdornmentLayers.Text)]
        public AdornmentLayerDefinition editorAdornmentLayer = null;

        private static LLMAdornment _currentAdornment;

        public void TextViewCreated(IWpfTextView textView)
        {
            // 使用 GetViewAdapter 方法获取 IVsTextView
            var viewAdapter = CommandFilter.GetViewAdapter(textView);
            if (viewAdapter != null)
            {
                var commandFilter = new CommandFilter(textView);
                commandFilter.AddCommandFilter();
            }
        }


        public static void CreateAdornment(IWpfTextView textView)
        {
            var adornment = GetCurrentAdornment();
            if (adornment == null || adornment.View != textView)
            {
                ClearAdornment(textView);
                _currentAdornment = new LLMAdornment(textView);
            }
        }

        public static LLMAdornment GetCurrentAdornment()
        {
            return _currentAdornment;
        }
        public static void ClearAdornment()
        {
            _currentAdornment = null;
        }

        public static void AcceptPrediction(IWpfTextView view)
        {
            var adornment = GetCurrentAdornment();
            if (adornment != null)
            {
                var caretPosition = view.Caret.Position.BufferPosition;
                if (caretPosition.CompareTo(adornment.Pos) == 0)
                {
                    var text = adornment.GetPredictionText();

                    view.TextBuffer.Insert(caretPosition, text);
                }

                ClearAdornment(view);
            }
        }

        public static void CancelPrediction(IWpfTextView view)
        {
            ClearAdornment(view);
        }

        public static void AcceptPredictionLines(IWpfTextView view, int lines)
        {
            var adornment = LLMAdornmentFactory.GetCurrentAdornment();
            if (adornment != null)
            {
                var caretPosition = view.Caret.Position.BufferPosition;
                if (caretPosition.CompareTo(adornment.Pos) == 0)
                {
                    var predictionLines = Regex.Split(adornment.GetPredictionText(), "\r\n|\r|\n");

                    lines = Math.Min(lines, predictionLines.Length);

                    var linesToInsert = string.Join(Environment.NewLine, predictionLines.Take(lines));

                    view.TextBuffer.Insert(caretPosition, linesToInsert);
                }

                ClearAdornment();
            }
        }

        public static void ClearAdornment(IWpfTextView view)
        {
            var adornmentLayer = view.GetAdornmentLayer("LLMAdornment");
            adornmentLayer.RemoveAllAdornments();
            ClearAdornment();
        }
    }

    public class CommandFilter : IOleCommandTarget
    {
        private readonly IWpfTextView _view;
        private IOleCommandTarget _nextCommandTarget;

        public CommandFilter(IWpfTextView view)
        {
            _view = view;
        }

        public static IVsTextView GetViewAdapter(IWpfTextView textView)
        {
            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var adapterFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            return adapterFactory.GetViewAdapter(textView);
        }

        public void AddCommandFilter()
        {
            var viewAdapter = GetViewAdapter(_view);
            if (viewAdapter != null)
            {
                viewAdapter.AddCommandFilter(this, out _nextCommandTarget);
            }
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _nextCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        private bool IsConcernedCommand(uint nCmdID, IntPtr pvaIn)
        {
            switch (nCmdID)
            {
                case (uint)VSConstants.VSStd2KCmdID.RETURN:
                case (uint)VSConstants.VSStd2KCmdID.TAB:
                case (uint)VSConstants.VSStd2KCmdID.BACKSPACE:
                case (uint)VSConstants.VSStd2KCmdID.DELETE:
                case (uint)VSConstants.VSStd2KCmdID.LEFT:
                case (uint)VSConstants.VSStd2KCmdID.RIGHT:
                case (uint)VSConstants.VSStd2KCmdID.UP:
                case (uint)VSConstants.VSStd2KCmdID.DOWN:
                case (uint)VSConstants.VSStd2KCmdID.HOME:
                case (uint)VSConstants.VSStd2KCmdID.END:
                case (uint)VSConstants.VSStd2KCmdID.CANCEL:
                    return true;
                case (uint)VSConstants.VSStd2KCmdID.TYPECHAR:
                    char typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                    if (typedChar >= '0' && typedChar <= '9')
                    {
                        return false;
                    }

                    return true;
                default:
                    return false;
            }
        }

        private static bool ShouldCancelPendingCompletion(uint nCmdID)
        {
            switch (nCmdID)
            {
                case (uint)VSConstants.VSStd2KCmdID.BACKSPACE:
                case (uint)VSConstants.VSStd2KCmdID.DELETE:
                case (uint)VSConstants.VSStd2KCmdID.LEFT:
                case (uint)VSConstants.VSStd2KCmdID.RIGHT:
                case (uint)VSConstants.VSStd2KCmdID.UP:
                case (uint)VSConstants.VSStd2KCmdID.DOWN:
                case (uint)VSConstants.VSStd2KCmdID.HOME:
                case (uint)VSConstants.VSStd2KCmdID.END:
                case (uint)VSConstants.VSStd2KCmdID.CANCEL:
                case (uint)VSConstants.VSStd2KCmdID.TYPECHAR:
                case (uint)VSConstants.VSStd2KCmdID.TAB:
                case (uint)VSConstants.VSStd2KCmdID.RETURN:
                    return true;
                default:
                    return false;
            }
        }

        private static char? GetTypedChar(uint nCmdID, IntPtr pvaIn)
        {
            if (nCmdID != (uint)VSConstants.VSStd2KCmdID.TYPECHAR || pvaIn == IntPtr.Zero)
            {
                return null;
            }

            return (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (!IsConcernedCommand(nCmdID, pvaIn) || LLMCopilotProvider.Package == null)
                {
                    return _nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                }

                if (ShouldCancelPendingCompletion(nCmdID))
                {
                    VsHelpers.CancelPendingCompletion();
                }

                if (LLMAdornmentFactory.GetCurrentAdornment() == null)
                {
                    var result = _nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                    var typedChar = GetTypedChar(nCmdID, pvaIn);
                    if (VsHelpers.ShouldTriggerAutoComplete(_view, typedChar, OllamaHelper.Instance.Options, nCmdID))
                    {
                        VsHelpers.CodeCompleteCommand();
                    }

                    return result;
                }
                else
                {
                    switch (nCmdID)
                    {
                        case (uint)VSConstants.VSStd2KCmdID.TAB:
                            LLMAdornmentFactory.AcceptPrediction(_view);
                            return VSConstants.S_OK;
                        case (uint)VSConstants.VSStd2KCmdID.CANCEL:
                            LLMAdornmentFactory.CancelPrediction(_view);
                            return VSConstants.S_OK;
                        case (uint)VSConstants.VSStd2KCmdID.TYPECHAR:
                            {
                                char typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                                if (typedChar >= '1' && typedChar <= '9')
                                {
                                    LLMAdornmentFactory.AcceptPredictionLines(_view, typedChar - '0');
                                    return VSConstants.S_OK;
                                }
                            }
                            break;
                    }
                }

                return _nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
            catch (Exception e)
            {
                LLMErrorHandler.HandleException(e);
            }

            return _nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

    }

    public class LLMAdornmentKeyProcessor : KeyProcessor
    {
        private readonly IWpfTextView _view;

        public LLMAdornmentKeyProcessor(IWpfTextView view)
        {
            _view = view;
        }

        public override void PreviewKeyDown(KeyEventArgs e)
        {
            if (LLMAdornmentFactory.GetCurrentAdornment() == null || LLMCopilotProvider.Package == null)
            {
                return;
            }

            if ((Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Q)
                || (e.SystemKey == Key.Q && Keyboard.Modifiers == ModifierKeys.Alt))
            {
                LLMAdornmentFactory.AcceptPrediction(_view);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && ((e.Key >= Key.D1 && e.Key <= Key.D9) || (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)))
            {
                LLMAdornmentFactory.AcceptPredictionLines(_view, GetDigitFromKey(e.Key));
                e.Handled = true;
            }
            else if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt
                || e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl
                || e.Key == Key.LeftShift || e.Key == Key.RightShift
                || e.Key == Key.System)
            {
                // do nothing
            }
            else
            {
                LLMAdornmentFactory.CancelPrediction(_view);
            }
        }

        private int GetDigitFromKey(Key key)
        {
            if (key >= Key.D1 && key <= Key.D9)
            {
                return (int)key - (int)Key.D0;
            }
            else if (key >= Key.NumPad1 && key <= Key.NumPad9)
            {
                return (int)key - (int)Key.NumPad0;
            }
            return 0;
        }

    }

    [Export(typeof(IKeyProcessorProvider))]
    [Name("LLMAdornmentKeyProcessorProvider")]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    public class LLMAdornmentKeyProcessorProvider : IKeyProcessorProvider
    {
        public KeyProcessor GetAssociatedProcessor(IWpfTextView textView)
        {
            return new LLMAdornmentKeyProcessor(textView);
        }
    }
}
