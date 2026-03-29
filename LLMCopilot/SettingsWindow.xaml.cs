using Microsoft.VisualStudio.Shell;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Diagnostics.CodeAnalysis;

namespace LLMCopilot
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

        private void LoadStaticChoices()
        {
            LanguageComboBox.ItemsSource = Enum.GetValues(typeof(ResponseLanguage));
            TriggerModeComboBox.ItemsSource = Enum.GetValues(typeof(AutoCompleteTriggerMode));
        }

        private void LoadFromOptions()
        {
            BaseUrlTextBox.Text = _options.BaseUrl;
            AccessTokenTextBox.Text = _options.AccessToken;
            ChatModelComboBox.Text = _options.ChatModel;
            CompleteModelComboBox.Text = _options.CompleteModel;
            ChatCtxTextBox.Text = _options.ChatCtxSize.ToString();
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

        private async Task RefreshModelsAsync()
        {
            try
            {
                SetStatus("Loading local models...", Brushes.DodgerBlue);
                var client = new OllamaApiClient(BaseUrlTextBox.Text.Trim());
                client.SetAuthorizationHeader(AccessTokenTextBox.Text?.Trim());
                var models = (await client.ListLocalModels()).Select(m => m.Name).OrderBy(n => n).ToList();
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
            SetStatus("Settings saved.", Brushes.SeaGreen);
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

        private void SetStatus(string message, Brush brush)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = brush;
        }

        private void ApplyPreset(string statusMessage, int chatCtx, int completeCtx, int delayMs, int minPrefixLength, AutoCompleteTriggerMode triggerMode)
        {
            ChatCtxTextBox.Text = chatCtx.ToString();
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
                SetStatus("Refresh models first so LLMCopilot can suggest a chat/completion split.", Brushes.DarkGoldenrod);
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
            }

            UpdateRecommendationText();
            SetStatus(
                $"Recommended models applied: chat = {ChatModelComboBox.Text}, completion = {CompleteModelComboBox.Text}.",
                Brushes.SeaGreen);
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
    }
}
