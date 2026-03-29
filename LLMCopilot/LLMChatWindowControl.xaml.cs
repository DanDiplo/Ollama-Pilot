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
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Shell;

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
    public partial class LLMChatWindowControl : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<MyMessage> _messages = new ObservableCollection<MyMessage>();
        public ObservableCollection<MyMessage> Messages => _messages;
        private Chat Chat { get; set; }
        private StringBuilder _messageCache = new StringBuilder(); // 用于缓存数据
        private CancellationTokenSource _sendCancellationTokenSource;
        private string _statusText = "Ready to connect to your local Ollama server.";
        private Brush _statusBrush = new SolidColorBrush(Colors.Gray);
        private string _chatModelBadge;
        private string _completionModelBadge;
        private string _autoCompleteBadge;
        private string _draftHintText = "Tip: Insert selection to give the model exact code context.";
        private bool _canCancelResponse;
        private bool _canRegenerateLast;
        private string _lastSubmittedText;
        private string _lastPromptOverride;
        private ChatRole _lastSubmittedRole = ChatRole.User;

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set
            {
                if (_statusBrush != value)
                {
                    _statusBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ChatModelBadge
        {
            get => _chatModelBadge;
            private set
            {
                if (_chatModelBadge != value)
                {
                    _chatModelBadge = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CompletionModelBadge
        {
            get => _completionModelBadge;
            private set
            {
                if (_completionModelBadge != value)
                {
                    _completionModelBadge = value;
                    OnPropertyChanged();
                }
            }
        }

        public string AutoCompleteBadge
        {
            get => _autoCompleteBadge;
            private set
            {
                if (_autoCompleteBadge != value)
                {
                    _autoCompleteBadge = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DraftHintText
        {
            get => _draftHintText;
            private set
            {
                if (_draftHintText != value)
                {
                    _draftHintText = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanCancelResponse
        {
            get => _canCancelResponse;
            private set
            {
                if (_canCancelResponse != value)
                {
                    _canCancelResponse = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanRegenerateLast
        {
            get => _canRegenerateLast;
            private set
            {
                if (_canRegenerateLast != value)
                {
                    _canRegenerateLast = value;
                    OnPropertyChanged();
                }
            }
        }
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
            Chat = OllamaClientFactory.CreateChat(OnChatResponseReceived);
            this.Unloaded += LLMChatWindowControl_Unloaded;
            this.Loaded += LLMChatWindowControl_loaded;
            RefreshHeaderState();
        }

        private void OnChatResponseReceived(ChatResponseStream response)
        {
            if (response.Message != null && response.Message.Content != null)
            {
                // 将新内容添加到缓存
                _messageCache.Append(response.Message.Content);

                string[] delimeters = {"\n", ",", "，", ".", "。", ":", "：", ";", "；", "\t"};

                bool containsKeyword = delimeters.Any(keyword => response.Message.Content.Contains(keyword));
                // 检查是否需要更新消息列表
                if (_messageCache.Length > 64 || containsKeyword || response.Done)
                {
                    AppendOrUpdateLastMessage(_messageCache.ToString());
                    _messageCache.Clear(); // 清空缓存
                }
            }
        }

        private void ClearChatHistory_Click(object sender, RoutedEventArgs e)
        {
            _messages.Clear();
            UpdateStatus("Chat history cleared.", Colors.Gray);
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            SettingsCommand.Instance.Execute(this, EventArgs.Empty);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void ListModels_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var client = OllamaClientFactory.CreateClient();
                var models = await client.ListLocalModelsAsync();
                var modelNames = string.Join("  \n", models.Select(m => m.Name));

                AddMessage(ChatRole.System, $"Available local models:  \n{modelNames}");
                UpdateStatus($"Found {models.Count()} local model(s).", Colors.SeaGreen);

            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex, "Unable to list Ollama models. Check the base URL and whether Ollama is running.");
                UpdateStatus("Unable to list models from Ollama.", Colors.OrangeRed);
            }
        }


        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Event handlers require async void.")]
        private async void OnExplainCodeCommandExecuted(object sender, CommandExecutedEventArgs e)
        {
            await SendChatMessageAsync(e.SelectedText, ChatRole.System, e.PromptOverride);
        }

        private void LLMChatWindowControl_loaded(object sender, RoutedEventArgs e)
        {
            EventManager.CodeCommandExecuted += OnExplainCodeCommandExecuted;
            
            Chat.Client.SelectedModel = OllamaHelper.Instance.Options.ChatModel;
            Chat.Options = OllamaHelper.Instance.ChatRequestOptions;
            Chat.Client.SetAuthorizationHeader(OllamaHelper.Instance.Options.AccessToken);
            RefreshHeaderState();
        }

        private void LLMChatWindowControl_Unloaded(object sender, RoutedEventArgs e)
        {
            EventManager.CodeCommandExecuted -= OnExplainCodeCommandExecuted;
            _sendCancellationTokenSource?.Cancel();
        }

        private void AppendOrUpdateLastMessage(string content)
        {
            if (!Dispatcher.CheckAccess())
            {
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    AppendOrUpdateLastMessage(content);
                });
                return;
            }

            if (_messages.Any() && _messages.Last().Role == ChatRole.Assistant)
            {
                var lastMessage = _messages.Last();
                lastMessage.Content += content;
            }
            else
            {
                _messages.Add(new MyMessage(ChatRole.Assistant, content));
            }

            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            if (!Dispatcher.CheckAccess())
            {
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ScrollToBottom();
                });
                return;
            }

            if (VisualTreeHelper.GetChildrenCount(MessagesScrollViewer) > 0)
            {
                MessagesScrollViewer.ScrollToEnd();
            }
        }

        private void AddMessage(ChatRole role, string Content)
        {
            if (!Dispatcher.CheckAccess())
            {
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    AddMessage(role, Content);
                });
                return;
            }

            MyMessage userMessage = new MyMessage(role, Content);
            _messages.Add(userMessage);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        public void KeepLastTenMessages()
        {
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                await SendMessageAsync();
            }
        }

        private void MessageTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (MessageTextBox.Text == PlaceholderText)
            {
                MessageTextBox.Text = "";
                MessageTextBox.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void MessageTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                MessageTextBox.Text = PlaceholderText;
                MessageTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        public async Task SendChatMessageAsync(string text, ChatRole role, string promptOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(text) && !VsHelpers.IsSending)
            {
                VsHelpers.IsSending = true;
                SendButton.Content = "Answering...";
                SendButton.IsEnabled = false;
                CanCancelResponse = true;
                UpdateStatus($"Asking {OllamaHelper.Instance.Options.ChatModel}...", Colors.DodgerBlue);
                RememberLastRequest(text, role, promptOverride);

                AddMessage(role, text);

                ScrollToBottom();

                _sendCancellationTokenSource?.Cancel();
                _sendCancellationTokenSource = new CancellationTokenSource();
                _sendCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(60));
                try
                {
                    await Task.Run(async () =>
                    {
                        await Chat.SendAsync(promptOverride ?? text, _sendCancellationTokenSource.Token);
                    });
                }
                catch (Exception ex)
                {
                    LLMErrorHandler.HandleException(ex, "Unable to get a response from Ollama. Check the configured URL, access token, and model names.");
                    UpdateStatus("The chat request failed. Check Ollama connectivity and model names.", Colors.OrangeRed);
                }


                // 确保在任何情况下都重置状态
                VsHelpers.IsSending = false;
                SendButton.Content = "Send";
                SendButton.IsEnabled = true;
                _sendCancellationTokenSource = null;
                CanCancelResponse = false;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    UpdateStatus($"Response ready from {OllamaHelper.Instance.Options.ChatModel}.", Colors.SeaGreen);
                }

                ScrollToBottom();

                
            }
        }

        private async Task SendMessageAsync()
        {
            string text = MessageTextBox.Text;
            if (string.Equals(text, PlaceholderText, StringComparison.Ordinal))
            {
                return;
            }

            MessageTextBox.Clear();
            await SendChatMessageAsync(text, ChatRole.User);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void ReviewFile_Click(object sender, RoutedEventArgs e)
        {
            var documentPath = await VsHelpers.GetActiveDocumentPathAsync(LLMCopilotProvider.Package);
            var documentText = await VsHelpers.GetActiveDocumentTextAsync(LLMCopilotProvider.Package);
            if (string.IsNullOrWhiteSpace(documentText))
            {
                UpdateStatus("Open a code file before reviewing it.", Colors.DarkGoldenrod);
                return;
            }

            var promptText = OllamaHelper.Instance.GetReviewFileTemplate(CurrentDocumentCommandExecutor.PrepareDocumentForPrompt(documentText), documentPath ?? string.Empty);
            var fileLabel = string.IsNullOrWhiteSpace(documentPath) ? "current file" : System.IO.Path.GetFileName(documentPath);
            DraftHintText = "File review request sent with the active document content.";
            await SendChatMessageAsync($"Review the current file: {fileLabel}", ChatRole.User, promptText);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void GenerateTestsForFile_Click(object sender, RoutedEventArgs e)
        {
            var documentPath = await VsHelpers.GetActiveDocumentPathAsync(LLMCopilotProvider.Package);
            var documentText = await VsHelpers.GetActiveDocumentTextAsync(LLMCopilotProvider.Package);
            if (string.IsNullOrWhiteSpace(documentText))
            {
                UpdateStatus("Open a code file before generating tests for it.", Colors.DarkGoldenrod);
                return;
            }

            var promptText = OllamaHelper.Instance.GetGenerateFileTestsTemplate(CurrentDocumentCommandExecutor.PrepareDocumentForPrompt(documentText), documentPath ?? string.Empty);
            var fileLabel = string.IsNullOrWhiteSpace(documentPath) ? "current file" : System.IO.Path.GetFileName(documentPath);
            DraftHintText = "File test generation request sent with the active document content.";
            await SendChatMessageAsync($"Generate tests for the current file: {fileLabel}", ChatRole.User, promptText);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void SummarizeChanges_Click(object sender, RoutedEventArgs e)
        {
            var changesContext = await GitContextHelper.TryGetChangesContextAsync(LLMCopilotProvider.Package);
            if (changesContext == null)
            {
                UpdateStatus("No Git changes were found from the current solution or document context.", Colors.DarkGoldenrod);
                return;
            }

            var repoName = System.IO.Path.GetFileName(changesContext.RepositoryRoot);
            var promptText = OllamaHelper.Instance.GetSummarizeChangesTemplate(
                changesContext.RepositoryRoot,
                changesContext.StatusText,
                changesContext.DiffText);
            DraftHintText = "Change summary request sent with the current Git diff.";
            await SendChatMessageAsync($"Summarize the current Git changes for {repoName}.", ChatRole.User, promptText);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void ExplainError_Click(object sender, RoutedEventArgs e)
        {
            var textView = await VsHelpers.GetActiveTextViewAsync(LLMCopilotProvider.Package);
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (textView == null)
            {
                UpdateStatus("Open a code editor before asking for an error diagnosis.", Colors.DarkGoldenrod);
                return;
            }

            var selectedText = VsHelpers.GetSelectedText(textView);
            var codeContext = !string.IsNullOrWhiteSpace(selectedText)
                ? selectedText
                : VsHelpers.GetContextAroundCaret(textView, 6, 6);
            if (string.IsNullOrWhiteSpace(codeContext))
            {
                UpdateStatus("Select code or place the caret on the problem line first.", Colors.DarkGoldenrod);
                return;
            }

            var draftDetails = GetDraftInstructionText();
            var currentLine = VsHelpers.GetCurrentLineText(textView);
            var diagnosticDetails = string.IsNullOrWhiteSpace(draftDetails) ? currentLine : draftDetails;
            var fileName = await VsHelpers.GetActiveDocumentFileNameAsync(LLMCopilotProvider.Package) ?? "current file";
            var prompt = OllamaHelper.Instance.GetDiagnoseErrorsTemplate(codeContext, fileName, diagnosticDetails);
            var visibleMessage = string.IsNullOrWhiteSpace(diagnosticDetails)
                ? $"Explain the likely error in {fileName}."
                : $"Explain the likely error in {fileName}: {diagnosticDetails}";

            MessageTextBox.Text = PlaceholderText;
            MessageTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            DraftHintText = "Error diagnosis request sent with current editor context.";
            await SendChatMessageAsync(visibleMessage, ChatRole.User, prompt);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void EditSelection_Click(object sender, RoutedEventArgs e)
        {
            var textView = await VsHelpers.GetActiveTextViewAsync(LLMCopilotProvider.Package);
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var selectedText = textView != null ? VsHelpers.GetSelectedText(textView) : null;
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                UpdateStatus("Select code in the active editor before asking for an edit.", Colors.DarkGoldenrod);
                return;
            }

            var instructions = GetDraftInstructionText();
            if (string.IsNullOrWhiteSpace(instructions))
            {
                UpdateStatus("Describe the change you want, then use Edit Selection.", Colors.DarkGoldenrod);
                return;
            }

            var fileName = await VsHelpers.GetActiveDocumentFileNameAsync(LLMCopilotProvider.Package) ?? "current file";
            var prompt = OllamaHelper.Instance.GetEditSelectionTemplate(selectedText, fileName, instructions);
            var visibleMessage = $"Edit selection in {fileName}: {instructions}";

            MessageTextBox.Text = PlaceholderText;
            MessageTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            DraftHintText = "Edit request sent with the active editor selection.";
            await SendChatMessageAsync(visibleMessage, ChatRole.User, prompt);
        }


        private void MessagesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void PreviewDiff_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.Tag is MyMessage message))
            {
                return;
            }

            var updatedCode = GetEditorInsertionText(message.Content);
            if (string.IsNullOrWhiteSpace(updatedCode))
            {
                VsHelpers.ShowInfo("There is no generated code to compare.");
                return;
            }

            var textView = await VsHelpers.GetActiveTextViewAsync(LLMCopilotProvider.Package);
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var selectedText = textView != null ? VsHelpers.GetSelectedText(textView) : null;
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                selectedText = await VsHelpers.GetActiveDocumentTextAsync(LLMCopilotProvider.Package);
                if (string.IsNullOrWhiteSpace(selectedText))
                {
                    VsHelpers.ShowInfo("Open a code editor before previewing a diff.");
                    return;
                }
            }

            var fileName = await VsHelpers.GetActiveDocumentFileNameAsync(LLMCopilotProvider.Package) ?? "current file";
            var diffPreview = DiffPreviewBuilder.BuildUnifiedDiff(selectedText, updatedCode, fileName);
            AddMessage(ChatRole.System, $"Preview diff for {fileName}:{Environment.NewLine}{Environment.NewLine}{diffPreview}");
            UpdateStatus("Added a diff preview to the chat history.", Colors.SeaGreen);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void InsertIntoEditor_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.Tag is MyMessage message))
            {
                return;
            }

            var content = GetEditorInsertionText(message.Content);
            if (string.IsNullOrWhiteSpace(content))
            {
                VsHelpers.ShowInfo("There is no generated content to insert into the editor.");
                return;
            }

            var inserted = await VsHelpers.InsertTextIntoActiveDocumentAsync(LLMCopilotProvider.Package, content);
            if (!inserted)
            {
                VsHelpers.ShowInfo("Open a code editor before inserting generated content.");
            }
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void ReplaceSelection_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.Tag is MyMessage message))
            {
                return;
            }

            var content = GetEditorInsertionText(message.Content);
            if (string.IsNullOrWhiteSpace(content))
            {
                VsHelpers.ShowInfo("There is no generated content to apply to the editor.");
                return;
            }

            var replaced = await VsHelpers.ReplaceSelectionInActiveDocumentAsync(LLMCopilotProvider.Package, content);
            if (!replaced)
            {
                VsHelpers.ShowInfo("Select code in the active editor before replacing it.");
            }
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void ReplaceFile_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.Tag is MyMessage message))
            {
                return;
            }

            var content = GetEditorInsertionText(message.Content);
            if (string.IsNullOrWhiteSpace(content))
            {
                VsHelpers.ShowInfo("There is no generated content to apply to the current file.");
                return;
            }

            var replaced = await VsHelpers.ReplaceActiveDocumentAsync(LLMCopilotProvider.Package, content);
            if (!replaced)
            {
                VsHelpers.ShowInfo("Open a code editor before replacing the current file.");
                return;
            }

            UpdateStatus("Replaced the current file with generated content.", Colors.SeaGreen);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void CreateSiblingFile_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.Tag is MyMessage message))
            {
                return;
            }

            var content = GetEditorInsertionText(message.Content);
            if (string.IsNullOrWhiteSpace(content))
            {
                VsHelpers.ShowInfo("There is no generated content to create a sibling file from.");
                return;
            }

            var siblingPath = await VsHelpers.CreateSiblingFileFromActiveDocumentAsync(LLMCopilotProvider.Package, content);
            if (string.IsNullOrWhiteSpace(siblingPath))
            {
                VsHelpers.ShowInfo("Open a code editor before creating a sibling file.");
                return;
            }

            UpdateStatus($"Created {System.IO.Path.GetFileName(siblingPath)} next to the active file.", Colors.SeaGreen);
        }

        private static string GetEditorInsertionText(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            if (!TryExtractFirstCodeBlock(content, out var fencedCode))
            {
                return content.Trim();
            }

            return fencedCode;
        }

        private static bool TryExtractFirstCodeBlock(string content, out string code)
        {
            code = null;
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            var fencedCodeMatch = Regex.Match(
                content,
                "```(?:[^\\r\\n`]*)\\r?\\n([\\s\\S]*?)```",
                RegexOptions.Singleline);

            if (!fencedCodeMatch.Success)
            {
                return false;
            }

            code = fencedCodeMatch.Groups[1].Value.Trim('\r', '\n');
            return !string.IsNullOrWhiteSpace(code);
        }

        private string GetDraftInstructionText()
        {
            var text = MessageTextBox.Text;
            if (string.Equals(text, PlaceholderText, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return text?.Trim() ?? string.Empty;
        }

        private const string PlaceholderText = "Ask Ollama for an explanation, refactor, test, or paste selected code into the prompt.";

        private void RefreshHeaderState()
        {
            var options = OllamaHelper.Instance.Options;
            ChatModelBadge = $"Chat: {options.ChatModel}";
            CompletionModelBadge = $"Completion: {options.CompleteModel}";
            var autoCompleteMode = options.EnableAutoComplete
                ? $"Autocomplete: {options.AutoCompleteTriggerMode} ({options.AutoCompleteDelayMs} ms)"
                : "Autocomplete: Off";
            AutoCompleteBadge = autoCompleteMode;
        }

        private void UpdateStatus(string message, Color color)
        {
            StatusText = message;
            StatusBrush = new SolidColorBrush(color);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Testing Ollama connection...", Colors.DodgerBlue);
            var validation = await OllamaSettingsValidator.ValidateAsync(OllamaHelper.Instance.Options);
            UpdateStatus(validation.Message, validation.Success ? Colors.SeaGreen : Colors.OrangeRed);
        }

        private void CancelResponse_Click(object sender, RoutedEventArgs e)
        {
            _sendCancellationTokenSource?.Cancel();
            CanCancelResponse = false;
            UpdateStatus("Canceled the current chat response.", Colors.DarkGoldenrod);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void InsertSelectionIntoPrompt_Click(object sender, RoutedEventArgs e)
        {
            var textView = await VsHelpers.GetActiveTextViewAsync(LLMCopilotProvider.Package);
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var selectedText = textView != null ? VsHelpers.GetSelectedText(textView) : null;
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                UpdateStatus("Select code in the active editor to insert it into the prompt.", Colors.DarkGoldenrod);
                return;
            }

            if (MessageTextBox.Text == PlaceholderText)
            {
                MessageTextBox.Clear();
            }

            var fileName = await VsHelpers.GetActiveDocumentFileNameAsync(LLMCopilotProvider.Package) ?? "current file";
            var snippet = $"Please work on this code from {fileName}:{Environment.NewLine}```{Environment.NewLine}{selectedText}{Environment.NewLine}```{Environment.NewLine}{Environment.NewLine}";
            MessageTextBox.Text = string.Concat(snippet, MessageTextBox.Text ?? string.Empty);
            MessageTextBox.Foreground = new SolidColorBrush(Colors.Black);
            MessageTextBox.Focus();
            MessageTextBox.CaretIndex = MessageTextBox.Text.Length;
            DraftHintText = "Selected code was inserted into the prompt.";
            UpdateStatus("Added the active selection to your draft.", Colors.SeaGreen);
        }

        private void ClearDraft_Click(object sender, RoutedEventArgs e)
        {
            MessageTextBox.Text = PlaceholderText;
            MessageTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            DraftHintText = "Tip: Insert selection to give the model exact code context.";
            UpdateStatus("Draft cleared.", Colors.Gray);
        }

        private void CopyCode_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.Tag is MyMessage message))
            {
                return;
            }

            var content = GetEditorInsertionText(message.Content);
            if (string.IsNullOrWhiteSpace(content))
            {
                VsHelpers.ShowInfo("There is no generated content to copy.");
                return;
            }

            Clipboard.SetText(content);
            UpdateStatus("Copied generated code to the clipboard.", Colors.SeaGreen);
        }

        private void UseAsDraft_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.Tag is MyMessage message))
            {
                return;
            }

            var draftText = message.Content?.Trim();
            if (string.IsNullOrWhiteSpace(draftText))
            {
                VsHelpers.ShowInfo("There is no generated content to reuse.");
                return;
            }

            if (TryExtractFirstCodeBlock(message.Content, out var codeBlock))
            {
                draftText = $"Please revise or explain this generated code:{Environment.NewLine}```{Environment.NewLine}{codeBlock}{Environment.NewLine}```";
            }

            MessageTextBox.Text = draftText;
            MessageTextBox.Foreground = new SolidColorBrush(Colors.Black);
            MessageTextBox.Focus();
            MessageTextBox.Select(MessageTextBox.Text.Length, 0);
            DraftHintText = "Assistant output was copied into the draft for follow-up.";
            UpdateStatus("Reply loaded into the draft box.", Colors.SeaGreen);
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void RegenerateLast_Click(object sender, RoutedEventArgs e)
        {
            if (!CanRegenerateLast || string.IsNullOrWhiteSpace(_lastSubmittedText))
            {
                UpdateStatus("There is no previous request to regenerate yet.", Colors.DarkGoldenrod);
                return;
            }

            DraftHintText = "Regenerating the most recent request with the same prompt context.";
            await SendChatMessageAsync(_lastSubmittedText, _lastSubmittedRole, _lastPromptOverride);
        }

        private void RememberLastRequest(string text, ChatRole role, string promptOverride)
        {
            _lastSubmittedText = text;
            _lastSubmittedRole = role;
            _lastPromptOverride = promptOverride;
            CanRegenerateLast = !string.IsNullOrWhiteSpace(text);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

    public class AssistantMessageVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is ChatRole role && role == ChatRole.Assistant
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
