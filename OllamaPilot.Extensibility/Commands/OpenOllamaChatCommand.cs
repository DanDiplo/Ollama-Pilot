using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using OllamaPilot.Extensibility.ToolWindows;

namespace OllamaPilot.Extensibility.Commands;

[VisualStudioContribution]
internal sealed class OpenOllamaChatCommand : Command
{
    public OpenOllamaChatCommand(VisualStudioExtensibility extensibility) : base(extensibility) { }

    public override CommandConfiguration CommandConfiguration => new("Open Ollama Chat")
    {
        Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.Extension, IconSettings.IconAndText)
    };

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        => await this.Extensibility.Shell().ShowToolWindowAsync<OllamaResultToolWindow>(activate: true, cancellationToken);
}
