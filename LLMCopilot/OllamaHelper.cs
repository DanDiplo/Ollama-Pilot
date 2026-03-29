using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows.Controls;
using System.Windows;
using OllamaSharp.Models;
using Task = System.Threading.Tasks.Task;
using System.Text.RegularExpressions;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Microsoft.VisualStudio;
using System.Net.Http.Headers;
using System.IO;

namespace OllamaPilot
{
    public sealed class OllamaHelper
    {
        public string CodeCompleteTemplate = $"<|fim▁begin|>{0}<|fim▁hole|>{1}<|fim▁end|>";

        private static readonly Lazy<OllamaHelper> lazy = new Lazy<OllamaHelper>(() => new OllamaHelper());

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
            TemplateService = new PromptTemplateService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates"));

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

            CompRequestOptions = new RequestOptions {
                NumCtx = Options.CompleteCtxSize,
                NumPredict = 128,
                Stop = stop,
                Temperature = 0.3f
            };

            ChatRequestOptions = new RequestOptions
            {
                NumCtx = Options.ChatCtxSize,
                NumPredict = 1024,
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
            var fileTemplate = RenderPrompt("explain-code.rdt.md", code, code_type, file);
            if (!string.IsNullOrWhiteSpace(fileTemplate))
            {
                return fileTemplate;
            }

            return BuildLegacyExplainTemplate(code, code_type);
        }

        public string GetFindBugTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            var fileTemplate = RenderPrompt("find-bugs.rdt.md", code, code_type, file);
            if (!string.IsNullOrWhiteSpace(fileTemplate))
            {
                return fileTemplate;
            }

            return BuildLegacyFindBugTemplate(code, code_type);
        }

        public string GetOptimizeCodeTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            var fileTemplate = RenderPrompt("optimize-code.rdt.md", code, code_type, file);
            if (!string.IsNullOrWhiteSpace(fileTemplate))
            {
                return fileTemplate;
            }

