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

namespace LLMCopilot
{
    public class LLMAdornment
    {
        private IWpfTextView _view;
        private IAdornmentLayer _adornmentLayer;
        private TextBlock _textBlock; // 用于显示预测结果
        private string _originalPredictionText; // 用于存储原始预测文本

        public LLMAdornment(IWpfTextView view)
        {
            _view = view;
            _adornmentLayer = view.GetAdornmentLayer("LLMAdornment");

            _view.LayoutChanged += OnLayoutChanged;

            // 初始化 _textBlock，但不添加到 _adornmentLayer 中
            _textBlock = new TextBlock
            {
                Text = "", // 初始内容为空
                Background = new SolidColorBrush(Colors.Gray),
                Opacity = 0.8, // 设置不透明度以使其看起来像预测文本
                TextWrapping = TextWrapping.Wrap, // 设置自动换行
                MaxWidth = CalculateMaxWidth() // 设置最大宽度
            };
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            
        }

        private double CalculateMaxWidth()
        {
            var defaultTextProperties = _view.FormattedLineSource.DefaultTextProperties;
            double fontSize = defaultTextProperties.FontRenderingEmSize; // 获取字体大小

            // 假设每个字符的平均宽度是字体大小的0.6倍（根据实际情况调整）
            double averageCharWidth = fontSize * 0.6;
            return averageCharWidth * 80;
        }

        private void CreateVisuals(SnapshotPoint position)
        {
            if (_textBlock != null)
            {
                // 获取光标所在行
                var line = position.GetContainingLine();
                var lineTextViewLine = _view.GetTextViewLineContainingBufferPosition(position);

                // 重新设置位置
                var caretPosition = _view.Caret.Position.BufferPosition;
                var caretTop = _view.Caret.ContainingTextViewLine.Top;
                var caretLeft = _view.Caret.Left;

                Canvas.SetLeft(_textBlock, caretLeft);
                Canvas.SetTop(_textBlock, caretTop);

                var span = new SnapshotSpan(position, 0);
                _adornmentLayer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, _textBlock, null);
            }
        }

        public void UpdatePrediction(string prediction)
        {
            _originalPredictionText = prediction;

            if (_textBlock != null)
            {
                // 在 UI 线程上更新 _textBlock.Text
                _view.VisualElement.Dispatcher.Invoke(() =>
                {
                    _textBlock.Text = AddLineNumbers(prediction);

                    // 获取当前光标位置
                    var caretPosition = _view.Caret.Position.BufferPosition;

                    // 更新并显示 Adornment
                    CreateVisuals(caretPosition);

                    // 将焦点重新设置到文本视图
                    _view.VisualElement.Focus();
                });
            }
        }

        private string AddLineNumbers(string text)
        {
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
            var numberedLines = lines.Select((line, index) => $"{index + 1}: {line}");
            return string.Join("\n", numberedLines);
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
        }

        public static void CreateAdornment(IWpfTextView textView)
        {
            _currentAdornment = new LLMAdornment(textView);
        }

        public static LLMAdornment GetCurrentAdornment()
        {
            return _currentAdornment;
        }
        public static void ClearAdornment()
        {
            _currentAdornment = null;
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
            if (LLMAdornmentFactory.GetCurrentAdornment() == null)
            {
                return;
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                AcceptPrediction();
                e.Handled = true;
            }
            else if ((e.Key >= Key.D1 && e.Key <= Key.D9) || (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9))
            {
                AcceptPredictionLines(GetDigitFromKey(e.Key));
                e.Handled = true;
            }
            else
            {
                CancelPrediction();
                e.Handled = true;
            }
        }

        private void AcceptPrediction()
        {
            // Logic to accept the prediction and insert it into the editor
            var adornment = LLMAdornmentFactory.GetCurrentAdornment();
            if (adornment != null)
            {
                var caretPosition = _view.Caret.Position.BufferPosition;
                _view.TextBuffer.Insert(caretPosition, adornment.GetPredictionText());
                ClearAdornment();
            }
        }

        private void AcceptPredictionLines(int lines)
        {
            // Logic to accept the first 'lines' lines of the prediction and insert it into the editor
            var adornment = LLMAdornmentFactory.GetCurrentAdornment();
            if (adornment != null)
            {
                // 使用正则表达式处理 \r\n 和 \n
                var predictionLines = Regex.Split(adornment.GetPredictionText(), "\r\n|\r|\n");

                // 确保 lines 不超过实际行数
                lines = Math.Min(lines, predictionLines.Length);

                var linesToInsert = string.Join(Environment.NewLine, predictionLines.Take(lines));

                var caretPosition = _view.Caret.Position.BufferPosition;
                _view.TextBuffer.Insert(caretPosition, linesToInsert);
                ClearAdornment();
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

        private void CancelPrediction()
        {
            // Logic to cancel the prediction
            ClearAdornment();
        }


        private void ClearAdornment()
        {
            // Clear the adornment
            var adornmentLayer = _view.GetAdornmentLayer("LLMAdornment");
            adornmentLayer.RemoveAllAdornments();
            LLMAdornmentFactory.ClearAdornment();
        }
    }

    [Export(typeof(IKeyProcessorProvider))]
    [Name("LLMAdornmentKeyProcessorProvider")]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    public class LLMAdornmentKeyProcessorProvider : IKeyProcessorProvider
    {
        public KeyProcessor GetAssociatedProcessor(IWpfTextView textView)
        {
            return new LLMAdornmentKeyProcessor(textView);
        }
    }
}
