using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;
using OllamaPilot.Ollama.Models;
using OllamaPilot.Ollama.Services;

namespace OllamaPilot.Extensibility.Windows;

internal sealed class OllamaSettingsWindowState
{
    public OllamaSettingsWindowData Data { get; }

    private readonly OllamaSettingsService _settingsService;
    private readonly IOllamaService _ollamaService;

    public OllamaSettingsWindowState(OllamaSettingsService settingsService, IOllamaService ollamaService)
    {
        _settingsService = settingsService;
        _ollamaService = ollamaService;

        Data = new OllamaSettingsWindowData
        {
            SaveCommand = new AsyncCommand(SaveAsync),
            RefreshModelsCommand = new AsyncCommand(RefreshModelsAsync),
            TestConnectionCommand = new AsyncCommand(TestConnectionAsync)
        };

        LoadFromSettings(_settingsService.Settings);
    }

    private async Task RefreshModelsAsync(object? parameter, IClientContext? clientContext, CancellationToken cancellationToken)
    {
        try
        {
            Data.Status = "Refreshing models...";
            IReadOnlyList<string> models = await _settingsService.GetAvailableModelsAsync(_ollamaService, cancellationToken);
            Data.AvailableModels.Clear();
            foreach (string model in models)
            {
                Data.AvailableModels.Add(model);
            }

            Data.Status = models.Count == 0 ? "No models were returned by Ollama." : "Models refreshed.";
        }
        catch (Exception ex)
        {
            Data.Status = ex.Message;
        }
    }

    private Task SaveAsync(object? parameter, IClientContext clientContext, CancellationToken cancellationToken)
    {
        _settingsService.SaveSettings(ToSettings());
        Data.Status = "Settings saved.";
        return Task.CompletedTask;
    }

    private async Task TestConnectionAsync(object? parameter, IClientContext? clientContext, CancellationToken cancellationToken)
    {
        try
        {
            Data.Status = "Testing connection...";
            OllamaClientSettings snapshot = ToSettings();
            IReadOnlyList<Model> models = await _ollamaService.ListLocalModelsAsync(snapshot.BaseUrl, snapshot.AccessToken, cancellationToken);
            Data.Status = $"Connected. {models.Count} model(s) detected.";
        }
        catch (Exception ex)
        {
            Data.Status = $"Connection failed: {ex.Message}";
        }
    }

    private void LoadFromSettings(OllamaClientSettings settings)
    {
        Data.BaseUrl = settings.BaseUrl;
        Data.AccessToken = settings.AccessToken ?? string.Empty;
        Data.SelectedModel = settings.SelectedModel;
        Data.SelectedChatModel = settings.SelectedChatModel;
        Data.DefaultTemperature = settings.DefaultTemperature.ToString("0.0");
        Data.ChatTemperature = settings.ChatTemperature.ToString("0.0");
        Data.MaxOutputTokens = settings.MaxOutputTokens.ToString();
        Data.ChatContextWindow = settings.ChatContextWindow.ToString();
        Data.IncludeSelectionByDefault = settings.IncludeSelectionByDefault;
    }

    private OllamaClientSettings ToSettings() =>
        new()
        {
            BaseUrl = Data.BaseUrl,
            AccessToken = Data.AccessToken,
            SelectedModel = Data.SelectedModel,
            SelectedChatModel = Data.SelectedChatModel,
            DefaultTemperature = float.TryParse(Data.DefaultTemperature, out float defaultTemp) ? defaultTemp : 0.2f,
            ChatTemperature = float.TryParse(Data.ChatTemperature, out float chatTemp) ? chatTemp : 0.3f,
            MaxOutputTokens = int.TryParse(Data.MaxOutputTokens, out int maxOutput) ? maxOutput : 2048,
            ChatContextWindow = int.TryParse(Data.ChatContextWindow, out int chatCtx) ? chatCtx : 4096,
            IncludeSelectionByDefault = Data.IncludeSelectionByDefault
        };
}
