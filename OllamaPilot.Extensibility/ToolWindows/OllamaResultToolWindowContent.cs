using Microsoft.VisualStudio.Extensibility.UI;
using OllamaPilot.Extensibility.Windows;

namespace OllamaPilot.Extensibility.ToolWindows;

internal sealed class OllamaResultToolWindowContent : RemoteUserControl
{
    public OllamaResultToolWindowContent(OllamaResultWindowData data)
        : base(dataContext: data)
    {
    }
}
