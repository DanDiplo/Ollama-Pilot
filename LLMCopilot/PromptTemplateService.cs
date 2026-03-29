using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace OllamaPilot
{
    internal sealed class PromptTemplateService
    {
        private const string InitialMessageBlockName = "template-initial-message";
        private readonly string templatesDirectory;

        public PromptTemplateService(string templatesDirectory)
        {
            this.templatesDirectory = templatesDirectory;
        }

        public string RenderInitialPrompt(string templateFileName, IDictionary<string, string> variables)
        {
            var templatePath = Path.Combine(templatesDirectory, templateFileName);
            if (!File.Exists(templatePath))
            {
                return null;
            }

            var markdown = File.ReadAllText(templatePath);
            var template = ExtractCodeFence(markdown, InitialMessageBlockName);
            if (string.IsNullOrWhiteSpace(template))
            {
                return null;
            }

            foreach (var variable in variables)
            {
                template = template.Replace("{{" + variable.Key + "}}", variable.Value ?? string.Empty);
            }

            return template.Replace("\\`", "`");
        }

        public bool TemplateExists(string templateFileName)
        {
            var templatePath = Path.Combine(templatesDirectory, templateFileName);
            return File.Exists(templatePath);
        }

        private static string ExtractCodeFence(string markdown, string blockName)
        {
            var pattern = "```" + Regex.Escape(blockName) + @"\r?\n(?<content>[\s\S]*?)\r?\n```";
            var match = Regex.Match(markdown, pattern);
            return match.Success ? match.Groups["content"].Value : null;
        }
    }
}
