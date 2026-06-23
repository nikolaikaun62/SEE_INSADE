using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SEE_INSADE.Core.Imaging;
using SEE_INSADE.Core.Raw;

namespace NuctechImgViewer
{
    public partial class MainWindow : Window
    {
        private readonly ImageProcessor _imageProcessor = new();
        private NuctechImgScan? _currentScan;
        private BitmapSource? _currentDisplayedImage;
        private OperatorFilterMode _currentMode = OperatorFilterMode.EnhancedColor;

        public MainWindow()
        {
            InitializeComponent();
            SetActiveFilterButton(_currentMode);
        }

        private void OpenImg_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Nuctech IMG",
                Filter = "Nuctech/OIS IMG (*.img)|*.img|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            LoadImg(dialog.FileName);
        }

        private void LoadImg(string filePath)
        {
            try
            {
                StatusText.Text = "Loading...";

                _currentScan = NuctechImgDecoder.Decode(
                    filePath,
                    detectorHeight: 0,
                    manualOffset: -1,
                    rotate90Clockwise: false,
                    flipHorizontal: false,
                    flipVertical: false);

                FileText.Text = _currentScan.FileName;
                MetaText.Text =
                    $"Model: {_currentScan.Model}   Serial: {_currentScan.SerialNumber}   Time: {_currentScan.ScanTimeText}   Size: {_currentScan.Width}x{_currentScan.Height}";

                UpdatePreview();
                StatusText.Text = "IMG loaded";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Load error";
                MessageBox.Show(this, ex.Message, "Nuctech IMG Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string modeName)
                return;

            if (!Enum.TryParse(modeName, out OperatorFilterMode mode))
                return;

            _currentMode = mode;
            SetActiveFilterButton(mode);
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
                return;
            }

            var settings = new OperatorFilterSettings
            {
                Mode = _currentMode,
                Strength = FilterStrengthSlider?.Value ?? 1.0,
                BrightnessEnabled = BrightnessCheck?.IsChecked == true,
                Brightness = BrightnessSlider?.Value ?? 1.0,
                ContrastEnabled = ContrastCheck?.IsChecked == true,
                Contrast = ContrastSlider?.Value ?? 1.0
            };

            WriteableBitmap filtered = _imageProcessor.CreateOperatorFilterView(
                _currentScan.MaterialMap,
                _currentScan.DensityMap,
                _currentScan.Width,
                _currentScan.Height,
                settings);

            PreviewImage.Source = filtered;
            _currentDisplayedImage = filtered;
        }

        private void ExportPng_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDisplayedImage == null)
            {
                MessageBox.Show(this, "Open an IMG file first.", "Nuctech IMG Viewer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export PNG",
                Filter = "PNG image (*.png)|*.png",
                FileName = _currentScan != null
                    ? Path.GetFileNameWithoutExtension(_currentScan.FileName) + "_export.png"
                    : "scan_export.png"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_currentDisplayedImage));

            using FileStream stream = File.Create(dialog.FileName);
            encoder.Save(stream);

            StatusText.Text = "Exported: " + dialog.FileName;
        }

        private void SetActiveFilterButton(OperatorFilterMode mode)
        {
            if (FilterButtonsPanel == null)
                return;

            foreach (object child in FilterButtonsPanel.Children)
            {
                if (child is not Button button)
                    continue;

                bool active = string.Equals(button.Tag?.ToString(), mode.ToString(), StringComparison.Ordinal);
                button.Background = active ? new SolidColorBrush(Color.FromRgb(0, 122, 120)) : Brushes.White;
                button.Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(23, 32, 44));
                button.BorderBrush = active ? new SolidColorBrush(Color.FromRgb(0, 122, 120)) : new SolidColorBrush(Color.FromRgb(212, 218, 227));
            }
        }
    }
}
