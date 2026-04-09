using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using OllamaPilot.Extensibility.Windows;

namespace OllamaPilot.Extensibility.ToolWindows;

[VisualStudioContribution]
internal sealed class OllamaResultToolWindow : ToolWindow
{
    private readonly OllamaResultToolWindowContent _content;

    public OllamaResultToolWindow(VisualStudioExtensibility extensibility, OllamaResultWindowState state)
        : base(extensibility)
    {
        Title = "Ollama Chat";
        _content = new OllamaResultToolWindowContent(state.Data);
    }

    public override ToolWindowConfiguration ToolWindowConfiguration => new()
    {
        Placement = ToolWindowPlacement.Floating
    };

    public override Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        => Task.FromResult<IRemoteUserControl>(_content);
}
