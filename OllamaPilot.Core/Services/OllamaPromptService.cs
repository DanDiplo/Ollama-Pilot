using System.Text;
using OllamaPilot.Core.Helpers;
using OllamaPilot.Core.Models;

namespace OllamaPilot.Core.Services;

public sealed class OllamaPromptService
{
    private readonly PromptTemplateService _templateService;

    public OllamaPromptService(PromptTemplateService templateService)
    {
        _templateService = templateService;
    }

    public string BuildActionPrompt(OllamaActionType actionType, CodeContext context)
    {
        string templateFileName = actionType switch
        {
            OllamaActionType.ExplainCode => "explain-code.rdt.md",
            OllamaActionType.AddComments => "document-code.rdt.md",
            OllamaActionType.ReviewCurrentFile => "review-file.rdt.md",
            OllamaActionType.GenerateFileTests => "generate-file-tests.rdt.md",
            _ => throw new InvalidOperationException($"Action '{actionType}' is not supported by the prompt service.")
        };

        string language = context.Language ?? "text";
        string prompt = RenderRequiredTemplate(
            templateFileName,
            new Dictionary<string, string?>
            {
                ["selectedText"] = context.EffectiveCode,
                ["language"] = language,
                ["location"] = Path.GetFileName(context.FilePath ?? string.Empty)
            });

        return ReplaceFirst(prompt, "```", $"```{language}");
    }

    public string BuildAddCommentsChatPrompt(CodeContext context)
    {
        string language = context.Language ?? "text";
        string languageName = LanguageHelper.GetLanguageName(language);
        string fenceHeader = LanguageHelper.GetFenceHeader(language);

        var builder = new StringBuilder();
        builder.AppendLine($"Can you add comments to this {languageName} code block?");
        builder.AppendLine();
        builder.AppendLine(fenceHeader);
        builder.AppendLine(context.EffectiveCode);
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine($"Return only the complete updated code in one fenced `{language}` code block.");
        builder.AppendLine("Preserve the existing behavior and formatting.");
        builder.AppendLine("Do not include explanation before or after the code block.");
        builder.AppendLine("Do not use placeholders.");
        return builder.ToString();
    }

    public string BuildAddCommentsSystemPrompt(string? language)
    {
        string normalizedLanguage = language ?? "text";
        return string.Join(Environment.NewLine, [
            $"You add clear, concise comments to {normalizedLanguage} code.",
            "Preserve the existing behavior and formatting.",
            $"Return the complete updated snippet in exactly one fenced `{normalizedLanguage}` code block.",
            "Do not include any explanation before or after the code block.",
            "Do not use placeholders such as `...`.",
            "Keep the code valid and compilable."
        ]);
    }

    public string BuildSummarizeChangesPrompt(GitChangesContext changesContext)
    {
        string prompt = RenderRequiredTemplate(
            "summarize-changes.rdt.md",
            new Dictionary<string, string?>
            {
                ["selectedText"] = changesContext.DiffText,
                ["language"] = "diff",
                ["location"] = Path.GetFileName(changesContext.RepositoryRoot),
                ["statusText"] = string.IsNullOrWhiteSpace(changesContext.StatusText) ? "(clean status unavailable)" : changesContext.StatusText
            });

        return ReplaceFirst(prompt, "```", "```diff");
    }

    public string BuildChatSystemPrompt(string userPrompt, IReadOnlyList<OllamaChatTurn> turns, CodeContext? codeContext)
    {
        var builder = new StringBuilder();
        string languageName = LanguageHelper.GetLanguageName(codeContext?.Language);
        string fenceHeader = LanguageHelper.GetFenceHeader(codeContext?.Language);

        builder.AppendLine($"You are an expert {languageName} Visual Studio coding assistant powered by Ollama.");
        builder.AppendLine("Be concise, practical, and safe.");
        builder.AppendLine("If the user asks for code changes, return the complete updated code in one fenced code block.");
        builder.AppendLine("If the user asks for explanation, advice, or review text, respond in lightweight markdown with short sections and flat bullet lists.");
        builder.AppendLine("Put each heading and each bullet on its own line, leave a blank line between sections, and avoid dense paragraphs.");
        builder.AppendLine("Do not emit fenced code blocks unless the user explicitly asks for code or a code change.");
        builder.AppendLine();

        if (turns.Count > 0)
        {
            builder.AppendLine("Conversation so far:");
            builder.AppendLine();

            foreach (var turn in turns)
            {
                builder.AppendLine($"{turn.Role}:");
                builder.AppendLine(turn.Text);
                builder.AppendLine();
            }
        }

        if (codeContext is not null && !string.IsNullOrWhiteSpace(codeContext.EffectiveCode))
        {
            if (!string.IsNullOrWhiteSpace(codeContext.FilePath))
            {
                builder.AppendLine($"File: {codeContext.FilePath}");
            }

            if (!string.IsNullOrWhiteSpace(codeContext.Language))
            {
                builder.AppendLine($"Language: {codeContext.Language}");
            }

            builder.AppendLine();
            builder.AppendLine("Current code context:");
            builder.AppendLine(fenceHeader);
            builder.AppendLine(codeContext.EffectiveCode);
            builder.AppendLine("```");
            builder.AppendLine();
        }

        builder.AppendLine("Latest user request:");
        builder.AppendLine(userPrompt);

        return builder.ToString();
    }

    public static string PrepareDocumentForPrompt(string documentText, int contextWindowChars, bool reviewMode = false)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return string.Empty;
        }

        double usageFraction = reviewMode ? 0.45 : 0.65;
        int minimumChars = reviewMode ? 2200 : 3000;
        int maxChars = Math.Max(minimumChars, (int)(contextWindowChars * usageFraction));
        if (documentText.Length <= maxChars)
        {
            return documentText;
        }

        double headFraction = reviewMode ? 0.35 : 0.5;
        int headLength = Math.Max(800, (int)(maxChars * headFraction));
        int tailLength = Math.Max(800, maxChars - headLength);
        if (headLength + tailLength >= documentText.Length)
        {
            return documentText;
        }

        return string.Concat(
            documentText[..headLength],
            Environment.NewLine,
            Environment.NewLine,
            "// ... file content trimmed for prompt size ...",
            Environment.NewLine,
            Environment.NewLine,
            documentText[^tailLength..]);
    }

    private string RenderRequiredTemplate(string templateFileName, IDictionary<string, string?> variables) =>
        _templateService.RenderInitialPrompt(templateFileName, variables)
        ?? throw new InvalidOperationException($"Prompt template '{templateFileName}' is missing or invalid.");

    private static string ReplaceFirst(string text, string find, string replace)
    {
        int index = text.IndexOf(find, StringComparison.Ordinal);
        if (index < 0)
        {
            return text;
        }

        return text[..index] + replace + text[(index + find.Length)..];
    }
}
