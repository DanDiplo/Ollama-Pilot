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

namespace LLMCopilot
{
    public class LLMAdornment
    {
        private IWpfTextView _view;
        private IAdornmentLayer _adornmentLayer;
        private TextBlock _textBlock; // 用于显示预测结果

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
                Opacity = 1.0 // 设置不透明度以使其看起来像预测文本
            };
            SetMaxWidthForTextBlock();
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            
        }

        private void SetMaxWidthForTextBlock()
        {
            var defaultTextProperties = _view.FormattedLineSource.DefaultTextProperties;
            double fontSize = defaultTextProperties.FontRenderingEmSize; // 获取字体大小

            // 假设每个字符的平均宽度是字体大小的0.6倍（根据实际情况调整）
            double averageCharWidth = fontSize * 0.6;
            _textBlock.MaxWidth = averageCharWidth * 80;
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
            if (_textBlock != null)
            {
                // 在 UI 线程上更新 _textBlock.Text
                _view.VisualElement.Dispatcher.Invoke(() =>
                {
                    _textBlock.Text = prediction;

                    // 获取当前光标位置
                    var caretPosition = _view.Caret.Position.BufferPosition;

                    // 更新并显示 Adornment
                    CreateVisuals(caretPosition);

                    // 将焦点重新设置到文本视图
                    _view.VisualElement.Focus();
                });
            }
        }

        public string GetPredictionText()
    {
        return _textBlock?.Text ?? string.Empty;
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
