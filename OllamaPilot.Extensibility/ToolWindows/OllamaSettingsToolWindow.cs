using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using OllamaPilot.Extensibility.Windows;

namespace OllamaPilot.Extensibility.ToolWindows;

[VisualStudioContribution]
internal sealed class OllamaSettingsToolWindow : ToolWindow
{
    private readonly OllamaSettingsToolWindowContent _content;

    public OllamaSettingsToolWindow(VisualStudioExtensibility extensibility, OllamaSettingsWindowState state)
        : base(extensibility)
    {
        Title = "Ollama Settings";
        _content = new OllamaSettingsToolWindowContent(state.Data);
    }

    public override ToolWindowConfiguration ToolWindowConfiguration => new()
    {
        Placement = ToolWindowPlacement.Floating
    };

    public override Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        => Task.FromResult<IRemoteUserControl>(_content);
}
