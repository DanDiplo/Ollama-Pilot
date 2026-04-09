using System.Text.RegularExpressions;

namespace OllamaPilot.Core.Services;

public static partial class ResponseTextFormatter
{
    public static string FormatForDisplay(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string formatted = text.Replace("\r\n", "\n", StringComparison.Ordinal);

        formatted = MarkdownHeadingRegex().Replace(formatted, "\n$1\n");
        formatted = MarkdownBulletRegex().Replace(formatted, "\n- ");
        formatted = MarkdownBoldRegex().Replace(formatted, "$1");
        formatted = SectionLabelRegex().Replace(formatted, "\n## $1\n");
        formatted = HeadingRegex().Replace(formatted, "\n$1");
        formatted = BoldLabelRegex().Replace(formatted, "\n$1");
        formatted = NumberedListRegex().Replace(formatted, "\n$1");
        formatted = BulletRegex().Replace(formatted, "\n$1");
        formatted = RunOnSectionRegex().Replace(formatted, "$1\n\n");
        formatted = SummaryRunOnRegex().Replace(formatted, "Summary\n\n");
        formatted = FindingsRunOnRegex().Replace(formatted, "Findings\n\n");
        formatted = TestsRunOnRegex().Replace(formatted, "Recommended Tests\n\n");
        formatted = MultiNewlineRegex().Replace(formatted, "\n\n");

        return formatted.Trim().Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\*\*([^*\r\n]+)\*\*")]
    private static partial Regex MarkdownBoldRegex();

    [GeneratedRegex(@"(?m)(?<!\n)(#{1,6}\s+[^\r\n#]+)")]
    private static partial Regex MarkdownHeadingRegex();

    [GeneratedRegex(@"(?<!\n)\*\s+")]
    private static partial Regex MarkdownBulletRegex();

    [GeneratedRegex(@"(?m)(^|\s)(#{1,6}\s)")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"(?m)(^|\s)(\*\*[^*\r\n]+:\*\*)")]
    private static partial Regex BoldLabelRegex();

    [GeneratedRegex(@"(?m)(^|\s)(\d+\.\s)")]
    private static partial Regex NumberedListRegex();

    [GeneratedRegex(@"(?m)(^|\s)([-*]\s)")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"(?im)\b(summary|findings|recommended tests|suggested improvements)\s*:?\s*(?=[A-Z0-9])")]
    private static partial Regex SectionLabelRegex();

    [GeneratedRegex(@"(?im)\b(Summary|Key Points|Key Functionality|Notable Details|Findings|Suggested Improvements|Recommended Tests|Notes|Flow)(?=[A-Z])")]
    private static partial Regex RunOnSectionRegex();

    [GeneratedRegex(@"(?m)^## Summary(?=[^\r\n])")]
    private static partial Regex SummaryRunOnRegex();

    [GeneratedRegex(@"(?m)^## Findings(?=[^\r\n])")]
    private static partial Regex FindingsRunOnRegex();

    [GeneratedRegex(@"(?m)^## Recommended Tests(?=[^\r\n])")]
    private static partial Regex TestsRunOnRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiNewlineRegex();
}
