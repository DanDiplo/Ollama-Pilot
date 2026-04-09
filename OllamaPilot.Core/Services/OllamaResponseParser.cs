using System.Text.RegularExpressions;
using OllamaPilot.Core.Models;

namespace OllamaPilot.Core.Services;

public sealed partial class OllamaResponseParser
{
    private static readonly Regex CodeBlockRegex = CreateCodeBlockRegex();
    private static readonly Regex UnclosedCodeBlockRegex = CreateUnclosedCodeBlockRegex();
    private static readonly Regex CompactCodeBlockRegex = CreateCompactCodeBlockRegex();
    private static readonly Regex UnclosedCompactCodeBlockRegex = CreateUnclosedCompactCodeBlockRegex();

    public OllamaParsedResponse Parse(
        string text,
        string actionName,
        string? sourceFilePath,
        object? applyTarget = null,
        ResponseParsingMode parsingMode = ResponseParsingMode.PreferCode)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new OllamaParsedResponse
            {
                ActionName = actionName,
                SourceFilePath = sourceFilePath,
                ApplyTarget = applyTarget
            };
        }

        if (parsingMode != ResponseParsingMode.TextOnly)
        {
            CodeCandidate? bestCandidate = FindBestClosedCodeCandidate(text, parsingMode);
            if (bestCandidate is not null)
            {
                return new OllamaParsedResponse
                {
                    FullText = text,
                    Explanation = text.Remove(bestCandidate.Match.Index, bestCandidate.Match.Length).Trim(),
                    CodeBlock = PostProcessCode(bestCandidate.Code, bestCandidate.Language).Code,
                    IsApplyReady = PostProcessCode(bestCandidate.Code, bestCandidate.Language).IsApplyReady,
                    Language = bestCandidate.Language,
                    ActionName = actionName,
                    SourceFilePath = sourceFilePath,
                    ApplyTarget = applyTarget
                };
            }

            if (parsingMode == ResponseParsingMode.CodeRequired)
            {
                Match unclosedCompactMatch = UnclosedCompactCodeBlockRegex.Match(text);
                if (unclosedCompactMatch.Success)
                {
                    return new OllamaParsedResponse
                    {
                        FullText = text,
                        Explanation = text[..unclosedCompactMatch.Index].Trim(),
                        CodeBlock = PostProcessCode(NormalizeCode(unclosedCompactMatch.Groups["code"].Value), unclosedCompactMatch.Groups["lang"].Value.Trim()).Code,
                        IsApplyReady = PostProcessCode(NormalizeCode(unclosedCompactMatch.Groups["code"].Value), unclosedCompactMatch.Groups["lang"].Value.Trim()).IsApplyReady,
                        Language = unclosedCompactMatch.Groups["lang"].Value.Trim(),
                        ActionName = actionName,
                        SourceFilePath = sourceFilePath,
                        ApplyTarget = applyTarget
                    };
                }

                Match unclosedMatch = UnclosedCodeBlockRegex.Match(text);
                if (unclosedMatch.Success)
                {
                    return new OllamaParsedResponse
                    {
                        FullText = text,
                        Explanation = text[..unclosedMatch.Index].Trim(),
                        CodeBlock = PostProcessCode(NormalizeCode(unclosedMatch.Groups["code"].Value), unclosedMatch.Groups["lang"].Value.Trim()).Code,
                        IsApplyReady = PostProcessCode(NormalizeCode(unclosedMatch.Groups["code"].Value), unclosedMatch.Groups["lang"].Value.Trim()).IsApplyReady,
                        Language = unclosedMatch.Groups["lang"].Value.Trim(),
                        ActionName = actionName,
                        SourceFilePath = sourceFilePath,
                        ApplyTarget = applyTarget
                    };
                }
            }
        }

        return new OllamaParsedResponse
        {
            FullText = text,
            Explanation = text,
            ActionName = actionName,
            SourceFilePath = sourceFilePath,
            ApplyTarget = applyTarget
        };
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        if (code.StartsWith("\r\n", StringComparison.Ordinal))
        {
            code = code[2..];
        }
        else if (code.StartsWith('\n'))
        {
            code = code[1..];
        }

        return code.TrimEnd();
    }

    private static string RemoveFirstMatch(string text, Match match) => text.Remove(match.Index, match.Length);

    private static CodeProcessingResult PostProcessCode(string code, string? language)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new CodeProcessingResult(string.Empty, false);
        }

        if (!string.Equals(language, "csharp", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(language, "c#", StringComparison.OrdinalIgnoreCase))
        {
            return new CodeProcessingResult(code, true);
        }

        if (code.Contains('\n') || code.Contains('\r'))
        {
            return new CodeProcessingResult(code, true);
        }

        return new CodeProcessingResult(FormatCompactCSharpForDisplay(code), false);
    }

    private static string FormatCompactCSharpForDisplay(string code)
    {
        string formatted = code;
        formatted = XmlDocStartRegex().Replace(formatted, "\n$0");
        formatted = XmlDocBoundaryRegex().Replace(formatted, "$1\n");
        formatted = MemberStartRegex().Replace(formatted, "\n$1");
        formatted = MethodOpenBraceRegex().Replace(formatted, "$1\n{");
        formatted = LineCommentStartRegex().Replace(formatted, "\n//");
        formatted = CommentToStatementRegex().Replace(formatted, "$1\n$2");
        formatted = StatementBoundaryRegex().Replace(formatted, "$1\n");
        formatted = CloseBraceBoundaryRegex().Replace(formatted, "\n}");
        formatted = MultiBlankLineRegex().Replace(formatted, "\n\n");
        return IndentBraceCode(formatted.Trim());
    }

    private static string IndentBraceCode(string code)
    {
        string[] lines = code
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        int indentLevel = 0;
        var builder = new System.Text.StringBuilder();

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.StartsWith("}", StringComparison.Ordinal))
            {
                indentLevel = Math.Max(0, indentLevel - 1);
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(' ', indentLevel * 4);
            builder.Append(line);

            if (line.EndsWith("{", StringComparison.Ordinal))
            {
                indentLevel++;
            }
        }

        return builder.ToString();
    }

    private static CodeCandidate? FindBestClosedCodeCandidate(string text, ResponseParsingMode parsingMode)
    {
        CodeCandidate? best = null;
        foreach (Match match in EnumerateClosedMatches(text, parsingMode))
        {
            string code = NormalizeCode(match.Groups["code"].Value);
            string language = match.Groups["lang"].Value.Trim();
            int score = ScoreCandidate(code, language);
            if (!IsAcceptable(score, parsingMode))
            {
                continue;
            }

            if (best is null || score > best.Score)
            {
                best = new CodeCandidate(match, code, language, score);
            }
        }

        return best;
    }

    private static IEnumerable<Match> EnumerateClosedMatches(string text, ResponseParsingMode parsingMode)
    {
        foreach (Match match in CodeBlockRegex.Matches(text))
        {
            yield return match;
        }

        if (parsingMode == ResponseParsingMode.CodeRequired)
        {
            foreach (Match match in CompactCodeBlockRegex.Matches(text))
            {
                yield return match;
            }
        }
    }

    private static bool IsAcceptable(int score, ResponseParsingMode parsingMode) =>
        parsingMode switch
        {
            ResponseParsingMode.CodeRequired => score >= 1,
            ResponseParsingMode.PreferCode => score >= 8,
            _ => false
        };

    private static int ScoreCandidate(string code, string language)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return 0;
        }

        int score = Math.Min(code.Length, 400);
        int lineCount = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length;
        score += Math.Min(lineCount * 20, 200);

        if (!string.IsNullOrWhiteSpace(language))
        {
            score += 50;
        }

        if (string.Equals(language, "block", StringComparison.OrdinalIgnoreCase))
        {
            score -= 200;
        }

        if (code.Contains("class ", StringComparison.Ordinal) ||
            code.Contains("public ", StringComparison.Ordinal) ||
            code.Contains("private ", StringComparison.Ordinal) ||
            code.Contains("internal ", StringComparison.Ordinal) ||
            code.Contains("return ", StringComparison.Ordinal) ||
            code.Contains("{", StringComparison.Ordinal))
        {
            score += 100;
        }

        if (code.Length < 24)
        {
            score -= 100;
        }

        return score;
    }

    [GeneratedRegex(@"(?ms)^[ \t]*```(?<lang>[a-zA-Z0-9_+\-]*)?[ \t]*\r?\n(?<code>.*?)(?:\r?\n)^[ \t]*```[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex CreateCodeBlockRegex();

    [GeneratedRegex(@"(?ms)^[ \t]*```(?<lang>[a-zA-Z0-9_+\-]*)?[ \t]*\r?\n(?<code>.*)$", RegexOptions.Multiline)]
    private static partial Regex CreateUnclosedCodeBlockRegex();

    [GeneratedRegex(@"(?s)```(?<lang>[a-zA-Z0-9_+\-]+)(?<code>.+?)```")]
    private static partial Regex CreateCompactCodeBlockRegex();

    [GeneratedRegex(@"(?s)```(?<lang>[a-zA-Z0-9_+\-]+)(?<code>.+)$")]
    private static partial Regex CreateUnclosedCompactCodeBlockRegex();

    [GeneratedRegex(@"(?=///\s*<)")]
    private static partial Regex XmlDocStartRegex();

    [GeneratedRegex(@"(</summary>|</remarks>|</value>|</returns>|</param>)\s*(?=(///|public|private|protected|internal|static|sealed|abstract|partial|class|struct|interface|enum))")]
    private static partial Regex XmlDocBoundaryRegex();

    [GeneratedRegex(@"(?<!^)(public\s+|private\s+|protected\s+|internal\s+)")]
    private static partial Regex MemberStartRegex();

    [GeneratedRegex(@"(\))\{")]
    private static partial Regex MethodOpenBraceRegex();

    [GeneratedRegex(@"(?<!\r)(?<!\n)(//(?!/))")]
    private static partial Regex LineCommentStartRegex();

    [GeneratedRegex(@"(//[^\r\n]*?)\s+(string\s+\w+\s*=|var\s+\w+\s*=|int\s+\w+\s*=|bool\s+\w+\s*=|long\s+\w+\s*=|double\s+\w+\s*=|decimal\s+\w+\s*=|float\s+\w+\s*=|char\s+\w+\s*=|byte\s+\w+\s*=|short\s+\w+\s*=|DateTime\s+\w+\s*=|File\.|Path\.|Environment\.|\w+\s*\+=|return\b|if\s*\(|for\s*\(|foreach\s*\(|while\s*\(|try\b|catch\b|using\b|await\b)")]
    private static partial Regex CommentToStatementRegex();

    [GeneratedRegex(@"(;)\s*(?=(string\s+\w+\s*=|var\s+\w+\s*=|int\s+\w+\s*=|bool\s+\w+\s*=|long\s+\w+\s*=|double\s+\w+\s*=|decimal\s+\w+\s*=|float\s+\w+\s*=|char\s+\w+\s*=|byte\s+\w+\s*=|short\s+\w+\s*=|DateTime\s+\w+\s*=|File\.|Path\.|Environment\.|\w+\s*\+=|return\b|if\s*\(|for\s*\(|foreach\s*\(|while\s*\(|try\b|catch\b|using\b|await\b|//))")]
    private static partial Regex StatementBoundaryRegex();

    [GeneratedRegex(@"\}(?=\S)")]
    private static partial Regex CloseBraceBoundaryRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiBlankLineRegex();

    private sealed record CodeProcessingResult(string Code, bool IsApplyReady);
    private sealed record CodeCandidate(Match Match, string Code, string Language, int Score);
}
