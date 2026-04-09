using System.Diagnostics;
using System.Text;
using OllamaPilot.Core.Models;

namespace OllamaPilot.Core.Services;

public static class GitService
{
    public static async Task<GitChangesContext?> TryGetChangesContextAsync(string? startingFilePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(startingFilePath))
        {
            return null;
        }

        string? startingDirectory = Path.GetDirectoryName(startingFilePath);
        if (string.IsNullOrWhiteSpace(startingDirectory) || !Directory.Exists(startingDirectory))
        {
            return null;
        }

        return await Task.Run(() => BuildChangesContext(startingDirectory), cancellationToken);
    }

    private static GitChangesContext? BuildChangesContext(string startingDirectory)
    {
        string? repoRoot = ResolveRepositoryRoot(startingDirectory);
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            return null;
        }

        GitCommandResult status = RunGit(repoRoot, "status --short");
        GitCommandResult stagedDiff = RunGit(repoRoot, "diff --cached --no-color");
        GitCommandResult unstagedDiff = RunGit(repoRoot, "diff --no-color");
        GitCommandResult untrackedFiles = RunGit(repoRoot, "ls-files --others --exclude-standard");

        string statusText = status.IsSuccess ? (status.Output ?? string.Empty).Trim() : string.Empty;
        string diff = BuildChangesSummary(
            statusText,
            stagedDiff.IsSuccess ? stagedDiff.Output : null,
            unstagedDiff.IsSuccess ? unstagedDiff.Output : null,
            untrackedFiles.IsSuccess ? untrackedFiles.Output : null);

        if (string.IsNullOrWhiteSpace(statusText) && string.IsNullOrWhiteSpace(diff))
        {
            return null;
        }

        return new GitChangesContext
        {
            RepositoryRoot = repoRoot,
            StatusText = statusText,
            DiffText = TrimForPrompt(diff)
        };
    }

    private static string? ResolveRepositoryRoot(string startingDirectory)
    {
        string? current = startingDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            GitCommandResult result = RunGit(current, "rev-parse --show-toplevel");
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Output))
            {
                return result.Output.Trim();
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }

    private static GitCommandResult RunGit(string workingDirectory, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return GitCommandResult.Failure("Unable to start git.");
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            return process.ExitCode == 0
                ? GitCommandResult.Success(output)
                : GitCommandResult.Failure(error);
        }
        catch (Exception ex)
        {
            return GitCommandResult.Failure(ex.Message);
        }
    }

    private static string TrimForPrompt(string diffText)
    {
        if (string.IsNullOrWhiteSpace(diffText))
        {
            return diffText;
        }

        const int maxChars = 12000;
        if (diffText.Length <= maxChars)
        {
            return diffText;
        }

        string[] lines = diffText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var builder = new StringBuilder();

        foreach (string line in lines.Take(200))
        {
            builder.AppendLine(line);
            if (builder.Length >= maxChars / 2)
            {
                break;
            }
        }

        builder.AppendLine();
        builder.AppendLine("... diff trimmed for prompt size ...");
        builder.AppendLine();

        foreach (string line in lines.Skip(Math.Max(0, lines.Length - 120)))
        {
            if (builder.Length + line.Length + Environment.NewLine.Length > maxChars)
            {
                break;
            }

            builder.AppendLine(line);
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildChangesSummary(string status, string? stagedDiff, string? unstagedDiff, string? untrackedFiles)
    {
        var builder = new StringBuilder();

        AppendSection(builder, "Staged changes", stagedDiff);
        AppendSection(builder, "Unstaged changes", unstagedDiff);

        string[] untrackedList = (untrackedFiles ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (untrackedList.Length > 0)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("Untracked files");
            builder.AppendLine("================");

            foreach (string file in untrackedList)
            {
                builder.AppendLine(file.Trim());
            }
        }

        if (builder.Length == 0 && !string.IsNullOrWhiteSpace(status))
        {
            builder.AppendLine("Working tree status");
            builder.AppendLine("===================");
            builder.AppendLine(status.Trim());
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendSection(StringBuilder builder, string title, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine(title);
        builder.AppendLine(new string('=', title.Length));
        builder.AppendLine(content.Trim());
    }

    private readonly record struct GitCommandResult(bool IsSuccess, string? Output)
    {
        public static GitCommandResult Success(string? output) => new(true, output);
        public static GitCommandResult Failure(string? output) => new(false, output);
    }
}
