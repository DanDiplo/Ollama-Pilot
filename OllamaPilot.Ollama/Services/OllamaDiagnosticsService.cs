using System.Text;

namespace OllamaPilot.Ollama.Services;

public sealed class OllamaDiagnosticsService
{
    private readonly string _logFilePath;
    private readonly object _gate = new();
    private const int MaxValueLength = 400;

    public OllamaDiagnosticsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "OllamaPilot.Extensibility", "logs");
        Directory.CreateDirectory(folder);
        _logFilePath = Path.Combine(folder, "ollama-diagnostics.log");
    }

    public string LogFilePath => _logFilePath;

    public void LogInfo(string eventName, IReadOnlyDictionary<string, object?> data) => WriteEntry("INFO", eventName, data);

    public void LogError(string eventName, Exception exception, IReadOnlyDictionary<string, object?> data)
    {
        Dictionary<string, object?> payload = new(data)
        {
            ["exceptionType"] = exception.GetType().FullName,
            ["message"] = exception.Message
        };

        WriteEntry("ERROR", eventName, payload);
    }

    private void WriteEntry(string level, string eventName, IReadOnlyDictionary<string, object?> data)
    {
        var builder = new StringBuilder();
        builder.Append(DateTimeOffset.UtcNow.ToString("O"));
        builder.Append(" [").Append(level).Append("] ").Append(eventName);

        foreach (var pair in data)
        {
            builder.Append(" | ").Append(pair.Key).Append('=').Append(FormatValue(pair.Value));
        }

        lock (_gate)
        {
            File.AppendAllText(_logFilePath, builder + Environment.NewLine);
        }
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "(null)";
        }

        string text = value.ToString() ?? string.Empty;
        text = text.Replace("\r", "\\r", StringComparison.Ordinal)
                   .Replace("\n", "\\n", StringComparison.Ordinal);

        if (text.Length > MaxValueLength)
        {
            return text[..MaxValueLength] + "...";
        }

        return text;
    }
}
