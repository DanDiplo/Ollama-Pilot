using Microsoft.VisualStudio.Extensibility.UI;
using OllamaPilot.Extensibility.Windows;

namespace OllamaPilot.Extensibility.ToolWindows;

internal sealed class OllamaSettingsToolWindowContent : RemoteUserControl
{
    public OllamaSettingsToolWindowContent(OllamaSettingsWindowData data)
        : base(dataContext: data)
    {
    }
}
