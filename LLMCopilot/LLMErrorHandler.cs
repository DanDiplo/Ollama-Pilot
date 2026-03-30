using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace OllamaPilot
{
    public static class LogHelper
    {
        public static void Log([CallerMemberName] string memberName = "")
        {
            LLMErrorHandler.WriteLog(memberName);
        }
    }

    public static class LLMErrorHandler
    {
        private static IServiceProvider serviceProvider;

        public static string FormatException(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"Source: {ex.Source}");
            sb.AppendLine($"TargetSite: {ex.TargetSite}");
            sb.AppendLine("Data:");
            foreach (var key in ex.Data.Keys)
            {
                sb.AppendLine($"{key}: {ex.Data[key]}");
            }
            sb.AppendLine($"StackTrace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                sb.AppendLine("---- Inner Exception ----");
                sb.AppendLine(FormatException(ex.InnerException)); // Recursively append the inner exception
            }

            return sb.ToString();
        }

        public static void Initialize(IServiceProvider provider)
        {
            serviceProvider = provider;
        }

        public static void HandleException(Exception exception, string userMessage = null)
        {
            var message = $"An error occurred: {FormatException(exception)}\n";
            var shortMessage = userMessage ?? "Ollama Pilot hit an unexpected error. Check the log file in Documents for details.";

            if (serviceProvider != null)
            {
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    VsShellUtilities.ShowMessageBox(
                        serviceProvider,
                        shortMessage,
                        "Ollama Pilot",
                        OLEMSGICON.OLEMSGICON_WARNING,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                });
            }

            LLMErrorHandler.WriteLog(message);
        }

        public static void WriteLog(string log)
        {
            string logFileName = $"OllamaPilot_{DateTime.Now:yyyy-MM-dd}.log";
            string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), logFileName);
            log += $"---------{DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n";
            File.AppendAllText(logFilePath, log);
        }
    }
}
