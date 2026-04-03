using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace OllamaPilot.Infrastructure
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
                // Return null if the template file does not exist, preserving existing behavior.
                return null;
            }

            var markdownContent = File.ReadAllText(templatePath);
            var template = ExtractCodeFence(markdownContent, InitialMessageBlockName);

            if (string.IsNullOrWhiteSpace(template))
            {
                // Return null if the specific template block is not found or is empty, preserving existing behavior.
                return null;
            }

            // Apply variable replacements using string interpolation for placeholder construction.
            foreach (var variable in variables)
            {
                // The double curly braces `{{` and `}}` are used to escape literal curly braces
                // within an interpolated string, allowing us to form the `{{KEY}}` placeholder.
                template = template.Replace($"{{{{{variable.Key}}}}}", variable.Value ?? string.Empty);
            }

            // Perform the final replacement for escaped backticks.
            return template.Replace("\\`", "`");
        }

        public bool TemplateExists(string templateFileName) =>
                    File.Exists(Path.Combine(templatesDirectory, templateFileName));

        private static string ExtractCodeFence(string markdown, string blockName)
        {
            var pattern = "```" + Regex.Escape(blockName) + @"\r?\n(?<content>[\s\S]*?)\r?\n```";
            var match = Regex.Match(markdown, pattern);
            return match.Success ? match.Groups["content"].Value : null;
        }
    }
}
