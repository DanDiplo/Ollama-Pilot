using Microsoft.VisualStudio.Shell;
using OllamaPilot.UI.Settings;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OllamaPilot.Services.Ollama
{
    public sealed class OllamaValidationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    internal static class OllamaSettingsValidator
    {
        public static OllamaValidationResult Validate(OptionPageGrid options)
        {
            return ThreadHelper.JoinableTaskFactory.Run(() => ValidateAsync(options));
        }

        public static OllamaValidationResult ValidateLocal(OptionPageGrid options)
        {
            if (options == null)
            {
                return Fail("OllamaPilot settings are unavailable.");
            }

            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return Fail("The Base URL must be a valid http or https address.");
            }

            if (options.ChatCtxSize <= 0 || options.CompleteCtxSize <= 0)
            {
                return Fail("Context lengths must be greater than zero.");
            }

            if (options.ChatMaxOutputTokens <= 0)
            {
                return Fail("Chat max output tokens must be greater than zero.");
            }

            if (options.AutoCompleteDelayMs < 0)
            {
                return Fail("Auto Complete Delay (ms) must be zero or greater.");
            }

            if (options.AutoCompleteMinPrefixLength < 0)
            {
                return Fail("Auto Complete Min Prefix must be zero or greater.");
            }

            if (string.IsNullOrWhiteSpace(options.ChatModel))
            {
                return Fail("Chat model must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(options.CompleteModel))
            {
                return Fail("Code Complete Model must not be empty.");
            }

            if (options.EnableAutoComplete)
            {
                if (string.IsNullOrWhiteSpace(options.FimBegin)
                    || string.IsNullOrWhiteSpace(options.FimHole)
                    || string.IsNullOrWhiteSpace(options.FimEnd))
                {
                    return Fail("FIM begin, hole, and end tokens must be configured when autocomplete is enabled.");
                }
            }

            return new OllamaValidationResult
            {
                Success = true,
                Message = "Settings look valid locally. Use Test Connection to verify Ollama connectivity and installed models."
            };
        }

        public static async Task<OllamaValidationResult> ValidateAsync(OptionPageGrid options, CancellationToken cancellationToken = default)
        {
            var localValidation = ValidateLocal(options);
            if (!localValidation.Success)
            {
                return localValidation;
            }

            try
            {
                var models = (await OllamaHelper.OllamaService.ListLocalModelsAsync(options.BaseUrl, options.AccessToken, cancellationToken)).ToList();
                if (models.Count == 0)
                {
                    return Fail("Connected to Ollama, but no local models were found.");
                }

                var modelNames = models.Select(m => m.Name).ToList();

                if (!modelNames.Contains(options.ChatModel, StringComparer.OrdinalIgnoreCase))
                {
                    return Fail($"Chat model '{options.ChatModel}' was not found locally.");
                }

                if (!modelNames.Contains(options.CompleteModel, StringComparer.OrdinalIgnoreCase))
                {
                    return Fail($"Code Complete Model '{options.CompleteModel}' was not found locally.");
                }

                if (options.EnableAutoComplete)
                {
                    var completeModelInfo = await OllamaHelper.OllamaService.ShowModelInformationAsync(options.BaseUrl, options.AccessToken, options.CompleteModel, cancellationToken);
                    if (!SupportsInsert(completeModelInfo?.Template))
                    {
                        return Fail($"Code Complete Model '{options.CompleteModel}' does not appear to support fill-in-the-middle insert mode.");
                    }

                    var tokenValidation = ValidateFimTokens(options, completeModelInfo.Template);
                    if (!tokenValidation.Success)
                    {
                        return tokenValidation;
                    }
                }

                return new OllamaValidationResult
                {
                    Success = true,
                    Message = $"Connected to Ollama and found {models.Count} local model(s). Chat='{options.ChatModel}', Complete='{options.CompleteModel}'."
                };
            }
            catch (Exception ex)
            {
                return Fail($"Unable to connect to Ollama. {ex.Message}");
            }
        }

        private static bool SupportsInsert(string template)
        {
            return !string.IsNullOrWhiteSpace(template)
                && template.Contains("fim_prefix")
                && template.Contains("fim_suffix")
                && template.Contains("fim_middle");
        }

        private static bool TokensMatchTemplate(OptionPageGrid options, string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return false;
            }

            return template.Contains(options.FimBegin ?? string.Empty)
                && template.Contains(options.FimEnd ?? string.Empty)
                && template.Contains(options.FimHole ?? string.Empty);
        }

        internal static bool TryGetSuggestedFimTokens(string modelName, out string fimBegin, out string fimHole, out string fimEnd)
        {
            fimBegin = null;
            fimHole = null;
            fimEnd = null;

            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            if (modelName.IndexOf("qwen", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fimBegin = "<|fim_prefix|>";
                fimHole = "<|fim_middle|>";
                fimEnd = "<|fim_suffix|>";
                return true;
            }

            if (modelName.IndexOf("deepseek", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fimBegin = "<｜fim▁begin｜>";
                fimHole = "<｜fim▁hole｜>";
                fimEnd = "<｜fim▁end｜>";
                return true;
            }

            return false;
        }

        private static OllamaValidationResult ValidateFimTokens(OptionPageGrid options, string template)
        {
            if (TokensMatchTemplate(options, template))
            {
                return new OllamaValidationResult { Success = true };
            }

            if (TryGetSuggestedFimTokens(options.CompleteModel, out var suggestedBegin, out var suggestedHole, out var suggestedEnd))
            {
                var matchesSuggestedTokens =
                    string.Equals(options.FimBegin, suggestedBegin, StringComparison.Ordinal) &&
                    string.Equals(options.FimHole, suggestedHole, StringComparison.Ordinal) &&
                    string.Equals(options.FimEnd, suggestedEnd, StringComparison.Ordinal);

                if (matchesSuggestedTokens)
                {
                    return new OllamaValidationResult { Success = true };
                }

                return Fail(
                    $"The configured FIM tokens do not match the selected completion model. Suggested tokens for '{options.CompleteModel}' are Begin='{suggestedBegin}', Hole='{suggestedHole}', End='{suggestedEnd}'.");
            }

            return Fail("The configured FIM tokens do not match the selected completion model template.");
        }

        private static OllamaValidationResult Fail(string message)
        {
            return new OllamaValidationResult
            {
                Success = false,
                Message = message
            };
        }
    }
}
