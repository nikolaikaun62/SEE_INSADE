using System;
using System.Windows;
using SEE_INSADE.Core.Imaging;
using SEE_INSADE.Core.Localization;

namespace SEE_INSADE.UI.Dialogs
{
    public partial class SettingsWindow : Window
    {
        public event EventHandler? SettingsApplied;

        public SettingsWindow()
            : this(
                1.0,
                1.0,
                false,
                OperatorFilterMode.EnhancedColor,
                1.0,
                false,
                1.0,
                false,
                1.0,
                false,
                false,
                false)
        {
        }

        public SettingsWindow(
            double defaultSpeed,
            double defaultSensitivity,
            bool invertDirection,
            OperatorFilterMode defaultFilterMode,
            double filterStrength,
            bool brightnessEnabled,
            double brightness,
            bool contrastEnabled,
            double contrast,
            bool materialEnhancementEnabled,
            bool edgeDetectionEnabled,
            bool noiseReductionEnabled)
        {
            InitializeComponent();
            LocalizationHelper.Apply(this);

            DefaultFilterComboBox.ItemsSource = Enum.GetValues(typeof(OperatorFilterMode));

            DefaultSpeedSlider.Value = defaultSpeed;
            DefaultSensitivitySlider.Value = defaultSensitivity;
            InvertDirectionCheck.IsChecked = invertDirection;
            DefaultFilterComboBox.SelectedItem = defaultFilterMode;
            FilterStrengthSlider.Value = filterStrength;

            BrightnessEnableCheck.IsChecked = brightnessEnabled;
            BrightnessSlider.Value = brightness;
            ContrastEnableCheck.IsChecked = contrastEnabled;
            ContrastSlider.Value = contrast;
            MaterialEnhancementCheck.IsChecked = materialEnhancementEnabled;
            EdgeDetectionCheck.IsChecked = edgeDetectionEnabled;
            NoiseReductionCheck.IsChecked = noiseReductionEnabled;

            RefreshValueLabels();
        }

        public double DefaultSpeed => DefaultSpeedSlider.Value;
        public double DefaultSensitivity => DefaultSensitivitySlider.Value;
        public bool InvertDirection => InvertDirectionCheck.IsChecked == true;
        public OperatorFilterMode DefaultFilterMode => DefaultFilterComboBox.SelectedItem is OperatorFilterMode mode ? mode : OperatorFilterMode.EnhancedColor;
        public double FilterStrength => FilterStrengthSlider.Value;

        public bool BrightnessEnabled => BrightnessEnableCheck.IsChecked == true;
        public double Brightness => BrightnessSlider.Value;

        public bool ContrastEnabled => ContrastEnableCheck.IsChecked == true;
        public double Contrast => ContrastSlider.Value;

        public bool MaterialEnhancementEnabled => MaterialEnhancementCheck.IsChecked == true;
        public bool EdgeDetectionEnabled => EdgeDetectionCheck.IsChecked == true;
        public bool NoiseReductionEnabled => NoiseReductionCheck.IsChecked == true;

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RefreshValueLabels();
        }

        private void RefreshValueLabels()
        {
            if (DefaultSpeedValueText != null)
                DefaultSpeedValueText.Text = $"{DefaultSpeedSlider.Value:F1}x";

            if (DefaultSensitivityValueText != null)
                DefaultSensitivityValueText.Text = $"{DefaultSensitivitySlider.Value * 100:F0}%";

            if (FilterStrengthValueText != null)
                FilterStrengthValueText.Text = $"{FilterStrengthSlider.Value:F1}x";

            if (BrightnessValueText != null)
                BrightnessValueText.Text = $"Brightness: {BrightnessSlider.Value:F1}x";

            if (ContrastValueText != null)
                ContrastValueText.Text = $"Contrast: {ContrastSlider.Value:F1}x";
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            SettingsApplied?.Invoke(this, EventArgs.Empty);
            MessageBox.Show(
                LocalizationManager.Instance.T("message.settingsApplied"),
                LocalizationManager.Instance.T("message.success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SettingsApplied?.Invoke(this, EventArgs.Empty);
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
