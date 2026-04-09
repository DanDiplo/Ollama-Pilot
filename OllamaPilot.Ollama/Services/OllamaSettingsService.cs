using System.Text.Json;
using OllamaPilot.Ollama.Models;

namespace OllamaPilot.Ollama.Services;

public sealed class OllamaSettingsService
{
    private readonly string _settingsFilePath;
    private readonly object _gate = new();
    private OllamaClientSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public OllamaSettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "OllamaPilot.Extensibility");
        Directory.CreateDirectory(folder);
        _settingsFilePath = Path.Combine(folder, "settings.json");
        _settings = LoadSettings();
    }

    public OllamaClientSettings Settings
    {
        get
        {
            lock (_gate)
            {
                return Clone(_settings);
            }
        }
    }

    public OllamaClientSettings SaveSettings(OllamaClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        OllamaClientSettings normalized = Normalize(settings);
        string json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(_settingsFilePath, json);

        lock (_gate)
        {
            _settings = Clone(normalized);
            return Clone(_settings);
        }
    }

    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(IOllamaService ollamaService, CancellationToken cancellationToken)
    {
        var settings = Settings;
        IReadOnlyList<Model> models = await ollamaService.ListLocalModelsAsync(settings.BaseUrl, settings.AccessToken, cancellationToken);
        return models.Select(model => model.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private OllamaClientSettings LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return Normalize(new OllamaClientSettings());
        }

        try
        {
            string json = File.ReadAllText(_settingsFilePath);
            OllamaClientSettings? settings = JsonSerializer.Deserialize<OllamaClientSettings>(json);
            return Normalize(settings ?? new OllamaClientSettings());
        }
        catch
        {
            return Normalize(new OllamaClientSettings());
        }
    }

    private static OllamaClientSettings Normalize(OllamaClientSettings settings)
    {
        settings.BaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "http://localhost:11434" : settings.BaseUrl.Trim();
        settings.AccessToken = string.IsNullOrWhiteSpace(settings.AccessToken) ? null : settings.AccessToken.Trim();
        settings.SelectedModel = string.IsNullOrWhiteSpace(settings.SelectedModel) ? "qwen2.5-coder:7b" : settings.SelectedModel.Trim();
        settings.SelectedChatModel = string.IsNullOrWhiteSpace(settings.SelectedChatModel) ? settings.SelectedModel : settings.SelectedChatModel.Trim();
        settings.DefaultTemperature = Math.Clamp(settings.DefaultTemperature, 0.0f, 2.0f);
        settings.ChatTemperature = Math.Clamp(settings.ChatTemperature, 0.0f, 2.0f);
        settings.ChatContextWindow = settings.ChatContextWindow <= 0 ? 4096 : settings.ChatContextWindow;
        settings.MaxOutputTokens = settings.MaxOutputTokens <= 0 ? 2048 : settings.MaxOutputTokens;
        return settings;
    }

    private static OllamaClientSettings Clone(OllamaClientSettings settings) =>
        new()
        {
            BaseUrl = settings.BaseUrl,
            AccessToken = settings.AccessToken,
            SelectedModel = settings.SelectedModel,
            SelectedChatModel = settings.SelectedChatModel,
            DefaultTemperature = settings.DefaultTemperature,
            ChatTemperature = settings.ChatTemperature,
            ChatContextWindow = settings.ChatContextWindow,
            MaxOutputTokens = settings.MaxOutputTokens,
            IncludeSelectionByDefault = settings.IncludeSelectionByDefault,
            ChatThinkingDepth = settings.ChatThinkingDepth
        };
}
