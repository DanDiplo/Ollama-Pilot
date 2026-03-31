using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using OllamaPilot.Infrastructure;
using OllamaPilot.Services.Ollama;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OllamaPilot.Services.VisualStudio
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

            return await Task.Run(() => BuildChangesContext(startingDirectories));
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

        private static GitChangesContext BuildChangesContext(IReadOnlyList<string> startingDirectories)
        {
            var repoRoot = ResolveRepositoryRoot(startingDirectories);
            if (string.IsNullOrWhiteSpace(repoRoot))
            {
                LLMErrorHandler.WriteLog($"GitContextHelper: unable to resolve a git repository root from {string.Join(", ", startingDirectories)}.");
                return null;
            }

            var status = RunGit(repoRoot, "status --short");
            var stagedDiff = RunGit(repoRoot, "diff --cached --no-color");
            var unstagedDiff = RunGit(repoRoot, "diff --no-color");
            var untrackedFiles = RunGit(repoRoot, "ls-files --others --exclude-standard");

            var statusText = status.IsSuccess ? status.Output : null;
            var diff = BuildChangesSummary(
                statusText,
                stagedDiff.IsSuccess ? stagedDiff.Output : null,
                unstagedDiff.IsSuccess ? unstagedDiff.Output : null,
                untrackedFiles.IsSuccess ? untrackedFiles.Output : null);

            if (string.IsNullOrWhiteSpace(statusText) && string.IsNullOrWhiteSpace(diff))
            {
                LLMErrorHandler.WriteLog($"GitContextHelper: repository '{repoRoot}' appears clean.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(diff))
            {
                LLMErrorHandler.WriteLog($"GitContextHelper: repository '{repoRoot}' has status output but no diff body. Status: {statusText}");
                diff = "No textual diff was produced. Use the Git status section to summarize the working changes.";
            }

            return new GitChangesContext
            {
                RepositoryRoot = repoRoot.Trim(),
                StatusText = statusText?.Trim(),
                DiffText = TrimForPrompt(diff)
            };
        }

        private static string ResolveRepositoryRoot(IReadOnlyList<string> startingDirectories)
        {
            foreach (var startingDirectory in startingDirectories)
            {
                var result = RunGit(startingDirectory, "rev-parse --show-toplevel");
                if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Output))
                {
                    return result.Output.Trim();
                }
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

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        LLMErrorHandler.WriteLog($"GitContextHelper: failed to start git process for '{arguments}' in '{workingDirectory}'.");
                        return GitCommandResult.Failure("Unable to start git.");
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(5000);
                    if (process.ExitCode != 0)
                    {
                        LLMErrorHandler.WriteLog(
                            $"GitContextHelper: git {arguments} failed in '{workingDirectory}' with exit code {process.ExitCode}. {error?.Trim()}");
                        return GitCommandResult.Failure(error);
                    }

                    return GitCommandResult.Success(output);
                }
            }
            catch (Exception ex)
            {
                LLMErrorHandler.WriteLog($"GitContextHelper: exception running git {arguments} in '{workingDirectory}'. {ex.Message}");
                return GitCommandResult.Failure(ex.Message);
            }
        }

        private static string TrimForPrompt(string diffText)
        {
            if (string.IsNullOrWhiteSpace(diffText))
            {
                return diffText;
            }

            var chatCtx = Math.Max(1024, OllamaHelper.Instance != null ? OllamaHelper.Instance.Options.ChatCtxSize : 4096);
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

        private sealed class GitCommandResult
        {
            public bool IsSuccess { get; private set; }
            public string Output { get; private set; }

            public static GitCommandResult Success(string output)
            {
                return new GitCommandResult
                {
                    IsSuccess = true,
                    Output = output
                };
            }

            public static GitCommandResult Failure(string output)
            {
                return new GitCommandResult
                {
                    IsSuccess = false,
                    Output = output
                };
            }
        }
    }
}
