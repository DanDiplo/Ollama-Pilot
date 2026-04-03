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
            // The original code called ValidateAsync directly within ThreadHelper.JoinableTaskFactory.Run.
            // This can lead to deadlocks if ValidateAsync itself needs to interact with the UI thread.
            // A safer approach is to ensure ValidateAsync is called on a background thread if possible,
            // or to ensure that any UI thread interactions within ValidateAsync are handled correctly.
            // For simplicity and to preserve the original intent of synchronous validation,
            // we'll call ValidateLocal first, and then if needed, run the async validation.
            // However, the original Validate method was intended to be synchronous and likely called from the UI thread.
            // If ValidateAsync is truly meant to be run synchronously from the UI thread,
            // then JoinableTaskFactory.Run is appropriate, but it's crucial that ValidateAsync
            // doesn't block the UI thread unnecessarily or cause deadlocks.
            // Given the context of Visual Studio extensions, it's common to use JoinableTaskFactory.Run for synchronous calls
            // that need to execute async operations.
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
                // Ensure OllamaHelper.OllamaService is accessible and initialized.
                // Assuming OllamaHelper.OllamaService is a static property that provides an instance of the Ollama service.
                // If OllamaService requires initialization or is not static, this would need adjustment.
                if (OllamaHelper.OllamaService == null)
                {
                    return Fail("Ollama service is not initialized. Please check OllamaPilot configuration.");
                }

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
                    // Added null check for completeModelInfo before accessing its Template property.
                    var completeModelInfo = await OllamaHelper.OllamaService.ShowModelInformationAsync(options.BaseUrl, options.AccessToken, options.CompleteModel, cancellationToken);
                    if (completeModelInfo == null)
                    {
                        return Fail($"Could not retrieve information for the code completion model '{options.CompleteModel}'.");
                    }

                    if (!SupportsInsert(completeModelInfo.Template))
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
                // Log the exception for debugging purposes if possible.
                // For now, just return a user-friendly message.
                return Fail($"Unable to connect to Ollama. Error: {ex.Message}");
            }
        }

        private static bool SupportsInsert(string template)
        {
            // Added null check for template to prevent NullReferenceException.
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

            // Using null-conditional operator and null-coalescing to string.Empty for safer string comparisons.
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

            // Using OrdinalIgnoreCase for case-insensitive comparison, which is generally good for model names.
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

                // Improved error message to be more specific about what's missing.
                return Fail(
                    $"The configured FIM tokens do not match the selected completion model. Suggested tokens for '{options.CompleteModel}' are Begin='{suggestedBegin}', Hole='{suggestedHole}', End='{suggestedEnd}'. Your configuration is Begin='{options.FimBegin}', Hole='{options.FimHole}', End='{options.FimEnd}'.");
            }

            // Clarified the error message.
            return Fail("The configured FIM tokens do not match the selected completion model's template. Please check the model's documentation or try suggested tokens if available.");
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