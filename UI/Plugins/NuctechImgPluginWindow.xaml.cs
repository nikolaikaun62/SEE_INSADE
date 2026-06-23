using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SEE_INSADE.Core.Imaging;
using SEE_INSADE.Core.Raw;

namespace SEE_INSADE.UI.Plugins
{
    public partial class NuctechImgPluginWindow : Window
    {
        private readonly ImageProcessor _imageProcessor = new();
        private NuctechImgScan? _currentScan;
        private string? _currentFilePath;
        private OperatorFilterMode _currentMode = OperatorFilterMode.EnhancedColor;
        private BitmapSource? _currentDisplayedImage;

        public NuctechImgPluginWindow()
        {
            InitializeComponent();
            SetActiveFilterButton(_currentMode);
        }

        private void OpenImg_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Nuctech IMG scan",
                Filter = "Nuctech IMG files (*.img)|*.img|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
            {
                _currentFilePath = dialog.FileName;
                LoadCurrentFile();
            }
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            LoadCurrentFile();
        }

        private void DecodeSettings_Changed(object sender, RoutedEventArgs e)
        {
            if (_currentFilePath != null)
                LoadCurrentFile();
        }

        private void LoadCurrentFile()
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath) || !File.Exists(_currentFilePath))
                return;

            try
            {
                int height = ParseDetectorHeight(HeightTextBox.Text);
                int offset = ParseOffset(OffsetTextBox.Text);

                _currentScan = NuctechImgDecoder.Decode(
                    _currentFilePath,
                    height,
                    offset,
                    RotateCheck.IsChecked == true,
                    FlipXCheck.IsChecked == true,
                    FlipYCheck.IsChecked == true);

                FileText.Text = _currentScan.FileName;
                MetaText.Text =
                    $"Model: {_currentScan.Model}   Serial: {_currentScan.SerialNumber}   Time: {_currentScan.ScanTimeText}   " +
                    $"Size: {_currentScan.Width}x{_currentScan.Height}   Offset: {_currentScan.DataOffset}   Tail: {_currentScan.TrailingBytes}";

                StatusText.Text = "IMG loaded successfully";
                UpdatePreview();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"IMG load error: {ex.Message}";
                MessageBox.Show(this, ex.Message, "Nuctech IMG load error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static int ParseDetectorHeight(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            if (string.Equals(text.Trim(), "Auto", StringComparison.OrdinalIgnoreCase))
                return 0;

            return int.TryParse(text, out int value) && value > 0 ? value : 0;
        }

        private static int ParseOffset(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return -1;

            if (string.Equals(text.Trim(), "Auto", StringComparison.OrdinalIgnoreCase))
                return -1;

            return int.TryParse(text, out int value) ? value : -1;
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string modeName)
                return;

            if (!Enum.TryParse(modeName, out OperatorFilterMode mode))
                return;

            _currentMode = mode;
            SetActiveFilterButton(_currentMode);
            UpdatePreview();
        }

        private void FilterSettings_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (_currentScan == null)
                return;

            if (_currentMode == OperatorFilterMode.EnhancedColor)
            {
                PreviewImage.Source = _currentScan.Bitmap;
                _currentDisplayedImage = _currentScan.Bitmap;
                StatusText.Text = "View: decoded IMG";
                return;
            }

            var settings = new OperatorFilterSettings
            {
                Mode = _currentMode,
                Strength = FilterStrengthSlider?.Value ?? 1.0,
                BrightnessEnabled = BrightnessCheck?.IsChecked == true,
                Brightness = BrightnessSlider?.Value ?? 1.0,
                ContrastEnabled = ContrastCheck?.IsChecked == true,
                Contrast = ContrastSlider?.Value ?? 1.0,
                MaterialEnhancementEnabled = MaterialCheck?.IsChecked == true,
                EdgeDetectionEnabled = EdgeCheck?.IsChecked == true,
                NoiseReductionEnabled = NoiseCheck?.IsChecked == true
            };

            WriteableBitmap filtered = _imageProcessor.CreateOperatorFilterView(
                _currentScan.MaterialMap,
                _currentScan.DensityMap,
                _currentScan.Width,
                _currentScan.Height,
                settings);

            PreviewImage.Source = filtered;
            _currentDisplayedImage = filtered;
            StatusText.Text = $"Filter: {_currentMode}";
        }

        private void SetActiveFilterButton(OperatorFilterMode mode)
        {
            if (FilterButtonsPanel == null)
                return;

            foreach (object child in FilterButtonsPanel.Children)
            {
                if (child is not Button button)
                    continue;

                bool isActive = string.Equals(button.Tag?.ToString(), mode.ToString(), StringComparison.Ordinal);

                button.Background = new SolidColorBrush(
                    isActive
                        ? Color.FromRgb(15, 118, 110)
                        : Color.FromRgb(24, 34, 48));

                button.BorderBrush = new SolidColorBrush(
                    isActive
                        ? Color.FromRgb(55, 211, 181)
                        : Color.FromRgb(45, 58, 77));

                button.Foreground = Brushes.White;
            }
        }

        private void ExportPng_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDisplayedImage == null)
            {
                MessageBox.Show(this, "Open an IMG file first.", "Export PNG", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export filtered image",
                Filter = "PNG image (*.png)|*.png",
                FileName = _currentScan != null
                    ? Path.GetFileNameWithoutExtension(_currentScan.FileName) + "_filtered.png"
                    : "filtered.png"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_currentDisplayedImage));

            using FileStream stream = File.Create(dialog.FileName);
            encoder.Save(stream);

            StatusText.Text = $"Exported: {dialog.FileName}";
        }
    }
}
