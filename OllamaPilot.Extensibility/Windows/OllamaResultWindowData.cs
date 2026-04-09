using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace OllamaPilot.Extensibility.Windows;

[DataContract]
internal sealed class OllamaResultWindowData : NotifyPropertyChangedObject
{
    private string _actionName = string.Empty;
    private string _sourceFilePath = string.Empty;
    private string _explanation = string.Empty;
    private string _codeBlock = string.Empty;
    private string _language = string.Empty;
    private string _fullText = string.Empty;
    private string _status = "Ready";
    private bool _isStreaming;
    private string _streamingText = string.Empty;
    private string _thinkingText = string.Empty;
    private string _userPrompt = string.Empty;
    private bool _includeSelection = true;
    private string _conversationText = string.Empty;

    [DataMember] public string ActionName { get => _actionName; set => SetProperty(ref _actionName, value); }
    [DataMember] public string SourceFilePath { get => _sourceFilePath; set => SetProperty(ref _sourceFilePath, value); }
    [DataMember] public string Explanation { get => _explanation; set => SetProperty(ref _explanation, value); }
    [DataMember] public string CodeBlock { get => _codeBlock; set => SetProperty(ref _codeBlock, value); }
    [DataMember] public string Language { get => _language; set => SetProperty(ref _language, value); }
    [DataMember] public string FullText { get => _fullText; set => SetProperty(ref _fullText, value); }
    [DataMember] public string Status { get => _status; set => SetProperty(ref _status, value); }
    [DataMember] public bool IsStreaming { get => _isStreaming; set => SetProperty(ref _isStreaming, value); }
    [DataMember] public string StreamingText { get => _streamingText; set => SetProperty(ref _streamingText, value); }
    [DataMember] public string ThinkingText { get => _thinkingText; set => SetProperty(ref _thinkingText, value); }
    [DataMember] public string UserPrompt { get => _userPrompt; set => SetProperty(ref _userPrompt, value); }
    [DataMember] public bool IncludeSelection { get => _includeSelection; set => SetProperty(ref _includeSelection, value); }
    [DataMember] public string ConversationText { get => _conversationText; set => SetProperty(ref _conversationText, value); }
    [DataMember] public AsyncCommand? SendCommand { get; init; }
    [DataMember] public AsyncCommand? StopCommand { get; init; }
    [DataMember] public AsyncCommand? ClearCommand { get; init; }
    [DataMember] public AsyncCommand? CopyCodeCommand { get; init; }
    [DataMember] public AsyncCommand? CopyResponseCommand { get; init; }
    [DataMember] public AsyncCommand? ApplyCodeCommand { get; init; }
}
