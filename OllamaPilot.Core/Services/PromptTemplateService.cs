using System.Reflection;
using System.Text.RegularExpressions;

namespace OllamaPilot.Core.Services;

public sealed class PromptTemplateService
{
    private const string InitialMessageBlockName = "template-initial-message";
    private readonly Assembly _assembly;

    public PromptTemplateService(Assembly? assembly = null)
    {
        _assembly = assembly ?? typeof(PromptTemplateService).Assembly;
    }

    public string? RenderInitialPrompt(string templateFileName, IDictionary<string, string?> variables)
    {
        string? markdownContent = ReadTemplate(templateFileName);
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            return null;
        }

        string? template = ExtractCodeFence(markdownContent, InitialMessageBlockName);
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        foreach (var variable in variables)
        {
            template = template.Replace($"{{{{{variable.Key}}}}}", variable.Value ?? string.Empty, StringComparison.Ordinal);
        }

        return template.Replace("\\`", "`", StringComparison.Ordinal);
    }

    private string? ReadTemplate(string templateFileName)
    {
        string? actualName = _assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(templateFileName, StringComparison.OrdinalIgnoreCase));

        if (actualName is null)
        {
            return null;
        }

        using Stream? stream = _assembly.GetManifestResourceStream(actualName);
        if (stream is null)
        {
            return null;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static string? ExtractCodeFence(string markdown, string blockName)
    {
        string pattern = "```" + Regex.Escape(blockName) + @"\r?\n(?<content>[\s\S]*?)\r?\n```";
        Match match = Regex.Match(markdown, pattern);
        return match.Success ? match.Groups["content"].Value : null;
    }
}
