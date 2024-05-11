using OllamaSharp.Models.Chat;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MdXaml;

namespace LLMCopilot
{
    /// <summary>
    /// Interaction logic for LLMChatWindowControl.
    /// </summary>
    public partial class LLMChatWindowControl : UserControl
    {
        private ObservableCollection<Message> _messages = new ObservableCollection<Message>();
        /// <summary>
        /// Initializes a new instance of the <see cref="LLMChatWindowControl"/> class.
        /// </summary>
        public LLMChatWindowControl()
        {
            var _ = new MdXaml.MarkdownScrollViewer();
            this.InitializeComponent();
            this.MessageListBox.ItemsSource = _messages;

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
            if (!string.IsNullOrWhiteSpace(text))
            {
                var messages = await OllamaHelper.Instance.Chat.Send(text);
                foreach (var message in messages)
                {
                    if (!_messages.Contains(message))
                    {
                        _messages.Add(message);
                    }
                }
                MessageTextBox.Clear();
                MessageListBox.ScrollIntoView(MessageListBox.Items[MessageListBox.Items.Count - 1]);
            }
            
        }
    }
}
