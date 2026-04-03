using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace OllamaPilot.Infrastructure
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

        /// <summary>
        /// Formats an exception into a human-readable string, including details like type, message,
        /// source, target site, data, stack trace, and any inner exceptions.
        /// </summary>
        /// <param name="ex">The exception to format.</param>
        /// <returns>A formatted string representation of the exception.</returns>
        public static string FormatException(Exception ex)
        {
            // Initialize a StringBuilder to efficiently construct the exception string.
            var sb = new StringBuilder();

            // Append core exception details.
            sb.AppendLine($"Exception Type: {ex.GetType().FullName}"); // Full name of the exception type.
            sb.AppendLine($"Message: {ex.Message}");                   // The error message that explains the reason for the exception.
            sb.AppendLine($"Source: {ex.Source}");                     // The name of the application or the object that causes the error.
            sb.AppendLine($"TargetSite: {ex.TargetSite}");             // The method that throws the current exception.

            // Append any additional data associated with the exception.
            sb.AppendLine("Data:");
            foreach (var key in ex.Data.Keys)
            {
                sb.AppendLine($"{key}: {ex.Data[key]}");
            }

            // Append the stack trace, which shows the sequence of method calls that led to the exception.
            sb.AppendLine($"StackTrace: {ex.StackTrace}");

            // Recursively handle inner exceptions to provide a complete error chain.
            if (ex.InnerException != null)
            {
                sb.AppendLine("---- Inner Exception ----");
                // Recursively call FormatException for the inner exception and append its details.
                sb.AppendLine(FormatException(ex.InnerException));
            }

            // Return the complete formatted exception string.
            return sb.ToString();
        }

        public static void Initialize(IServiceProvider provider)
        {
            serviceProvider = provider;
        }

        [SuppressMessage("Usage", "VSSDK007:Await/join tasks created from ThreadHelper.JoinableTaskFactory.RunAsync", Justification = "Error reporting intentionally shows the UI warning asynchronously to avoid blocking the failing thread.")]
        public static void HandleException(Exception exception, string userMessage = null)
        {
            var message = $"An error occurred: {FormatException(exception)}\n";
            var shortMessage = userMessage ?? "Ollama Pilot hit an unexpected error. Check the log file in Documents for details.";

            if (serviceProvider != null)
            {
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
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
