namespace OllamaPilot.Core.Helpers;

public static class LanguageHelper
{
    public static string GetLanguageName(string? language) =>
        string.IsNullOrWhiteSpace(language) ? "code" : language;

    public static string GetFenceHeader(string? language) =>
        string.IsNullOrWhiteSpace(language) ? "```" : $"```{NormalizeFenceLanguage(language)}";

    public static string? InferLanguageFromFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        string fileName = Path.GetFileName(filePath);
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase))
        {
            return "dockerfile";
        }

        if (string.Equals(fileName, ".editorconfig", StringComparison.OrdinalIgnoreCase))
        {
            return "editorconfig";
        }

        return extension switch
        {
            ".cs" => "csharp",
            ".vb" => "vb",
            ".fs" or ".fsx" or ".fsi" => "fsharp",
            ".js" => "javascript",
            ".jsx" => "jsx",
            ".ts" => "typescript",
            ".tsx" => "tsx",
            ".json" => "json",
            ".xml" or ".csproj" or ".vbproj" or ".fsproj" or ".props" or ".targets" or ".resx" => "xml",
            ".xaml" => "xml",
            ".razor" or ".cshtml" or ".vbhtml" => "razor",
            ".html" => "html",
            ".css" => "css",
            ".scss" => "scss",
            ".less" => "less",
            ".sql" => "sql",
            ".py" => "python",
            ".go" => "go",
            ".java" => "java",
            ".cpp" or ".cc" or ".c" or ".h" or ".hpp" => "cpp",
            ".md" => "markdown",
            ".yaml" or ".yml" => "yaml",
            ".sh" => "bash",
            ".ps1" => "powershell",
            ".patch" or ".diff" => "diff",
            _ => null
        };
    }

    private static string NormalizeFenceLanguage(string language) =>
        language.Trim().ToLowerInvariant() switch
        {
            "c#" => "csharp",
            "c/c++" => "cpp",
            _ => language.Trim().ToLowerInvariant()
        };
}
