using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using OllamaPilot.Core.Models;
using OllamaPilot.Core.Services;
using OllamaPilot.Extensibility.Models;
using OllamaPilot.Extensibility.Services;
using OllamaPilot.Extensibility.Windows;
using OllamaPilot.Ollama.Models;
using OllamaPilot.Ollama.Services;

namespace OllamaPilot.Extensibility.Commands;

[VisualStudioContribution]
internal sealed class AddCommentsCommand : OllamaCommandBase
{
    public AddCommentsCommand(VisualStudioExtensibility extensibility, CodeContextService codeContextService, IOllamaStreamingService streamingService, OllamaPromptService promptService, OllamaResponseParser responseParser, OllamaResultStore resultStore, OllamaResultWindowState windowState, OllamaSettingsService settingsService, OllamaDiagnosticsService diagnosticsService)
        : base(extensibility, codeContextService, streamingService, promptService, responseParser, resultStore, windowState, settingsService, diagnosticsService) { }

    protected override OllamaActionType ActionType => OllamaActionType.AddComments;
    protected override ResponseParsingMode ParsingMode => ResponseParsingMode.CodeRequired;
    protected override bool UseChatModel => true;
    protected override ThinkingDepth? ThinkingDepthOverride => ThinkingDepth.Off;
    protected override string BuildPrompt(CodeContext codeContext) => PromptService.BuildAddCommentsChatPrompt(codeContext);
    protected override IAsyncEnumerable<OllamaStreamingUpdate> StreamResponseAsync(CodeContext codeContext, string prompt, CancellationToken cancellationToken)
        => StreamingService.StreamStructuredChatAsync(
            PromptService.BuildAddCommentsSystemPrompt(codeContext.Language),
            prompt,
            useChatModel: true,
            cancellationToken,
            ThinkingDepthOverride);

    public override CommandConfiguration CommandConfiguration => new("Add Comments")
    {
        Icon = new CommandIconConfiguration(ImageMoniker.KnownValues.AddComment, IconSettings.IconAndText)
    };

    protected override OllamaParsedResponse PostProcessResponse(CodeContext codeContext, OllamaParsedResponse parsed)
    {
        if (!parsed.HasCodeBlock || string.IsNullOrWhiteSpace(codeContext.EffectiveCode))
        {
            return parsed;
        }

        bool invalidRewrite = CommentOnlyValidator.LooksLikeInvalidRewrite(codeContext.EffectiveCode, parsed.CodeBlock);
        if (!invalidRewrite)
        {
            return parsed;
        }

        return new OllamaParsedResponse
        {
            FullText = parsed.FullText,
            Explanation = parsed.Explanation,
            CodeBlock = parsed.CodeBlock,
            IsApplyReady = false,
            Language = parsed.Language,
            ActionName = parsed.ActionName,
            SourceFilePath = parsed.SourceFilePath,
            ApplyTarget = parsed.ApplyTarget
        };
    }

    protected override string? GetNonApplyReadyStatusMessage(OllamaParsedResponse parsed) =>
        parsed.HasCodeBlock
            ? "Comment-only response changed code structure. Review or copy manually."
            : "Model did not return apply-ready code. See log for details.";
}
