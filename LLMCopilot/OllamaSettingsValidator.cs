using Microsoft.VisualStudio.Shell;
using OllamaSharp;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LLMCopilot
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

        public static async Task<OllamaValidationResult> ValidateAsync(OptionPageGrid options, CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                return Fail("LLMCopilot settings are unavailable.");
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

            if (options.AutoCompleteDelayMs < 0)
            {
                return Fail("Auto Complete Delay (ms) must be zero or greater.");
            }

            if (options.AutoCompleteMinPrefixLength < 0)
            {
                return Fail("Auto Complete Min Prefix must be zero or greater.");
            }

            try
            {
                var client = new OllamaApiClient(options.BaseUrl);
                client.SetAuthorizationHeader(options.AccessToken);

                var models = (await client.ListLocalModels(cancellationToken)).ToList();
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
                    var completeModelInfo = await client.ShowModelInformation(options.CompleteModel, cancellationToken);
                    if (!SupportsInsert(completeModelInfo?.Template))
                    {
                        return Fail($"Code Complete Model '{options.CompleteModel}' does not appear to support fill-in-the-middle insert mode.");
                    }

                    if (!TokensMatchTemplate(options, completeModelInfo.Template))
                    {
                        return Fail("The configured FIM tokens do not match the selected completion model template.");
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
