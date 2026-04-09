using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using OllamaPilot.Core.Services;
using OllamaPilot.Extensibility.Commands;
using OllamaPilot.Extensibility.Models;
using OllamaPilot.Extensibility.Services;
using OllamaPilot.Extensibility.Windows;
using OllamaPilot.Ollama.Services;

namespace OllamaPilot.Extensibility;

[VisualStudioContribution]
internal sealed class ExtensionEntrypoint : Extension
{
    [VisualStudioContribution]
    public static MenuConfiguration OllamaPilotMenu => new("OllamaPilot")
    {
        Children =
        [
            MenuChild.Command<OpenOllamaChatCommand>(),
            MenuChild.Separator,
            MenuChild.Command<ExplainCodeCommand>(),
            MenuChild.Command<AddCommentsCommand>(),
            MenuChild.Command<ReviewCurrentFileCommand>(),
            MenuChild.Command<GenerateFileTestsCommand>(),
            MenuChild.Command<SummariseWorkingChangesCommand>(),
            MenuChild.Separator,
            MenuChild.Command<OpenOllamaSettingsCommand>(),
        ],
    };

    [VisualStudioContribution]
    public static CommandGroupConfiguration OllamaPilotMenuPlacement => new(GroupPlacement.KnownPlacements.ExtensionsMenu)
    {
        Children = [GroupChild.Menu(OllamaPilotMenu)],
    };

    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            id: "OllamaPilot.Extensibility.2a8d9894-e434-4e3d-b64f-c9299e9c2a11",
            version: this.ExtensionAssemblyVersion,
            publisherName: "Dan Diplo",
            displayName: "OllamaPilot Extensibility",
            description: "Modern Ollama powered coding copilot for Visual Studio"),
        LoadedWhen = ActivationConstraint.SolutionState(SolutionState.FullyLoaded),
    };

    protected override void InitializeServices(IServiceCollection serviceCollection)
    {
        base.InitializeServices(serviceCollection);

        serviceCollection.AddSingleton<PromptTemplateService>();
        serviceCollection.AddSingleton<OllamaPromptService>();
        serviceCollection.AddSingleton<OllamaResponseParser>();
        serviceCollection.AddSingleton<OllamaResultStore>();

        serviceCollection.AddSingleton<CodeContextService>();
        serviceCollection.AddSingleton<EditorApplyService>();

        serviceCollection.AddSingleton<OllamaSettingsService>();
        serviceCollection.AddSingleton<OllamaDiagnosticsService>();
        serviceCollection.AddSingleton<IOllamaService, OllamaSharpService>();
        serviceCollection.AddSingleton<IOllamaStreamingService, OllamaStreamingService>();
        serviceCollection.AddSingleton<IOllamaChatService, OllamaChatService>();

        serviceCollection.AddSingleton<OllamaResultWindowState>();
        serviceCollection.AddSingleton<OllamaSettingsWindowState>();
    }
}
