using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Input;
using System.Windows;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace LLMCopilot
{
    public class LLMAdornment
    {
        private IWpfTextView _view;
        private IAdornmentLayer _adornmentLayer;
        private TextEditor _textEditor; // 用于显示预测结果
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
                ShowLineNumbers = true,
                Background = new SolidColorBrush(Colors.Gray) { Opacity = 0.8 },
                FontFamily = typeface.FontFamily,
                FontSize = fontRenderingSize,
                Foreground = foregroundBrush,
                WordWrap = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, // 隐藏垂直滚动条
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden // 隐藏水平滚动条
            };

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

        public void UpdatePrediction(string prediction)
        {
            _originalPredictionText = prediction;

            if (_textEditor != null)
            {
                // 在 UI 线程上更新 _textEditor.Text
                _view.VisualElement.Dispatcher.Invoke(() =>
                {
                    _textEditor.Text = SplitLines(prediction.TrimStart());

                    // 获取当前光标位置
                    var caretPosition = _view.Caret.Position.BufferPosition;

                    Pos = caretPosition;
                    // 更新并显示 Adornment
                    CreateVisuals(caretPosition);

                    // 将焦点重新设置到文本视图
                    _view.VisualElement.Focus();
                });
            }
        }

        private string SplitLines(string text)
        {
            // 使用正则表达式来分割文本，匹配 \n、\r\n 或 \r 作为换行符
            var lines = Regex.Split(text, "\r\n|\r|\n");

            // 将处理过的文本行重新连接成一个字符串，使用 Environment.NewLine 保证在当前操作系统上使用正确的换行符
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
            return _nextCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        private bool IsConcernedCommand(uint nCmdID, IntPtr pvaIn)
        {
            switch (nCmdID)
            {
                case (uint)VSConstants.VSStd2KCmdID.RETURN:
                case (uint)VSConstants.VSStd2KCmdID.TAB:
               
                case (uint)VSConstants.VSStd2KCmdID.CANCEL:
                    {
                        return true;
                    }
                    break;
                case (uint)VSConstants.VSStd2KCmdID.TYPECHAR:
                    {
                        char typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                        if (typedChar >= '0' && typedChar <= '9')
                        {

                            return false;
                        }

                        return true;
                    }

                    break;
                default:
                    {
                        return false;
                    }
            }
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            try
            {
                if (!IsConcernedCommand(nCmdID, pvaIn) || LLMCopilotProvider.Package == null)
                {
                    return _nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                }

                if (LLMAdornmentFactory.GetCurrentAdornment() == null)
                {
                    switch (nCmdID)
                    {
                        case (uint)VSConstants.VSStd2KCmdID.RETURN:
                        case (uint)VSConstants.VSStd2KCmdID.TAB:
                        case (uint)VSConstants.VSStd2KCmdID.TYPECHAR:
                            {
                                var options = OllamaHelper.Instance.Options;
                                if (options.EnableAutoComplete)
                                {
                                    VsHelpers.CodeCompleteCommand();
                                    break;
                                }
                            }
                            break;
                        default:
                            {
                                return _nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                            }
                    }
                }
                else
                {
                    switch (nCmdID)
                    {
                        case (uint)VSConstants.VSStd2KCmdID.CANCEL:
                            {
                                LLMAdornmentFactory.CancelPrediction(_view);
                                return VSConstants.S_OK;
                            }
                            break;
                        //case (uint)VSConstants.VSStd2KCmdID.TYPECHAR:
                        //    {
                        //        char typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                        //        if (typedChar >= '1' && typedChar <= '9')
                        //        {
                        //            LLMAdornmentFactory.AcceptPredictionLines(_view, typedChar - '0');
                        //            return VSConstants.S_OK;
                        //        }
                        //    }
                        //    break;
                    }
                }

                return _nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
            catch(Exception e)
            {
                LLMErrorHandler.HandleException(e);
            }

            return _nextCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

    }

    public class LLMAdornmentKeyProcessor : KeyProcessor
    {
        private IWpfTextView _view;

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
            else if(e.Key == Key.LeftAlt || e.Key == Key.RightAlt
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
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    public class LLMAdornmentKeyProcessorProvider : IKeyProcessorProvider
    {
        public KeyProcessor GetAssociatedProcessor(IWpfTextView textView)
        {
            return new LLMAdornmentKeyProcessor(textView);
        }
    }
}
