using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using OllamaPilot.Extensibility.ToolWindows;

namespace OllamaPilot.Extensibility.Commands;

[VisualStudioContribution]
internal sealed class OpenOllamaSettingsCommand : Command
{
    public OpenOllamaSettingsCommand(VisualStudioExtensibility extensibility) : base(extensibility) { }

    public override CommandConfiguration CommandConfiguration => new("Ollama Settings")
    {
        Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.Settings, IconSettings.IconAndText)
    };

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        => await this.Extensibility.Shell().ShowToolWindowAsync<OllamaSettingsToolWindow>(activate: true, cancellationToken);
}
