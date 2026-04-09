using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace OllamaPilot.Extensibility.Windows;

[DataContract]
internal sealed class OllamaSettingsWindowData : NotifyPropertyChangedObject
{
    private string _baseUrl = "http://localhost:11434";
    private string _accessToken = string.Empty;
    private string _selectedModel = "qwen2.5-coder:7b";
    private string _selectedChatModel = "qwen2.5-coder:7b";
    private string _defaultTemperature = "0.2";
    private string _chatTemperature = "0.3";
    private string _maxOutputTokens = "2048";
    private string _chatContextWindow = "4096";
    private bool _includeSelectionByDefault = true;
    private string _status = "Ready";

    [DataMember] public string BaseUrl { get => _baseUrl; set => SetProperty(ref _baseUrl, value); }
    [DataMember] public string AccessToken { get => _accessToken; set => SetProperty(ref _accessToken, value); }
    [DataMember] public string SelectedModel { get => _selectedModel; set => SetProperty(ref _selectedModel, value); }
    [DataMember] public string SelectedChatModel { get => _selectedChatModel; set => SetProperty(ref _selectedChatModel, value); }
    [DataMember] public string DefaultTemperature { get => _defaultTemperature; set => SetProperty(ref _defaultTemperature, value); }
    [DataMember] public string ChatTemperature { get => _chatTemperature; set => SetProperty(ref _chatTemperature, value); }
    [DataMember] public string MaxOutputTokens { get => _maxOutputTokens; set => SetProperty(ref _maxOutputTokens, value); }
    [DataMember] public string ChatContextWindow { get => _chatContextWindow; set => SetProperty(ref _chatContextWindow, value); }
    [DataMember] public bool IncludeSelectionByDefault { get => _includeSelectionByDefault; set => SetProperty(ref _includeSelectionByDefault, value); }
    [DataMember] public string Status { get => _status; set => SetProperty(ref _status, value); }
    [DataMember] public ObservableCollection<string> AvailableModels { get; } = [];
    [DataMember] public AsyncCommand? SaveCommand { get; init; }
    [DataMember] public AsyncCommand? RefreshModelsCommand { get; init; }
    [DataMember] public AsyncCommand? TestConnectionCommand { get; init; }
}