            return BuildLegacyOptimizeCodeTemplate(code, code_type);
        }

        public string GetUnitTestTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            var fileTemplate = RenderPrompt("generate-unit-test.rdt.md", code, code_type, file);
            if (!string.IsNullOrWhiteSpace(fileTemplate))
            {
                return fileTemplate;
            }

            return BuildLegacyUnitTestTemplate(code, code_type);
        }

        public string GetReviewFileTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            var fileTemplate = RenderPrompt("review-file.rdt.md", code, code_type, file);
            if (!string.IsNullOrWhiteSpace(fileTemplate))
            {
                return fileTemplate;
            }

            return BuildLegacyReviewFileTemplate(code, code_type, file);
        }

        public string GetGenerateFileTestsTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            var fileTemplate = RenderPrompt("generate-file-tests.rdt.md", code, code_type, file);
            if (!string.IsNullOrWhiteSpace(fileTemplate))
            {
                return fileTemplate;
            }

            return BuildLegacyGenerateFileTestsTemplate(code, code_type, file);
        }

        public string GetSummarizeChangesTemplate(string repositoryRoot, string gitStatus, string diffText)
        {
            var prompt = TemplateService.RenderInitialPrompt("summarize-changes.rdt.md", new Dictionary<string, string>
            {
                { "selectedText", diffText },
                { "language", "diff" },
                { "location", Path.GetFileName(repositoryRoot ?? string.Empty) },
                { "statusText", string.IsNullOrWhiteSpace(gitStatus) ? "(clean status unavailable)" : gitStatus }
            });

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                return ReplaceFirst(prompt, "```", "```diff");
            }

            return BuildLegacySummarizeChangesTemplate(repositoryRoot, gitStatus, diffText);
        }

        public string GetAddCommentTemplate(string code, string file)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            var fileTemplate = RenderPrompt("document-code.rdt.md", code, code_type, file);
            if (!string.IsNullOrWhiteSpace(fileTemplate))
            {
                return fileTemplate;
            }

            return BuildLegacyAddCommentTemplate(code, code_type);
        }

        public string GetDiagnoseErrorsTemplate(string code, string file, string diagnosticText = null)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            if (TemplateService.TemplateExists("diagnose-errors.rdt.md"))
            {
                var prompt = TemplateService.RenderInitialPrompt("diagnose-errors.rdt.md", new Dictionary<string, string>
                {
                    { "selectedTextWithDiagnostics", BuildDiagnosticInput(code, code_type, diagnosticText) },
                    { "location", Path.GetFileName(file ?? string.Empty) }
                });

                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    return prompt;
                }
            }

            return BuildLegacyDiagnoseErrorsTemplate(code, code_type, diagnosticText);
        }

        public string GetFixErrorTemplate(string code, string file, string diagnosticText, int? lineNumber = null, int? columnNumber = null)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            var prompt = TemplateService.RenderInitialPrompt("fix-error.rdt.md", new Dictionary<string, string>
            {
                { "selectedText", code },
                { "language", code_type },
                { "location", Path.GetFileName(file ?? string.Empty) },
                { "diagnosticText", BuildDiagnosticSummary(diagnosticText, lineNumber, columnNumber) }
            });

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                return ReplaceFirst(prompt, "```", $"```{code_type}");
            }

            return BuildLegacyFixErrorTemplate(code, code_type, diagnosticText, lineNumber, columnNumber);
        }

        public string GetEditSelectionTemplate(string code, string file, string instructions)
        {
            string code_type = VsHelpers.GetSourceCodeType(file);
            var prompt = TemplateService.RenderInitialPrompt("edit-selection.rdt.md", new Dictionary<string, string>
            {
                { "selectedText", code },
                { "language", code_type },
                { "location", Path.GetFileName(file ?? string.Empty) },
                { "instructions", instructions }
            });

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                return ReplaceFirst(prompt, "```", $"```{code_type}");
            }

            return BuildLegacyEditSelectionTemplate(code, code_type, instructions);
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

        private static string ReplaceFirst(string text, string find, string replace)
        {
            var index = text.IndexOf(find, StringComparison.Ordinal);
            if (index < 0)
            {
                return text;
            }

            return text.Substring(0, index) + replace + text.Substring(index + find.Length);
        }

        private string BuildLegacyExplainTemplate(string code, string code_type)
        {
            string templateEN = $@"## Instructions
Summarize the code below (emphasizing its key functionality).

## Selected Code
```{code_type}
{code}
```

## Task
Summarize the code at a high level (including goal and purpose) with an emphasis on its key functionality.

## Response

";
            string templateCN = $@"## 说明
总结下面的代码（强调其关键功能）。

## 选定代码
```{code_type}
{code}
```

## 任务
高层次地总结代码（包括目标和目的），并着重介绍其关键功能。

## 回答

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
        }

        private string BuildLegacyFindBugTemplate(string code, string code_type)
        {
            string templateEN = $@"## Instructions
What could be wrong with the code below?
Only consider defects that would lead to incorrect behavior.
The programming language is {code_type}.

## Selected Code
```{code_type}
{code}
```

## Task
Describe what could be wrong with the code?
Only consider defects that would lead to incorrect behavior.
Provide potential fix suggestions where possible.
Consider that there might not be any problems with the code.
Include code snippets(using Markdown) and examples where appropriate.

## Analysis

";
            string templateCN = $@"
## 说明
下面的代码可能有什么问题？
只考虑会导致不正确行为的缺陷。
编程语言是{code_type}。

## 选定代码
```{code_type}
{code}
```

## 任务
描述代码可能有什么问题？
只考虑会导致不正确行为的缺陷。
在可能的情况下提供潜在的修复建议。
考虑代码可能没有任何问题的情况。
在适当的情况下包含代码片段（使用Markdown）和示例。

## 分析

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
        }

        private string BuildLegacyOptimizeCodeTemplate(string code, string code_type)
        {
            string templateEN = $@"## Instructions
How could the readability and performance of the code below be improved?
The programming language is {code_type}.
Consider overall readability, performance and idiomatic constructs.

## Selected Code
```{code_type}
{code}
```

## Task
How could the readability and performance of the code be improved?
The programming language is {code_type}.
Consider overall readability, performance and idiomatic constructs.
Provide potential improvements suggestions where possible.
Consider that the code might be perfect and no improvements are possible.
Include code snippets (using Markdown) and examples where appropriate.
The code snippets must contain valid {code_type} code.

## Readability and Performance Improvements

";
            string templateCN = $@"## 说明
如何提高下面代码的可读性和性能？
编程语言是{code_type}。
考虑整体可读性、性能和惯用构造。

## 选定代码
```{code_type}
{code}
```

## 任务
如何提高代码的可读性和性能？
编程语言是{code_type}。
考虑整体可读性、性能和惯用构造。
在可能的情况下提供潜在的改进建议。
考虑代码可能已经完美，没有改进的空间。
在适当的情况下包含代码片段（使用Markdown）和示例。
代码片段必须包含有效的{code_type}代码。

## 可读性和性能改进

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
        }

        private string BuildLegacyUnitTestTemplate(string code, string code_type)
        {
            string templateEN = $@"## Instructions
Write a unit test for the code below.

## Selected Code
```{code_type}
{code}
```

## Task
Write a unit test that contains test cases for the happy path and for all edge cases.
The programming language is {code_type}.

## Response

";
            string templateCN = $@"## 说明
为下面的代码编写单元测试。

## 选定代码
```{code_type}
{code}
```

## 任务
编写一个包含正常情况和所有边缘情况的单元测试。
编程语言是{code_type}。

## 回答

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
        }

        private string BuildLegacyAddCommentTemplate(string code, string code_type)
        {
            string templateEN = $@"## Instructions
Document the code on function/method/class level.
Avoid line comments.
The programming language is {code_type}.

## Code
```{code_type}
{code}
```

## Documented Code

";
            string templateCN = $@"## 说明
在函数/方法/类级别上对代码进行文档化。
避免行内注释。
编程语言是{code_type}。

## 代码
```{code_type}
{code}
```

## 文档化的代码

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
        }

        private string BuildLegacyReviewFileTemplate(string code, string code_type, string file)
        {
            string location = Path.GetFileName(file ?? string.Empty);
            string templateEN = $@"## Instructions
Review the current file from {location}.
Focus on correctness risks, maintainability issues, and the most valuable improvements first.
Call out missing tests where appropriate.

## Current File
```{code_type}
{code}
```

## Task
Summarize the file's purpose briefly, then list the most important findings.
Include concrete fix suggestions.
If the file looks good, say so and mention residual risks or test gaps.

## Review

";
            string templateCN = $@"## 说明
审查来自 {location} 的当前文件。
优先关注正确性风险、可维护性问题以及最有价值的改进点。
在适当情况下指出缺失的测试。

## 当前文件
```{code_type}
{code}
```

## 任务
先简要总结文件用途，然后列出最重要的问题。
提供具体修复建议。
如果文件整体不错，也请说明剩余风险或测试缺口。

## 审查

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
        }

        private string BuildLegacyGenerateFileTestsTemplate(string code, string code_type, string file)
        {
            string location = Path.GetFileName(file ?? string.Empty);
            string templateEN = $@"## Instructions
Generate a practical test file for the current code from {location}.
Cover happy paths, edge cases, and likely regressions.
Return exactly one fenced code block containing the test code only.

## Current File
```{code_type}
{code}
```

## Task
Write tests that match the conventions most likely used for {code_type}.
Prefer a complete, directly usable test scaffold over commentary.

## Response

";
            string templateCN = $@"## 说明
为来自 {location} 的当前代码生成一个实用的测试文件。
覆盖正常路径、边缘情况和可能的回归点。
仅返回一个包含测试代码的 fenced code block。

## 当前文件
```{code_type}
{code}
```

## 任务
编写符合 {code_type} 常见约定的测试代码。
优先提供可直接使用的测试骨架，而不是说明文字。

## 回答

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
        }

        private string BuildLegacySummarizeChangesTemplate(string repositoryRoot, string gitStatus, string diffText)
        {
            string location = Path.GetFileName(repositoryRoot ?? string.Empty);
            string templateEN = $@"## Instructions
Summarize the current Git changes for the repository {location}.
Draft a concise commit message and a short change summary.
Call out any risky or incomplete changes you notice from the diff.

## Git Status
{gitStatus}

## Diff
```diff
{diffText}
```

## Task
Respond with:
1. A one-line commit title.
2. A short paragraph summary.
3. Any notable risks, gaps, or follow-up checks.

## Response

";
            string templateCN = $@"## 说明
总结仓库 {location} 的当前 Git 变更。
草拟一个简洁的提交标题和简短的变更摘要。
指出从 diff 中看到的风险或未完成点。

## Git 状态
{gitStatus}

## Diff
```diff
{diffText}
```

## 任务
请返回：
1. 一行提交标题。
2. 一小段变更摘要。
3. 值得注意的风险、缺口或后续检查项。

## 回答

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
        }

        private string BuildLegacyEditSelectionTemplate(string code, string code_type, string instructions)
        {
            string templateEN = $@"## Instructions
You are editing existing {code_type} code.
Apply the user's requested change conservatively.
Preserve behavior unless the request clearly asks for a behavior change.
Keep naming, formatting, and style consistent with the original code.
Return exactly one fenced code block containing only the revised code.

## Requested Change
{instructions}

## Selected Code
```{code_type}
{code}
```

## Response

";
            string templateCN = $@"## 说明
你正在编辑现有的 {code_type} 代码。
请谨慎应用用户请求的修改。
除非请求明确要求改变行为，否则应保持原有行为。
保持与原始代码一致的命名、格式和风格。
只返回一个 Markdown 代码块，并且只包含修改后的代码。

## 修改要求
{instructions}

## 选定代码
```{code_type}
{code}
```

## 回答

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
        }

        private string BuildLegacyDiagnoseErrorsTemplate(string code, string code_type, string diagnosticText)
        {
            var diagnosticSectionEn = string.IsNullOrWhiteSpace(diagnosticText)
                ? "No explicit error text was provided. Infer likely issues from the code and nearby context."
                : diagnosticText;
            var diagnosticSectionCn = string.IsNullOrWhiteSpace(diagnosticText)
                ? "未提供明确的错误文本。请根据代码和附近上下文推断最可能的问题。"
                : diagnosticText;

            string templateEN = $@"## Instructions
You are diagnosing a likely error or warning in existing {code_type} code.
Focus on the most probable cause, the smallest safe fix, and any assumptions.

## Diagnostic Context
{diagnosticSectionEn}

## Selected Code
```{code_type}
{code}
```

## Task
Explain the likely cause of the error or warning.
Suggest the minimal fix first.
If a code change is helpful, include a short Markdown code block.

## Answer

";
            string templateCN = $@"## 说明
你正在诊断现有 {code_type} 代码中可能出现的错误或警告。
重点说明最可能的原因、最小且安全的修复方案，以及任何必要的假设。

## 诊断上下文
{diagnosticSectionCn}

## 选定代码
```{code_type}
{code}
```

## 任务
解释错误或警告最可能的原因。
优先给出最小修复方案。
如果有助于说明，可以附上简短的 Markdown 代码块。

## 回答

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
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

        private string BuildLegacyFixErrorTemplate(string code, string code_type, string diagnosticText, int? lineNumber, int? columnNumber)
        {
            var summaryEn = BuildDiagnosticSummary(diagnosticText, lineNumber, columnNumber);
            var summaryCn = BuildDiagnosticSummary(diagnosticText, lineNumber, columnNumber);

            string templateEN = $@"## Instructions
You are fixing an existing {code_type} error or warning.
Make the smallest safe change that resolves the diagnostic.
Preserve behavior unless the diagnostic requires a behavior change.
Return a brief explanation followed by exactly one fenced code block with the corrected code.

## Diagnostic
{summaryEn}

## Relevant Code
```{code_type}
{code}
```

## Response

";
            string templateCN = $@"## 说明
你正在修复现有 {code_type} 代码中的错误或警告。
请给出能解决诊断问题的最小且安全的修改。
除非诊断明确要求改变行为，否则应保持原有行为。
先给出简短说明，再返回且只返回一个包含修正后代码的 Markdown 代码块。

## 诊断信息
{summaryCn}

## 相关代码
```{code_type}
{code}
```

## 回答

";

            return Options.Language == ResponseLanguage.English ? templateEN : templateCN;
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
                // 如果 nCtx 没有值，则返回一个默认值，例如0
                return 0;
            }

            return Convert.ToInt32(EstimateCharsByTokens(nCtx.Value) * PrefixCodeLinePercent) / defaultCodeLineLength;
        }

        public static int EstimateSuffixLinesByCtx(int? nCtx)
        {
            if (!nCtx.HasValue)
            {
                // 如果 nCtx 没有值，则返回一个默认值，例如0
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
            
            //Task.Run(async () => await this.InitModelCtxAsync());
        }

        public async Task InitModelCtxAsync()
        {
            try
            {
                var client = OllamaClientFactory.CreateClient();
                var chatModelInfo = await client.ShowModelInformationAsync(Options.ChatModel);
                var compModelInfo = await client.ShowModelInformationAsync(Options.CompleteModel);

                Func<string, string, int> GetCtx = (string parameters, string model) =>
                {
                    int num_ctx = defaultContext; 
                    if (!string.IsNullOrEmpty(parameters))
                    {
                        var match = Regex.Match(parameters, @"PARAMETER\s+num_ctx\s+(\d+)");
                        if (match.Success && match.Groups.Count > 1)
                        {
                            int.TryParse(match.Groups[1].Value, out num_ctx);
                        }
                    }
                    return num_ctx;
                };

                int chatCtx = GetCtx(chatModelInfo.Parameters, Options.ChatModel);
                ChatRequestOptions.NumCtx = chatCtx;

                //int CompCtx = GetCtx(compModelInfo.Parameters, Options.CompleteModel);
                //CompRequestOptions.NumCtx = CompCtx;
            }
            catch (Exception ex)
            {
                LLMErrorHandler.HandleException(ex, "Unable to initialize Ollama model settings. Check that Ollama is reachable and the configured models exist.");
            }

        }

    }

    public static class EventManager
    {
        public static event EventHandler<CommandExecutedEventArgs> CodeCommandExecuted;
        public static event EventHandler<CmdEventArgs> CmdEventsHandler;

        public static void OnCodeCommandExecuted(string selectedText, string promptOverride = null)
        {
            CodeCommandExecuted?.Invoke(null, new CommandExecutedEventArgs(selectedText, promptOverride));
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

        public CommandExecutedEventArgs(string selectedText, string promptOverride = null)
        {
            SelectedText = selectedText;
            PromptOverride = promptOverride;
        }
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
        public static OllamaApiClient CreateClient()
        {
            var options = OllamaHelper.Instance.Options;
            var ollamaApiClient = new OllamaApiClient(options.BaseUrl);
            ollamaApiClient.SelectedModel = options.CompleteModel;
            ollamaApiClient.SetAuthorizationHeader(options.AccessToken);
            
            return ollamaApiClient;
        }

        public static Chat CreateChat(Action<ChatResponseStream> streamer)
        {
            var ollamaApiClient = CreateClient();
            var options = OllamaHelper.Instance.Options;
            ollamaApiClient.SelectedModel = options.ChatModel;
            var chat = new Chat(ollamaApiClient, streamer, OllamaHelper.Instance.ChatRequestOptions);

            return chat;
        }
    }
}
