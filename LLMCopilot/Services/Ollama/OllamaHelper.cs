using OllamaPilot.Infrastructure;
using OllamaPilot.Package;
using OllamaPilot.Services.VisualStudio;
using OllamaPilot.UI.Chat;
using OllamaPilot.UI.Settings;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace OllamaPilot.Services.Ollama
{
    public sealed class OllamaHelper
    {
        public string CodeCompleteTemplate = $"<|fim▁begin|>{0}<|fim▁hole|>{1}<|fim▁end|>";

        private static readonly Lazy<OllamaHelper> lazy = new Lazy<OllamaHelper>(() => new OllamaHelper());
        private static readonly IOllamaService ollamaService = new OllamaSharpService();

        public static OllamaHelper Instance { get { return lazy.Value; } }

        private static readonly int defaultContext = 4096;
        private static readonly int defaultCodeLineLength = 80;
        private static readonly double PrefixCodeLinePercent = 0.8;
        private static readonly double SuffixCodeLinePercent = 1 - PrefixCodeLinePercent;
        private static readonly double defaultContextUsage = 0.8;

        public RequestOptions CompRequestOptions { get; private set; }
        public RequestOptions ChatRequestOptions { get; private set; }

        private readonly string[] stop;

        public OptionPageGrid Options { get; private set; }
        private PromptTemplateService TemplateService { get; set; }


        private OllamaHelper()
        {
            var package = LLMCopilotProvider.Package;
            Options = (OptionPageGrid)package.GetDialogPage(typeof(OptionPageGrid));
            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            TemplateService = new PromptTemplateService(Path.Combine(assemblyDirectory ?? string.Empty, "Templates"));

            stop = new string[]{
                Options.FimBegin,
                Options.FimHole,
                Options.FimEnd,
                "//",
                "<｜end▁of▁sentence｜>",
                "\n\n",
                "\r\n\r\n",
                "/src/","#- coding: utf-8",
                "```",
                "\nclass",
                "\nnamespace",
                "\nvoid"
                };

            CompRequestOptions = new RequestOptions
            {
                NumCtx = Options.CompleteCtxSize,
                NumPredict = 128,
                Stop = stop,
                Temperature = 0.3f
            };

            ChatRequestOptions = new RequestOptions
            {
                NumCtx = Options.ChatCtxSize,
                NumPredict = Options.ChatMaxOutputTokens,
                Temperature = 0.7f
            };

            Options.SettingsChanged += Options_SettingsChanged;
        }

        ~OllamaHelper()
        {
            Options.SettingsChanged -= Options_SettingsChanged;
        }

        public string GetExplainCodeTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            return RenderPromptOrThrow("explain-code.rdt.md", code, code_type, file);
        }

        public string GetFindBugTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            return RenderPromptOrThrow("find-bugs.rdt.md", code, code_type, file);
        }

        public string GetOptimizeCodeTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            return RenderPromptOrThrow("optimize-code.rdt.md", code, code_type, file);
        }

        public string GetUnitTestTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            return RenderPromptOrThrow("generate-unit-test.rdt.md", code, code_type, file);
        }

        public string GetReviewFileTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            return RenderPromptOrThrow("review-file.rdt.md", code, code_type, file);
        }

        public string GetGenerateFileTestsTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            return RenderPromptOrThrow("generate-file-tests.rdt.md", code, code_type, file);
        }

        public string GetSummarizeChangesTemplate(string repositoryRoot, string gitStatus, string diffText)
        {
            var prompt = RenderInitialPromptOrThrow("summarize-changes.rdt.md", new Dictionary<string, string>
            {
                { "selectedText", diffText },
                { "language", "diff" },
                { "location", Path.GetFileName(repositoryRoot ?? string.Empty) },
                { "statusText", string.IsNullOrWhiteSpace(gitStatus) ? "(clean status unavailable)" : gitStatus }
            });
            return ReplaceFirst(prompt, "```", "```diff");
        }

        public string GetAddCommentTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            return RenderPromptOrThrow("document-code.rdt.md", code, code_type, file);
        }

        public string GetDiagnoseErrorsTemplate(string code, string file, string diagnosticText = null)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            return RenderInitialPromptOrThrow("diagnose-errors.rdt.md", new Dictionary<string, string>
            {
                { "selectedTextWithDiagnostics", BuildDiagnosticInput(code, code_type, diagnosticText) },
                { "location", Path.GetFileName(file ?? string.Empty) }
            });
        }

        public string GetFixErrorTemplate(string code, string file, string diagnosticText, int? lineNumber = null, int? columnNumber = null)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            var prompt = RenderInitialPromptOrThrow("fix-error.rdt.md", new Dictionary<string, string>
            {
                { "selectedText", code },
                { "language", code_type },
                { "location", Path.GetFileName(file ?? string.Empty) },
                { "diagnosticText", BuildDiagnosticSummary(diagnosticText, lineNumber, columnNumber) }
            });
            return ReplaceFirst(prompt, "```", $"```{code_type}");
        }

        public string GetFixDiagnosticTemplate(string diagnosticText, string location)
        {
            return RenderInitialPromptOrThrow("fix-diagnostic.rdt.md", new Dictionary<string, string>
            {
                { "diagnosticText", string.IsNullOrWhiteSpace(diagnosticText) ? "No diagnostic text was provided." : diagnosticText.Trim() },
                { "location", string.IsNullOrWhiteSpace(location) ? "the current project" : location.Trim() }
            });
        }

        public string GetEditSelectionTemplate(string code, string file, string instructions)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            var prompt = RenderInitialPromptOrThrow("edit-selection.rdt.md", new Dictionary<string, string>
            {
                { "selectedText", code },
                { "language", code_type },
                { "location", Path.GetFileName(file ?? string.Empty) },
                { "instructions", instructions }
            });
            return ReplaceFirst(prompt, "```", $"```{code_type}");
        }

        private string RenderPrompt(string templateFileName, string code, string language, string file)
        {
            var prompt = TemplateService.RenderInitialPrompt(templateFileName, new Dictionary<string, string>
            {
                { "selectedText", code },
                { "language", language },
                { "location", Path.GetFileName(file ?? string.Empty) }
            });

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return null;
            }

            return ReplaceFirst(prompt, "```", $"```{language}");
        }

        private string RenderPromptOrThrow(string templateFileName, string code, string language, string file)
        {
            var prompt = RenderPrompt(templateFileName, code, language, file);
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                return prompt;
            }

            throw new InvalidOperationException($"Prompt template '{templateFileName}' is missing or invalid.");
        }

        private string RenderInitialPromptOrThrow(string templateFileName, IDictionary<string, string> variables)
        {
            var prompt = TemplateService.RenderInitialPrompt(templateFileName, variables);
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                return prompt;
            }

            throw new InvalidOperationException($"Prompt template '{templateFileName}' is missing or invalid.");
        }

        private static string ReplaceFirst(string text, string find, string replace)
        {
            var index = text.IndexOf(find, StringComparison.Ordinal);
            if (index < 0)
            {
                return text;
            }

            return text.Substring(0, index) + replace + text.Substring(index + find.Length);
        }

        private static string BuildDiagnosticInput(string code, string language, string diagnosticText)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(diagnosticText))
            {
                builder.AppendLine("Diagnostic details:");
                builder.AppendLine(diagnosticText.Trim());
                builder.AppendLine();
            }

            builder.AppendLine($"Code ({language}):");
            builder.AppendLine(code);
            return builder.ToString().TrimEnd();
        }

        private static string BuildDiagnosticSummary(string diagnosticText, int? lineNumber, int? columnNumber)
        {
            var builder = new StringBuilder();
            if (lineNumber.HasValue)
            {
                builder.Append("Line ");
                builder.Append(lineNumber.Value);
                if (columnNumber.HasValue)
                {
                    builder.Append(", Column ");
                    builder.Append(columnNumber.Value);
                }
                builder.AppendLine();
            }

            builder.Append(string.IsNullOrWhiteSpace(diagnosticText)
                ? "No explicit diagnostic text was provided."
                : diagnosticText.Trim());
            return builder.ToString().Trim();
        }

        public static int EstimateTokensByChars(string str)
        {
            return str.Length / 4;
        }

        public static int EstimateCharsByTokens(int nTokens)
        {
            return Convert.ToInt32(Convert.ToDouble(nTokens) * defaultContextUsage * 4);
        }


        public static int EstimatePrefixLinesByCtx(int? nCtx)
        {
            if (!nCtx.HasValue)
            {
                return 0;
            }

            return Convert.ToInt32(EstimateCharsByTokens(nCtx.Value) * PrefixCodeLinePercent) / defaultCodeLineLength;
        }

        public static int EstimateSuffixLinesByCtx(int? nCtx)
        {
            if (!nCtx.HasValue)
            {
                return 0;
            }

            return Convert.ToInt32(EstimateCharsByTokens(nCtx.Value) * SuffixCodeLinePercent) / defaultCodeLineLength;
        }

        private void Options_SettingsChanged(object sender, EventArgs e)
        {
            this.SetOllamaOptions();
        }

        private void SetOllamaOptions()
        {
            stop[0] = Options.FimBegin;
            stop[1] = Options.FimEnd;
            stop[2] = Options.FimHole;
            CompRequestOptions.Stop = stop;
            CompRequestOptions.NumCtx = Options.CompleteCtxSize;
            ChatRequestOptions.NumCtx = Options.ChatCtxSize;
            ChatRequestOptions.NumPredict = Options.ChatMaxOutputTokens;

            //Task.Run(async () => await this.InitModelCtxAsync());
        }

        public async Task InitModelCtxAsync()
        {
            // Initialize the context (num_ctx) for the chat model by querying Ollama
            try
            {
                // Retrieve model information for the chat and completion models
                var chatModelInfo = await ollamaService.ShowModelInformationAsync(Options.BaseUrl, Options.AccessToken, Options.ChatModel, default(System.Threading.CancellationToken));
                var compModelInfo = await ollamaService.ShowModelInformationAsync(Options.BaseUrl, Options.AccessToken, Options.CompleteModel, default(System.Threading.CancellationToken));

                // Helper to extract the 'num_ctx' parameter from a model's parameter string
                Func<string, string, int> GetCtx = (string parameters, string model) =>
                {
                    int num_ctx = defaultContext;
                    if (!string.IsNullOrEmpty(parameters))
                    {
                        // Look for a line like "PARAMETER num_ctx 2048" and parse the value
                        var match = Regex.Match(parameters, @"PARAMETER\s+num_ctx\s+(\d+)");
                        if (match.Success && match.Groups.Count > 1)
                        {
                            int.TryParse(match.Groups[1].Value, out num_ctx);
                        }
                    }
                    return num_ctx;
                };

                // Set the context for the chat request options
                int chatCtx = GetCtx(chatModelInfo.Parameters, Options.ChatModel);
                ChatRequestOptions.NumCtx = chatCtx;

                // The following lines are commented out because the completion model context is not yet used
                //int CompCtx = GetCtx(compModelInfo.Parameters, Options.CompleteModel);
                //CompRequestOptions.NumCtx = CompCtx;
            }
            catch (Exception ex)
            {
                // Handle any errors that occur while initializing the model settings
                LLMErrorHandler.HandleException(ex, "Unable to initialize Ollama model settings. Check that Ollama is reachable and the configured models exist.");
            }

        }

    }

    public static class EventManager
    {
        private static readonly ConcurrentQueue<CommandExecutedEventArgs> pendingCodeCommands = new ConcurrentQueue<CommandExecutedEventArgs>();
        private static EventHandler<CommandExecutedEventArgs> codeCommandExecuted;

        public static event EventHandler<CommandExecutedEventArgs> CodeCommandExecuted
        {
            add
            {
                codeCommandExecuted += value;
            }
            remove
            {
                codeCommandExecuted -= value;
            }
        }

        public static event EventHandler<CmdEventArgs> CmdEventsHandler;

        public static void OnCodeCommandExecuted(
            string selectedText,
            string promptOverride = null,
            string originalSelection = null,
            GeneratedResponseGuard responseGuard = GeneratedResponseGuard.None,
            AssistantActionCapabilities assistantActions = AssistantActionCapabilities.Discussion,
            bool resetConversation = true)
        {
            var args = new CommandExecutedEventArgs(selectedText, promptOverride, originalSelection, responseGuard, assistantActions, resetConversation);
            var handler = codeCommandExecuted;
            if (handler == null)
            {
                pendingCodeCommands.Enqueue(args);
                return;
            }

            handler.Invoke(null, args);
        }

        public static bool TryDequeuePendingCodeCommand(out CommandExecutedEventArgs args)
        {
            return pendingCodeCommands.TryDequeue(out args);
        }

        public static void OnCmdEventHandler(CmdEventType cmdType)
        {
            CmdEventsHandler?.Invoke(null, new CmdEventArgs(cmdType));
        }
    }

    public class CommandExecutedEventArgs : EventArgs
    {
        public string SelectedText { get; }
        public string PromptOverride { get; }
        public string OriginalSelection { get; }
        public GeneratedResponseGuard ResponseGuard { get; }
        public AssistantActionCapabilities AssistantActions { get; }
        public bool ResetConversation { get; }

        public CommandExecutedEventArgs(
            string selectedText,
            string promptOverride = null,
            string originalSelection = null,
            GeneratedResponseGuard responseGuard = GeneratedResponseGuard.None,
            AssistantActionCapabilities assistantActions = AssistantActionCapabilities.Discussion,
            bool resetConversation = true)
        {
            SelectedText = selectedText;
            PromptOverride = promptOverride;
            OriginalSelection = originalSelection;
            ResponseGuard = responseGuard;
            AssistantActions = assistantActions;
            ResetConversation = resetConversation;
        }
    }

    public enum GeneratedResponseGuard
    {
        None,
        CommentOnly
    }

    [Flags]
    public enum AssistantActionCapabilities
    {
        None = 0,
        UseAsDraft = 1 << 0,
        CopyCode = 1 << 1,
        PreviewDiff = 1 << 2,
        InsertIntoEditor = 1 << 3,
        ReplaceSelection = 1 << 4,
        ReplaceFile = 1 << 5,
        CreateSiblingFile = 1 << 6,
        Discussion = UseAsDraft,
        FileGeneration = UseAsDraft | CopyCode | InsertIntoEditor | CreateSiblingFile,
        FileRewrite = UseAsDraft | CopyCode | PreviewDiff | InsertIntoEditor | ReplaceFile,
        SelectionEdit = UseAsDraft | CopyCode | PreviewDiff | InsertIntoEditor | ReplaceSelection | ReplaceFile | CreateSiblingFile
    }

    public enum CmdEventType
    {
        ClearMessages,
        ListModels
    }

    public class CmdEventArgs : EventArgs
    {
        public CmdEventType CmdType { get; }

        public CmdEventArgs(CmdEventType cmdType)
        {
            CmdType = cmdType;
        }
    }

    public class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate UserTemplate { get; set; }
        public DataTemplate AssistantTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var message = item as MyMessage;
            if (message != null)
            {
                return message.Role == ChatRole.User ? UserTemplate : AssistantTemplate;
            }
            return base.SelectTemplate(item, container);
        }
    }

    public class OllamaClientFactory
    {
        private static readonly IOllamaService ollamaService = new OllamaSharpService();

        public static Chat CreateChat(Action<ChatResponseStream> streamer, ThinkingDepth? thinkingDepthOverride = null)
        {
            var options = OllamaHelper.Instance.Options;
            return ollamaService.CreateChatSession(
                options.BaseUrl,
                options.ChatModel,
                options.AccessToken,
                OllamaHelper.Instance.ChatRequestOptions,
                thinkingDepthOverride ?? options.ChatThinkingDepth,
                streamer);
        }
    }
}
