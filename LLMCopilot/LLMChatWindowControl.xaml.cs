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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LLMCopilot
{
    public class MyMessage : INotifyPropertyChanged
    {
        Message _message;
        public string Content
        {
            get => _message.Content;
            set
            {
                if (_message.Content != value)
                {
                    _message.Content = value;
                    OnPropertyChanged();
                }
            }
        }

        public ChatRole? Role
        {
            get => _message.Role;
            private set { }
        }

        public MyMessage(Message message)
        {
            _message = message;
        }

        public MyMessage(ChatRole? role, string content)
        {
            _message = new Message(role, content);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    /// <summary>
    /// Interaction logic for LLMChatWindowControl.
    /// </summary>
    public partial class LLMChatWindowControl : UserControl
    {
        private bool _isSending;
        private ObservableCollection<MyMessage> _messages = new ObservableCollection<MyMessage>();
        public ObservableCollection<MyMessage> Messages => _messages;
        /// <summary>
        /// Initializes a new instance of the <see cref="LLMChatWindowControl"/> class.
        /// </summary>
        public LLMChatWindowControl()
        {
            var _ = new MdXaml.MarkdownScrollViewer();//DO NOT Delete This!! fix can't find mdxaml dll
            this.InitializeComponent();
            this.DataContext = this;
            MessagesScrollViewer.PreviewMouseWheel += MessagesScrollViewer_PreviewMouseWheel;
            //this.MessageItemsControl.ItemsSource = _messages;
            OllamaHelper.Instance.OnMessageReceived += AppendOrUpdateLastMessage;
            EventManager.CodeCommandExecuted += OnExplainCodeCommandExecuted;
            this.Unloaded += LLMChatWindowControl_Unloaded;

            // 在窗口启动时自动发送ListLocalModels请求
            Task.Run(async () =>
            {
                try
                {
                    var models = await OllamaHelper.Instance.OllamaChatClient.ListLocalModels();
                    var modelNames = string.Join("  \n", models.Select(m => m.Name));

                    await this.Dispatcher.InvokeAsync(() =>
                    {
                        _messages.Add(new MyMessage(ChatRole.System, $"Available local models:  \n{modelNames}"));
                    });
                }
                catch(Exception ex)
                {
                    LLMErrorHandler.HandleException(ex);
                }
            });
        }

        private void OnExplainCodeCommandExecuted(object sender, CommandExecutedEventArgs e)
        {
            // 处理事件，执行某些操作
            Dispatcher.Invoke(async () => {
                // 在 UI 线程上更新 UI 或执行操作
                await SendChatMessageAsync(e.SelectedText);
                // 或者执行其他操作
            });
        }

        private void LLMChatWindowControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // 当 UserControl 卸载时执行的代码
            OllamaHelper.Instance.OnMessageReceived -= AppendOrUpdateLastMessage;
            EventManager.CodeCommandExecuted -= OnExplainCodeCommandExecuted;
        }


        private void AppendOrUpdateLastMessage(string content)
        {
            // UI 线程上执行
            Dispatcher.Invoke(() =>
            {
                if (_messages.Any() && _messages.Last().Role == ChatRole.Assistant)
                {
                    var lastMessage = _messages.Last();
                    lastMessage.Content += content;
                }
                else
                {
                    _messages.Add(new MyMessage(ChatRole.Assistant, content));
                }

                ScrollToBottom();  // 确保每次更新后滚动到底部
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

        public async Task SendChatMessageAsync(string text)
        {
            if (!string.IsNullOrWhiteSpace(text) && !_isSending)
            {
                _isSending = true;
                SendButton.Content = "Answering...";
                SendButton.IsEnabled = false;

                MyMessage userMessage = new MyMessage(ChatRole.User, text);
                _messages.Add(userMessage);

                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(30)); // 设置超时时间为30秒
                try
                {
                    await OllamaHelper.Instance.Chat.Send(text, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 任务被取消
                    LLMErrorHandler.HandleException(new TimeoutException("发送消息超时"));
                }
                catch (Exception ex)
                {
                    // 其他异常处理
                    LLMErrorHandler.HandleException(ex);
                }
                finally
                {
                    // 确保在任何情况下都重置状态
                    _isSending = false;
                    SendButton.Content = "Send";
                    SendButton.IsEnabled = true;
                }

                ScrollToBottom();
            }
        }

        private async Task SendMessageAsync()
        {
            string text = MessageTextBox.Text;
            MessageTextBox.Clear();
            await SendChatMessageAsync(text);
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
