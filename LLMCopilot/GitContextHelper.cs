using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OllamaPilot
{
    internal sealed class GitChangesContext
    {
        public string RepositoryRoot { get; set; }
        public string StatusText { get; set; }
        public string DiffText { get; set; }
    }

    internal static class GitContextHelper
    {
        public static async Task<GitChangesContext> TryGetChangesContextAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var startingDirectories = await GetStartingDirectoriesAsync(package);
            if (startingDirectories.Count == 0)
            {
                LLMErrorHandler.WriteLog("GitContextHelper: no starting directory was available from the active document or solution.");
                return null;
            }

            string repoRoot = null;
            foreach (var startingDirectory in startingDirectories)
            {
                repoRoot = RunGit(startingDirectory, "rev-parse --show-toplevel")?.Trim();
                if (!string.IsNullOrWhiteSpace(repoRoot))
                {
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(repoRoot))
            {
                LLMErrorHandler.WriteLog($"GitContextHelper: unable to resolve a git repository root from {string.Join(", ", startingDirectories)}.");
                return null;
            }

            var status = RunGit(repoRoot, "status --short");
            var stagedDiff = RunGit(repoRoot, "diff --cached --no-color");
            var unstagedDiff = RunGit(repoRoot, "diff --no-color");
            var untrackedFiles = RunGit(repoRoot, "ls-files --others --exclude-standard");
            var diff = BuildChangesSummary(status, stagedDiff, unstagedDiff, untrackedFiles);

            if (string.IsNullOrWhiteSpace(status) && string.IsNullOrWhiteSpace(diff))
            {
                LLMErrorHandler.WriteLog($"GitContextHelper: repository '{repoRoot}' appears clean.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(diff))
            {
                LLMErrorHandler.WriteLog($"GitContextHelper: repository '{repoRoot}' has status output but no diff body. Status: {status}");
                diff = "No textual diff was produced. Use the Git status section to summarize the working changes.";
            }

            return new GitChangesContext
            {
                RepositoryRoot = repoRoot.Trim(),
                StatusText = status?.Trim(),
                DiffText = TrimForPrompt(diff)
            };
        }

        private static async Task<IReadOnlyList<string>> GetStartingDirectoriesAsync(AsyncPackage package)
        {
            var directories = new List<string>();
            var documentPath = await VsHelpers.GetActiveDocumentPathAsync(package);
            if (!string.IsNullOrWhiteSpace(documentPath))
            {
                directories.Add(Path.GetDirectoryName(documentPath));
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;
            if (dte == null)
            {
                return directories
                    .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (!string.IsNullOrWhiteSpace(dte.Solution?.FullName))
            {
                directories.Add(Path.GetDirectoryName(dte.Solution.FullName));
            }

            return directories
                .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string RunGit(string workingDirectory, string arguments)
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

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return null;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(5000);
                    return process.ExitCode == 0 ? output : error;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string TrimForPrompt(string diffText)
        {
            if (string.IsNullOrWhiteSpace(diffText))
            {
                return diffText;
            }

            var chatCtx = Math.Max(1024, OllamaHelper.Instance.Options.ChatCtxSize);
            var maxChars = Math.Max(4000, (int)(OllamaHelper.EstimateCharsByTokens(chatCtx) * 0.6));
            if (diffText.Length <= maxChars)
            {
                return diffText;
            }

            var lines = diffText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var builder = new StringBuilder();
            var currentLength = 0;

            foreach (var line in lines.Take(200))
            {
                builder.AppendLine(line);
                currentLength += line.Length + Environment.NewLine.Length;
                if (currentLength >= maxChars / 2)
                {
                    break;
                }
            }

            builder.AppendLine();
            builder.AppendLine("... diff trimmed for prompt size ...");
            builder.AppendLine();

            currentLength = builder.Length;
            foreach (var line in lines.Skip(Math.Max(0, lines.Length - 120)))
            {
                if (currentLength + line.Length + Environment.NewLine.Length > maxChars)
                {
                    break;
                }

                builder.AppendLine(line);
                currentLength += line.Length + Environment.NewLine.Length;
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildChangesSummary(string status, string stagedDiff, string unstagedDiff, string untrackedFiles)
        {
            var builder = new StringBuilder();

            AppendSection(builder, "Staged changes", stagedDiff);
            AppendSection(builder, "Unstaged changes", unstagedDiff);

            var untrackedList = (untrackedFiles ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (untrackedList.Length > 0)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine("Untracked files");
                builder.AppendLine("================");
                foreach (var file in untrackedList)
                {
                    builder.AppendLine(file);
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

        private static void AppendSection(StringBuilder builder, string title, string content)
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
    }
}
