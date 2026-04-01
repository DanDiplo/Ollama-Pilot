using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using OllamaPilot.Commands;
using OllamaPilot.Commands.Executors;
using OllamaPilot.Infrastructure;
using OllamaPilot.Package;
using OllamaPilot.Services.Ollama;
using OllamaPilot.Services.VisualStudio;
using OllamaPilot.UI.Chat;
using OllamaPilot.UI.Settings;

namespace OllamaPilot.UI.Settings
{
    public partial class SettingsWindow : Window
    {
        private readonly OptionPageGrid _options;
        private IReadOnlyList<string> _availableModels = Array.Empty<string>();

        public SettingsWindow(OptionPageGrid options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            InitializeComponent();
            LoadStaticChoices();
            LoadFromOptions();
        }

        /// <summary>
        /// Populates combo boxes with available enum values for static configuration options.
        /// </summary>
        private void LoadStaticChoices()
        {
            LanguageComboBox.ItemsSource = Enum.GetValues(typeof(ResponseLanguage));
            TriggerModeComboBox.ItemsSource = Enum.GetValues(typeof(AutoCompleteTriggerMode));
            ThinkingDepthComboBox.ItemsSource = Enum.GetValues(typeof(ThinkingDepth));
        }

        /// <summary>
        /// Loads application configuration options into the UI controls.
        /// </summary>
        private void LoadFromOptions()
        {
            BaseUrlTextBox.Text = _options.BaseUrl;
            AccessTokenTextBox.Text = _options.AccessToken;
            ChatModelComboBox.Text = _options.ChatModel;
            CompleteModelComboBox.Text = _options.CompleteModel;
            ChatCtxTextBox.Text = _options.ChatCtxSize.ToString();
            ChatMaxOutputTextBox.Text = _options.ChatMaxOutputTokens.ToString();
            ThinkingDepthComboBox.SelectedItem = _options.ChatThinkingDepth;
            CompleteCtxTextBox.Text = _options.CompleteCtxSize.ToString();
            EnableAutoCompleteCheckBox.IsChecked = _options.EnableAutoComplete;
            TriggerModeComboBox.SelectedItem = _options.AutoCompleteTriggerMode;
            AutoCompleteDelayTextBox.Text = _options.AutoCompleteDelayMs.ToString();
            AutoCompleteMinPrefixTextBox.Text = _options.AutoCompleteMinPrefixLength.ToString();
            FimBeginTextBox.Text = _options.FimBegin;
            FimHoleTextBox.Text = _options.FimHole;
            FimEndTextBox.Text = _options.FimEnd;
            LanguageComboBox.SelectedItem = _options.Language;
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void RefreshModels_Click(object sender, RoutedEventArgs e)
        {
            await RefreshModelsAsync();
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyInputToOptions();
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, Brushes.OrangeRed);
                return;
            }

            SetStatus("Testing Ollama connection...", Brushes.DodgerBlue);
            var validation = await OllamaSettingsValidator.ValidateAsync(_options);
            SetStatus(validation.Message, validation.Success ? Brushes.SeaGreen : Brushes.OrangeRed);
            if (validation.Success)
            {
                await RefreshModelsAsync();
            }
        }

