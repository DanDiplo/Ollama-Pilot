using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
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

            var startingDirectory = await GetStartingDirectoryAsync(package);
            if (string.IsNullOrWhiteSpace(startingDirectory) || !Directory.Exists(startingDirectory))
            {
                return null;
            }

            var repoRoot = RunGit(startingDirectory, "rev-parse --show-toplevel");
            if (string.IsNullOrWhiteSpace(repoRoot))
            {
                return null;
            }

            var status = RunGit(repoRoot, "status --short");
            var diff = RunGit(repoRoot, "diff --cached --no-color");
            if (string.IsNullOrWhiteSpace(diff))
            {
                diff = RunGit(repoRoot, "diff --no-color");
            }

            if (string.IsNullOrWhiteSpace(diff))
            {
                return null;
            }

            return new GitChangesContext
            {
                RepositoryRoot = repoRoot.Trim(),
                StatusText = status?.Trim(),
                DiffText = TrimForPrompt(diff)
            };
        }

        private static async Task<string> GetStartingDirectoryAsync(AsyncPackage package)
        {
            var documentPath = await VsHelpers.GetActiveDocumentPathAsync(package);
            if (!string.IsNullOrWhiteSpace(documentPath))
            {
                return Path.GetDirectoryName(documentPath);
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;
            if (dte == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(dte.Solution?.FullName))
            {
                return Path.GetDirectoryName(dte.Solution.FullName);
            }

            return null;
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
    }
}
