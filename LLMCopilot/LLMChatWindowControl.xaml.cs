using OllamaSharp.Models.Chat;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MdXaml;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;
using System;

namespace LLMCopilot
{
    /// <summary>
    /// Interaction logic for LLMChatWindowControl.
    /// </summary>
    public partial class LLMChatWindowControl : UserControl
    {
        private bool _isSending;
        private ObservableCollection<Message> _messages = new ObservableCollection<Message>();
        /// <summary>
        /// Initializes a new instance of the <see cref="LLMChatWindowControl"/> class.
        /// </summary>
        public LLMChatWindowControl()
        {
            LLMErrorHandler.WriteLog("LLMChatWindowControl");
            var _ = new MdXaml.MarkdownScrollViewer();//DO NOT Delete This!! fix can't find mdxaml dll
            this.InitializeComponent();
            MessagesScrollViewer.PreviewMouseWheel += MessagesScrollViewer_PreviewMouseWheel;
            this.MessageItemsControl.ItemsSource = _messages;


            // 在窗口启动时自动发送ListLocalModels请求
            Task.Run(async () =>
            {
                var models = await OllamaHelper.Instance.OllamaClient.ListLocalModels();
                var modelNames = string.Join("\n", models.Select(m => m.Name));

                await this.Dispatcher.InvokeAsync(() =>
                {
                    _messages.Add(new Message(ChatRole.Assistant, $"Available local models:\n{modelNames}"));
                });
            });
        }

        private void ScrollToBottom()
        {
            this.Dispatcher.InvokeAsync(() =>
            {
                if (VisualTreeHelper.GetChildrenCount(MessagesScrollViewer) > 0)
                {
                    MessagesScrollViewer.ScrollToEnd();
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }



        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SendMessageAsync();
            }
        }

        private async Task SendMessageAsync()
        {
            string text = MessageTextBox.Text;
            if (!string.IsNullOrWhiteSpace(text) && !_isSending)
            {
                _isSending = true;
                SendButton.Content = "Sending...";
                SendButton.IsEnabled = false;
                MessageTextBox.Clear();

                var messages = await OllamaHelper.Instance.Chat.Send(text);
                foreach (var message in messages)
                {
                    if (!_messages.Contains(message))
                    {
                        _messages.Add(message);
                    }
                }

                ScrollToBottom();
                _isSending = false;
                SendButton.Content = "Send";
                SendButton.IsEnabled = true;
            }
        }


        private void MessagesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        

    }

    public class CapitalizeFirstLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatRole role)
            {
                string roleString = role.ToString();
                return char.ToUpper(roleString[0]) + roleString.Substring(1);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