        private void FastPreset_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(
                "Fast preset applied: quicker completions with lighter context.",
                4096,
                1536,
                200,
                2,
                AutoCompleteTriggerMode.Smart);
        }

        private void BalancedPreset_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(
                "Balanced preset applied: a good default for everyday coding.",
                8192,
                2048,
                350,
                3,
                AutoCompleteTriggerMode.Smart);
        }

        private void QualityPreset_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(
                "Quality preset applied: more context and a calmer completion cadence.",
                16384,
                4096,
                500,
                4,
                AutoCompleteTriggerMode.Smart);
        }

        private void RecommendedModels_Click(object sender, RoutedEventArgs e)
        {
            ApplyRecommendedModels();
        }

        [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handlers require async void.")]
        private async void RecommendedContexts_Click(object sender, RoutedEventArgs e)
        {
            await ApplyRecommendedContextsAsync();
        }

        private async Task RefreshModelsAsync()
        {
            try
            {
                SetStatus("Loading local models...", Brushes.DodgerBlue);
                var models = (await OllamaHelper.OllamaService.ListLocalModelsAsync(
                    BaseUrlTextBox.Text.Trim(),
                    AccessTokenTextBox.Text?.Trim(),
                    default(System.Threading.CancellationToken)))
                    .Select(m => m.Name)
                    .OrderBy(n => n)
                    .ToList();
                _availableModels = models;

                ModelsListBox.ItemsSource = models;
                ChatModelComboBox.ItemsSource = models;
                CompleteModelComboBox.ItemsSource = models;

                if (!string.IsNullOrWhiteSpace(ChatModelComboBox.Text))
                {
                    ChatModelComboBox.Text = ChatModelComboBox.Text;
                }

                if (!string.IsNullOrWhiteSpace(CompleteModelComboBox.Text))
                {
                    CompleteModelComboBox.Text = CompleteModelComboBox.Text;
                }

                UpdateRecommendationText();
                SetStatus($"Loaded {models.Count} model(s) from Ollama.", Brushes.SeaGreen);
            }
            catch (Exception ex)
            {
                SetStatus($"Unable to load models. {ex.Message}", Brushes.OrangeRed);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (EnableAutoCompleteCheckBox.IsChecked == true)
                {
                    ApplySuggestedFimTokens(CompleteModelComboBox.Text?.Trim(), force: false);
                }

                ApplyInputToOptions();
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, Brushes.OrangeRed);
                return;
            }

            var validation = _options.ValidateSettings();
            if (!validation.Success)
            {
                SetStatus(validation.Message, Brushes.OrangeRed);
                return;
            }

            _options.PersistSettings();
            SetStatus("Settings saved. Use Test Connection to verify Ollama connectivity and installed models.", Brushes.SeaGreen);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyInputToOptions()
        {
            _options.BaseUrl = BaseUrlTextBox.Text?.Trim();
            _options.AccessToken = AccessTokenTextBox.Text?.Trim();
            _options.ChatModel = ChatModelComboBox.Text?.Trim();
            _options.CompleteModel = CompleteModelComboBox.Text?.Trim();
            _options.ChatCtxSize = ParsePositiveInt(ChatCtxTextBox.Text, "Chat Context Length");
            _options.ChatMaxOutputTokens = ParsePositiveInt(ChatMaxOutputTextBox.Text, "Chat Max Output Tokens");
            _options.ChatThinkingDepth = ThinkingDepthComboBox.SelectedItem is ThinkingDepth thinkingDepth
                ? thinkingDepth
                : ThinkingDepth.Medium;
            _options.CompleteCtxSize = ParsePositiveInt(CompleteCtxTextBox.Text, "Complete Context Length");
            _options.EnableAutoComplete = EnableAutoCompleteCheckBox.IsChecked == true;
            _options.AutoCompleteTriggerMode = TriggerModeComboBox.SelectedItem is AutoCompleteTriggerMode triggerMode
                ? triggerMode
                : AutoCompleteTriggerMode.Smart;
            _options.AutoCompleteDelayMs = ParseNonNegativeInt(AutoCompleteDelayTextBox.Text, "Delay (ms)");
            _options.AutoCompleteMinPrefixLength = ParseNonNegativeInt(AutoCompleteMinPrefixTextBox.Text, "Minimum Prefix Length");
            _options.FimBegin = FimBeginTextBox.Text?.Trim();
            _options.FimHole = FimHoleTextBox.Text?.Trim();
            _options.FimEnd = FimEndTextBox.Text?.Trim();
            _options.Language = LanguageComboBox.SelectedItem is ResponseLanguage language
                ? language
                : ResponseLanguage.English;
        }

        private static int ParsePositiveInt(string value, string label)
        {
            if (!int.TryParse(value, out var parsed) || parsed <= 0)
            {
                throw new InvalidOperationException($"{label} must be a whole number greater than zero.");
            }

            return parsed;
        }

        private static int ParseNonNegativeInt(string value, string label)
        {
            if (!int.TryParse(value, out var parsed) || parsed < 0)
            {
                throw new InvalidOperationException($"{label} must be a whole number zero or greater.");
            }

            return parsed;
        }

        private static int RecommendChatMaxOutputTokens(int chatContext)
        {
            return Math.Max(1024, Math.Min(8192, chatContext / 4));
        }

        private void SetStatus(string message, Brush brush)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = brush;
        }

        private void ApplyPreset(string statusMessage, int chatCtx, int completeCtx, int delayMs, int minPrefixLength, AutoCompleteTriggerMode triggerMode)
        {
            ChatCtxTextBox.Text = chatCtx.ToString();
            ChatMaxOutputTextBox.Text = RecommendChatMaxOutputTokens(chatCtx).ToString();
            CompleteCtxTextBox.Text = completeCtx.ToString();
            EnableAutoCompleteCheckBox.IsChecked = true;
            TriggerModeComboBox.SelectedItem = triggerMode;
            AutoCompleteDelayTextBox.Text = delayMs.ToString();
            AutoCompleteMinPrefixTextBox.Text = minPrefixLength.ToString();
            SetStatus(statusMessage, Brushes.SeaGreen);
        }

        private void ApplyRecommendedModels()
        {
            var models = _availableModels;
            if (models == null || models.Count == 0)
            {
                SetStatus("Refresh models first so Ollama Pilot can suggest a chat/completion split.", Brushes.DarkGoldenrod);
                return;
            }

            var preferredChatModel = SelectPreferredModel(models, preferLargest: true);
            var preferredCompletionModel = SelectPreferredModel(models, preferLargest: false);

            if (!string.IsNullOrWhiteSpace(preferredChatModel))
            {
                ChatModelComboBox.Text = preferredChatModel;
            }

            if (!string.IsNullOrWhiteSpace(preferredCompletionModel))
            {
                CompleteModelComboBox.Text = preferredCompletionModel;
                ApplySuggestedFimTokens(preferredCompletionModel, force: true);
            }

            UpdateRecommendationText();
            SetStatus(
                $"Recommended models applied: chat = {ChatModelComboBox.Text}, completion = {CompleteModelComboBox.Text}.",
                Brushes.SeaGreen);
        }

        private async Task ApplyRecommendedContextsAsync()
        {
            var baseUrl = BaseUrlTextBox.Text.Trim();
            var accessToken = AccessTokenTextBox.Text?.Trim();
            var chatModel = ChatModelComboBox.Text?.Trim();
            var completionModel = CompleteModelComboBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(chatModel) && string.IsNullOrWhiteSpace(completionModel))
            {
                SetStatus("Choose a chat and/or completion model first so Ollama Pilot can suggest working context sizes.", Brushes.DarkGoldenrod);
                return;
            }

            try
            {
                SetStatus("Inspecting selected model context limits...", Brushes.DodgerBlue);

                var chatSuggestion = await TryGetRecommendedContextAsync(baseUrl, accessToken, chatModel, isChat: true);
                var completionSuggestion = await TryGetRecommendedContextAsync(baseUrl, accessToken, completionModel, isChat: false);

                if (chatSuggestion.HasValue)
                {
                    ChatCtxTextBox.Text = chatSuggestion.Value.ToString();
                    ChatMaxOutputTextBox.Text = RecommendChatMaxOutputTokens(chatSuggestion.Value).ToString();
                }

                if (completionSuggestion.HasValue)
                {
                    CompleteCtxTextBox.Text = completionSuggestion.Value.ToString();
                }

                if (!chatSuggestion.HasValue && !completionSuggestion.HasValue)
                {
                    SetStatus("Ollama Pilot could not read model context limits. Keeping your existing values.", Brushes.DarkGoldenrod);
                    return;
                }

                var statusParts = new List<string>();
                if (chatSuggestion.HasValue)
                {
                    statusParts.Add($"chat = {chatSuggestion.Value}");
                }

                if (completionSuggestion.HasValue)
                {
                    statusParts.Add($"completion = {completionSuggestion.Value}");
                }

                SetStatus($"Recommended contexts applied: {string.Join(", ", statusParts)}.", Brushes.SeaGreen);
            }
            catch (Exception ex)
            {
                SetStatus($"Unable to recommend context sizes. {ex.Message}", Brushes.OrangeRed);
            }
        }

        private void UpdateRecommendationText()
        {
            if (_availableModels == null || _availableModels.Count == 0)
            {
                RecommendationTextBlock.Text = "Tip: use a larger reasoning model for chat and a smaller coder model for completion.";
                return;
            }

            var recommendedChat = SelectPreferredModel(_availableModels, preferLargest: true);
            var recommendedCompletion = SelectPreferredModel(_availableModels, preferLargest: false);

            if (!string.IsNullOrWhiteSpace(recommendedChat) && !string.IsNullOrWhiteSpace(recommendedCompletion))
            {
                RecommendationTextBlock.Text = $"Suggested split: chat = {recommendedChat}, completion = {recommendedCompletion}.";
                return;
            }

            RecommendationTextBlock.Text = $"Loaded {_availableModels.Count} model(s). Choose a larger chat model and a smaller completion model when possible.";
        }

        private static string SelectPreferredModel(IReadOnlyList<string> models, bool preferLargest)
        {
            var coderModels = models
                .Where(IsCoderModel)
                .OrderBy(m => preferLargest ? -ExtractApproximateModelSize(m) : ExtractApproximateModelSize(m))
                .ThenBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (coderModels.Count > 0)
            {
                return coderModels.First();
            }

            return models
                .OrderBy(m => preferLargest ? -ExtractApproximateModelSize(m) : ExtractApproximateModelSize(m))
                .ThenBy(m => m, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static bool IsCoderModel(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            return modelName.IndexOf("coder", StringComparison.OrdinalIgnoreCase) >= 0
                || modelName.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static double ExtractApproximateModelSize(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return 0;
            }

            var sizeToken = modelName.Split(':').LastOrDefault();
            if (string.IsNullOrWhiteSpace(sizeToken))
            {
                return 0;
            }

            sizeToken = sizeToken.Trim().ToLowerInvariant();
            if (sizeToken.EndsWith("b") && double.TryParse(sizeToken.Substring(0, sizeToken.Length - 1), out var billions))
            {
                return billions;
            }

            if (double.TryParse(sizeToken, out var plainNumber))
            {
                return plainNumber;
            }

            return 0;
        }

        private static int? TryExtractModelContextLimit(string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                return null;
            }

            var match = Regex.Match(parameters, @"PARAMETER\s+num_ctx\s+(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success || match.Groups.Count < 2)
            {
                return null;
            }

            if (!int.TryParse(match.Groups[1].Value, out var numCtx) || numCtx <= 0)
            {
                return null;
            }

            return numCtx;
        }

        private async Task<int?> TryGetRecommendedContextAsync(string baseUrl, string accessToken, string modelName, bool isChat)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return null;
            }

            var modelInfo = await OllamaHelper.OllamaService.ShowModelInformationAsync(
                baseUrl,
                accessToken,
                modelName,
                default(System.Threading.CancellationToken));

            var modelLimit = TryExtractModelContextLimit(modelInfo?.Parameters);
            if (!modelLimit.HasValue)
            {
                return RecommendContextFromModelSize(modelName, isChat);
            }

            return RecommendContextWindow(modelName, modelLimit.Value, isChat);
        }

        private static int RecommendContextFromModelSize(string modelName, bool isChat)
        {
            var modelSize = ExtractApproximateModelSize(modelName);
            if (isChat)
            {
                if (modelSize >= 14)
                {
                    return 8192;
                }

                if (modelSize >= 7)
                {
                    return 4096;
                }

                return 2048;
            }

            if (modelSize >= 14)
            {
                return 4096;
            }

            if (modelSize >= 7)
            {
                return 2048;
            }

            return 1536;
        }

        private static int RecommendContextWindow(string modelName, int modelLimit, bool isChat)
        {
            var modelSize = ExtractApproximateModelSize(modelName);
            var suggested = isChat
                ? RecommendChatContext(modelLimit, modelSize)
                : RecommendCompletionContext(modelLimit, modelSize);

            return Math.Max(1024, Math.Min(modelLimit, suggested));
        }

        private static int RecommendChatContext(int modelLimit, double modelSize)
        {
            var candidate = modelLimit >= 16384
                ? modelLimit / 2
                : (int)Math.Round(modelLimit * 0.75);

            var sizeCap = modelSize >= 20 ? 8192
                : modelSize >= 14 ? 12288
                : 16384;

            return RoundDownToStep(Clamp(candidate, 4096, Math.Min(modelLimit, sizeCap)), 1024);
        }

        private static int RecommendCompletionContext(int modelLimit, double modelSize)
        {
            var candidate = (int)Math.Round(modelLimit * 0.35);
            var sizeCap = modelSize >= 14 ? 4096 : 3072;
            return RoundDownToStep(Clamp(candidate, 1024, Math.Min(modelLimit, sizeCap)), 512);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static int RoundDownToStep(int value, int step)
        {
            if (step <= 1)
            {
                return value;
            }

            return Math.Max(step, (value / step) * step);
        }

        private void ApplySuggestedFimTokens(string modelName, bool force)
        {
            if (!OllamaSettingsValidator.TryGetSuggestedFimTokens(modelName, out var suggestedBegin, out var suggestedHole, out var suggestedEnd))
            {
                return;
            }

            if (!force && !ShouldReplaceFimTokens(suggestedBegin, suggestedHole, suggestedEnd))
            {
                return;
            }

            FimBeginTextBox.Text = suggestedBegin;
            FimHoleTextBox.Text = suggestedHole;
            FimEndTextBox.Text = suggestedEnd;
        }

        private bool ShouldReplaceFimTokens(string suggestedBegin, string suggestedHole, string suggestedEnd)
        {
            var currentBegin = FimBeginTextBox.Text?.Trim();
            var currentHole = FimHoleTextBox.Text?.Trim();
            var currentEnd = FimEndTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(currentBegin) || string.IsNullOrWhiteSpace(currentHole) || string.IsNullOrWhiteSpace(currentEnd))
            {
                return true;
            }

            if (string.Equals(currentBegin, suggestedBegin, StringComparison.Ordinal)
                && string.Equals(currentHole, suggestedHole, StringComparison.Ordinal)
                && string.Equals(currentEnd, suggestedEnd, StringComparison.Ordinal))
            {
                return false;
            }

            if (OllamaSettingsValidator.TryGetSuggestedFimTokens("deepseek", out var deepseekBegin, out var deepseekHole, out var deepseekEnd)
                && string.Equals(currentBegin, deepseekBegin, StringComparison.Ordinal)
                && string.Equals(currentHole, deepseekHole, StringComparison.Ordinal)
                && string.Equals(currentEnd, deepseekEnd, StringComparison.Ordinal))
            {
                return true;
            }

            if (OllamaSettingsValidator.TryGetSuggestedFimTokens("qwen", out var qwenBegin, out var qwenHole, out var qwenEnd)
                && string.Equals(currentBegin, qwenBegin, StringComparison.Ordinal)
                && string.Equals(currentHole, qwenHole, StringComparison.Ordinal)
                && string.Equals(currentEnd, qwenEnd, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }
    }
}
