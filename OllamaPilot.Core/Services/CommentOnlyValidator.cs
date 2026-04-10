namespace OllamaPilot.Core.Services;

public static class CommentOnlyValidator
{
    public static bool LooksLikeInvalidRewrite(string originalSelection, string generatedCode)
    {
        if (string.IsNullOrWhiteSpace(originalSelection) || string.IsNullOrWhiteSpace(generatedCode))
        {
            return true;
        }

        string[] originalLines = GetNonEmptyLines(originalSelection);
        string[] generatedLines = GetNonEmptyLines(generatedCode);
        string[] originalCodeLines = originalLines.Where(line => !IsCommentOnlyLine(line)).ToArray();
        string[] generatedCodeLines = generatedLines.Where(line => !IsCommentOnlyLine(line)).ToArray();

        if (originalLines.Length == 0 || generatedLines.Length == 0)
        {
            return true;
        }

        if (originalCodeLines.Length == 0 || generatedCodeLines.Length == 0)
        {
            return true;
        }

        if (!string.Equals(originalCodeLines[0], generatedCodeLines[0], StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(originalCodeLines[^1], generatedCodeLines[^1], StringComparison.Ordinal))
        {
            return true;
        }

        return !ContainsAllOriginalCodeLinesInOrder(originalLines, generatedLines);
    }

    private static string[] GetNonEmptyLines(string text) =>
        (text ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

    private static bool ContainsAllOriginalCodeLinesInOrder(string[] originalLines, string[] generatedLines)
    {
        int generatedIndex = 0;
        foreach (string originalLine in originalLines)
        {
            if (IsCommentOnlyLine(originalLine))
            {
                continue;
            }

            bool matched = false;
            while (generatedIndex < generatedLines.Length)
            {
                string generatedLine = generatedLines[generatedIndex++];
                if (IsCommentOnlyLine(generatedLine))
                {
                    continue;
                }

                if (string.Equals(originalLine, generatedLine, StringComparison.Ordinal))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCommentOnlyLine(string line)
    {
        string? trimmed = line?.TrimStart();
        return !string.IsNullOrWhiteSpace(trimmed)
            && (trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("/*", StringComparison.Ordinal)
                || trimmed.StartsWith("*", StringComparison.Ordinal)
                || trimmed.StartsWith("///", StringComparison.Ordinal)
                || trimmed.StartsWith("'''", StringComparison.Ordinal));
    }
}
