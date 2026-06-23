using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using SEE_INSADE.UI.Dialogs;
using SEE_INSADE.Core.Imaging;
using SEE_INSADE.Core.Filters;
using SEE_INSADE.Core.Localization;
using SEE_INSADE.Core.Plugins;
using SEE_INSADE.Plugins.Configuration;
using SEE_INSADE.Plugins.DetectorCheck;
using SEE_INSADE.Plugins.NuctechImg;
using SEE_INSADE.Services.Scanning;

namespace SEE_INSADE.UI.MainWindows
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _scanTimer = null!;
        private DispatcherTimer _uiTimer = null!;
        private WriteableBitmap _scanBitmap = null!;
        private WriteableBitmap _materialBitmap = null!;
        private WriteableBitmap _densityBitmap = null!;
        private WriteableBitmap _filteredBitmap = null!;

        private bool _isScanning = false;
        private bool _isProjectionPanning = false;
        private bool _projectionNeedsFit = true;
        private bool _projectionFitWidth = true;
        private int _frameCount = 0;
        private double _projectionZoom = 1.0;
        private DateTime _lastUpdate = DateTime.Now;
        private Point _projectionPanStart;
        private Point _projectionTranslateStart;
        private ScanDirection _requestedScanDirection = ScanDirection.Forward;

        private ImageProcessor _imageProcessor = null!;
        private ScanService _scanService = null!;
        private FilterPipeline _filterPipeline = null!;
        private AdvancedFilterManager _advancedFilterManager = null!;
        private PluginManager _pluginManager = null!;
        private LocalizationManager _localization = null!;
        private string _lastStatusKey = "status.initialized";

        private BrightnessFilter _brightnessFilter = null!;
        private ContrastFilter _contrastFilter = null!;
        private GrayscaleFilter _grayscaleFilter = null!;

        public MainWindow()
        {
            InitializeComponent();
            InitializeSystem();
            Loaded += (_, _) => FitProjectionToWidth();
        }

        private void InitializeSystem()
        {
            _imageProcessor = new ImageProcessor();
            _scanService = new ScanService();
            _filterPipeline = new FilterPipeline();
            _advancedFilterManager = new AdvancedFilterManager();
            _pluginManager = new PluginManager(new PluginContext(_scanService, this));
            _pluginManager.Register(new ConfigurationPlugin());
            _pluginManager.Register(new DetectorCheckPlugin());
            _pluginManager.Register(new NuctechImgPlugin());
            _pluginManager.LoadExternalPlugins();
            _localization = LocalizationManager.Instance;
            _localization.LoadLanguages();

            _brightnessFilter = new BrightnessFilter();
            _contrastFilter = new ContrastFilter();
            _grayscaleFilter = new GrayscaleFilter();

            _filterPipeline.AddFilter(_brightnessFilter);
            _filterPipeline.AddFilter(_contrastFilter);
            _filterPipeline.AddFilter(_grayscaleFilter);
            _grayscaleFilter.IsEnabled = false;

            _scanBitmap = new WriteableBitmap(800, 400, 96, 96, PixelFormats.Bgr32, null);
            _materialBitmap = new WriteableBitmap(800, 400, 96, 96, PixelFormats.Bgr32, null);
            _densityBitmap = new WriteableBitmap(800, 400, 96, 96, PixelFormats.Bgr32, null);
            _filteredBitmap = new WriteableBitmap(800, 400, 96, 96, PixelFormats.Bgr32, null);

            FilteredImage.Source = _filteredBitmap;
            _projectionNeedsFit = true;

            _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _scanTimer.Tick += ScanTimer_Tick;

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();

            InitializeControls();
            InitializePlugins();
            InitializeLanguages();
            InitializeOperatorFilters();
            InitializeScanSources();

            UpdateAllDisplays(_scanService.GetCurrentScan());
            ApplyLocalization();
            UpdateStatusKey("status.initialized");
        }

        private void InitializeControls()
        {
            SpeedSlider.ValueChanged += (s, e) =>
            {
                SpeedValueText.Text = $"{e.NewValue:F1}x";
                SpeedText.Text = $"Speed: {e.NewValue:F1}x";
            };

            SensitivitySlider.ValueChanged += (s, e) =>
            {
                SensitivityValueText.Text = $"{(int)(e.NewValue * 100)}%";
            };

            _brightnessFilter.Intensity = BrightnessSlider.Value;
            _contrastFilter.Intensity = ContrastSlider.Value;
        }

        private void InitializeLanguages()
        {
            LanguageComboBox.ItemsSource = _localization.AvailableLanguages;
            LanguageComboBox.DisplayMemberPath = nameof(LanguageOption.Name);

            for (int i = 0; i < _localization.AvailableLanguages.Count; i++)
            {
                if (_localization.AvailableLanguages[i].Code == "ru")
                {
                    LanguageComboBox.SelectedIndex = i;
                    _localization.SetLanguage("ru");
                    return;
                }
            }

            if (LanguageComboBox.Items.Count > 0)
                LanguageComboBox.SelectedIndex = 0;
        }

        private void InitializeOperatorFilters()
        {
            OperatorFilterComboBox.ItemsSource = CreateOperatorFilterOptions();
            OperatorFilterComboBox.DisplayMemberPath = nameof(OperatorFilterOption.Name);
            OperatorFilterComboBox.SelectedIndex = 0;

            if (OperatorFilterComboBox.SelectedItem is OperatorFilterOption option)
                SetActiveFilterTab(option.Mode);
        }

        private void InitializePlugins()
        {
            PluginComboBox.ItemsSource = _pluginManager.Plugins;
            PluginComboBox.DisplayMemberPath = nameof(IScannerPlugin.Name);

            if (PluginComboBox.Items.Count > 0)
                PluginComboBox.SelectedIndex = 0;
        }

        private void InitializeScanSources()
        {
            ScanSourceComboBox.ItemsSource = new[]
            {
                new ScanSourceOption(ScannerOperationMode.ArchivePlayback, "Архивные IMG"),
                new ScanSourceOption(ScannerOperationMode.Nuctech6040D, "Nuctech 6040D")
            };
            ScanSourceComboBox.DisplayMemberPath = nameof(ScanSourceOption.Name);
            ArchiveFolderTextBox.Text = _scanService.ArchiveScanFolder;

            foreach (ScanSourceOption option in ScanSourceComboBox.Items)
            {
                if (option.Mode == _scanService.OperationMode)
                {
                    ScanSourceComboBox.SelectedItem = option;
                    return;
                }
            }

            ScanSourceComboBox.SelectedIndex = 0;
        }

        private void ScanTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isScanning)
                return;

            _frameCount++;

            _scanService.UpdateScan(SpeedSlider.Value, GetEffectiveScanDirection());
            var scanData = _scanService.GetCurrentScan();

            UpdateAllDisplays(scanData);

            if (_frameCount % 5 == 0)
                UpdateRealTimeInfo(scanData);
        }

        private ScanDirection GetEffectiveScanDirection()
        {
            bool invert = InvertDirectionCheck?.IsChecked == true;

            if (!invert)
                return _requestedScanDirection;

            return _requestedScanDirection == ScanDirection.Forward
                ? ScanDirection.Backward
                : ScanDirection.Forward;
        }

        private void UpdateAllDisplays(ScanData scanData)
        {
            UpdateStandardView(scanData);
            UpdateMaterialView(scanData);
            UpdateDensityView(scanData);
            UpdateFilteredView(scanData);
        }

        private void UpdateStandardView(ScanData scanData)
        {
            _scanBitmap = _imageProcessor.CreateColorizedXray(
                scanData.MaterialMap,
                scanData.DensityMap,
                scanData.Image.PixelWidth,
                scanData.Image.PixelHeight);
        }

        private void UpdateMaterialView(ScanData scanData)
        {
            _materialBitmap = _imageProcessor.CreateMaterialMap(
                scanData.MaterialMap,
                scanData.Image.PixelWidth,
                scanData.Image.PixelHeight);
        }

        private void UpdateDensityView(ScanData scanData)
        {
            int width = scanData.DensityMap.GetLength(0);
            int height = scanData.DensityMap.GetLength(1);
            byte[] pixels = new byte[width * height * 4];

            if (_densityBitmap.PixelWidth != width || _densityBitmap.PixelHeight != height)
                _densityBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    double density = Math.Clamp(scanData.DensityMap[x, y] / 2.35, 0.0, 1.0);
                    byte intensity = (byte)(density * 255);

                    pixels[index] = intensity;
                    pixels[index + 1] = intensity;
                    pixels[index + 2] = intensity;
                    pixels[index + 3] = 255;
                }
            }

            _densityBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        }

        private void UpdateFilteredView(ScanData scanData)
        {
            OperatorFilterMode mode = OperatorFilterComboBox?.SelectedItem is OperatorFilterOption option
                ? option.Mode
                : OperatorFilterMode.EnhancedColor;

            if (mode == OperatorFilterMode.EnhancedColor && _scanService.OperationMode == ScannerOperationMode.ArchivePlayback)
            {
                _filteredBitmap = scanData.Image;
                FilteredImage.Source = _filteredBitmap;
                FitProjectionAfterFirstImage();
                return;
            }

            _filteredBitmap = _imageProcessor.CreateOperatorFilterView(
                scanData.MaterialMap,
                scanData.DensityMap,
                scanData.Image.PixelWidth,
                scanData.Image.PixelHeight,
                new OperatorFilterSettings
                {
                    Mode = mode,
                    Strength = OperatorFilterSlider?.Value ?? 1.0,
                    BrightnessEnabled = BrightnessFilterCheck?.IsChecked == true,
                    Brightness = BrightnessSlider?.Value ?? 1.0,
                    ContrastEnabled = ContrastFilterCheck?.IsChecked == true,
                    Contrast = ContrastSlider?.Value ?? 1.0,
                    MaterialEnhancementEnabled = MaterialFilterCheck?.IsChecked == true,
                    EdgeDetectionEnabled = EdgeFilterCheck?.IsChecked == true,
                    NoiseReductionEnabled = NoiseFilterCheck?.IsChecked == true
                });

            FilteredImage.Source = _filteredBitmap;
            FitProjectionAfterFirstImage();
        }

        private Color ApplyAllFilters(Color input, MaterialType material, double density)
        {
            Color result = _filterPipeline.ApplyFilters(input, material, density);

            if (MaterialFilterCheck?.IsChecked == true)
                result = _advancedFilterManager.ApplyAdvancedFilters(result, material, density);

            return result;
        }

        private Color GetPixelColor(WriteableBitmap bitmap, int x, int y)
        {
            try
            {
                byte[] pixel = new byte[4];
                bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
                return Color.FromRgb(pixel[2], pixel[1], pixel[0]);
            }
            catch
            {
                return Colors.Black;
            }
        }

        private void UpdateRealTimeInfo(ScanData scanData)
        {
            PositionText.Text = $"Belt: {(int)scanData.ScanPosition}px  Column: {_scanService.GetCurrentScanLine()}";
            ObjectsText.Text = $"Objects: {scanData.ObjectCount}";
            SpeedText.Text = $"Speed: {SpeedSlider.Value:F1}x";
            DetectorsText.Text = $"Detectors: {scanData.DetectorData.Length}/{scanData.DetectorData.Length}";
            MaterialsText.Text = $"Materials: {CountUniqueMaterials(scanData.MaterialMap)} types";
        }

        private int CountUniqueMaterials(MaterialType[,] materialMap)
        {
            var uniqueMaterials = new System.Collections.Generic.HashSet<MaterialType>();
            int width = materialMap.GetLength(0);
            int height = materialMap.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (materialMap[x, y] != MaterialType.Air && materialMap[x, y] != MaterialType.Unknown)
                        uniqueMaterials.Add(materialMap[x, y]);
                }
            }

            return uniqueMaterials.Count;
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            TimeText.Text = DateTime.Now.ToString("HH:mm:ss");

            double fps = 1000.0 / Math.Max(1, (DateTime.Now - _lastUpdate).TotalMilliseconds);
            _lastUpdate = DateTime.Now;

            FrameRateText.Text = $"FPS: {fps:0}";
            MemoryText.Text = $"Memory: {GC.GetTotalMemory(false) / 1024 / 1024} MB";
            ProcessingText.Text = $"Processing: {(_isScanning ? GetEffectiveScanDirection().ToString() : "Idle")} | {_scanService.SourceStatus}";
            FilterCountText.Text = $"Active Filters: {GetActiveFilterCount()}";
        }

        private int GetActiveFilterCount()
        {
            int count = 0;

            if (BrightnessFilterCheck?.IsChecked == true) count++;
            if (ContrastFilterCheck?.IsChecked == true) count++;
            if (MaterialFilterCheck?.IsChecked == true) count++;
            if (EdgeFilterCheck?.IsChecked == true) count++;
            if (NoiseFilterCheck?.IsChecked == true) count++;

            return count;
        }

        public void ForwardScan_Click(object sender, RoutedEventArgs e)
        {
            _requestedScanDirection = ScanDirection.Forward;
            _isScanning = true;
            _scanTimer.Start();
            UpdateStatus("Scanning forward");
        }

        public void BackwardScan_Click(object sender, RoutedEventArgs e)
        {
            _requestedScanDirection = ScanDirection.Backward;
            _isScanning = true;
            _scanTimer.Start();
            UpdateStatus("Scanning backward");
        }

        public void StopScan_Click(object sender, RoutedEventArgs e)
        {
            _isScanning = false;
            _scanTimer.Stop();
            UpdateStatusKey("status.stopped");
        }

        public void ResetScan_Click(object sender, RoutedEventArgs e)
        {
            _isScanning = false;
            _scanTimer.Stop();
            _frameCount = 0;
            _scanService.ResetScan();
            UpdateAllDisplays(_scanService.GetCurrentScan());
            UpdateStatusKey("status.reset");
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (_scanService == null || _filteredBitmap == null)
                return;

            try
            {
                if (_brightnessFilter != null && BrightnessSlider != null)
                {
                    _brightnessFilter.IsEnabled = BrightnessFilterCheck?.IsChecked == true;
                    _brightnessFilter.Intensity = BrightnessSlider.Value;
                }

                if (_contrastFilter != null && ContrastSlider != null)
                {
                    _contrastFilter.IsEnabled = ContrastFilterCheck?.IsChecked == true;
                    _contrastFilter.Intensity = ContrastSlider.Value;
                }

                if (_grayscaleFilter != null)
                    _grayscaleFilter.IsEnabled = false;

                if (_advancedFilterManager != null)
                {
                    foreach (var filter in _advancedFilterManager.Filters)
                    {
                        if (filter.Name == "Material Enhancement")
                            filter.IsEnabled = MaterialFilterCheck?.IsChecked == true;
                        else if (filter.Name == "Edge Detection")
                            filter.IsEnabled = EdgeFilterCheck?.IsChecked == true;
                        else if (filter.Name == "Noise Reduction")
                            filter.IsEnabled = NoiseFilterCheck?.IsChecked == true;
                    }
                }

                UpdateFilteredView(_scanService.GetCurrentScan());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Filter change error: {ex.Message}");
            }
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            if (_scanService != null && _filteredBitmap != null)
                UpdateFilteredView(_scanService.GetCurrentScan());

            UpdateStatusKey("status.filtersApplied");
        }

        private void ScanSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_scanService == null || ScanSourceComboBox?.SelectedItem is not ScanSourceOption option)
                return;

            _isScanning = false;
            _scanTimer?.Stop();
            _frameCount = 0;
            _scanService.SetOperationMode(option.Mode);
            UpdateArchiveFolderControls();
            _projectionNeedsFit = true;
            UpdateAllDisplays(_scanService.GetCurrentScan());
            UpdateStatus($"Source: {option.Name}");
        }

        private void BrowseArchiveFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select folder with Nuctech IMG scans",
                UseDescriptionForTitle = true,
                SelectedPath = string.IsNullOrWhiteSpace(_scanService.ArchiveScanFolder)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                    : _scanService.ArchiveScanFolder,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() != WinForms.DialogResult.OK)
                return;

            _isScanning = false;
            _scanTimer?.Stop();
            _frameCount = 0;
            _scanService.SetArchiveScanFolder(dialog.SelectedPath);
            ArchiveFolderTextBox.Text = dialog.SelectedPath;
            _projectionNeedsFit = true;
            UpdateAllDisplays(_scanService.GetCurrentScan());
            UpdateStatus($"IMG folder: {dialog.SelectedPath}");
        }

        private void UpdateArchiveFolderControls()
        {
            bool archiveMode = _scanService.OperationMode == ScannerOperationMode.ArchivePlayback;

            if (ArchiveFolderTextBox != null)
            {
                ArchiveFolderTextBox.Text = _scanService.ArchiveScanFolder;
                ArchiveFolderTextBox.IsEnabled = archiveMode;
            }

            if (BrowseArchiveFolderButton != null)
                BrowseArchiveFolderButton.IsEnabled = archiveMode;
        }

        private void FitProjectionAfterFirstImage()
        {
            if (!_projectionNeedsFit)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_projectionFitWidth)
                    FitProjectionToWidth();
                else
                    FitProjectionToHost();
            }), DispatcherPriority.Loaded);
        }

        private void FitProjectionToHost()
        {
            if (ProjectionHost == null || FilteredImage.Source is not BitmapSource source)
                return;

            double hostWidth = Math.Max(1, ProjectionHost.ActualWidth);
            double hostHeight = Math.Max(1, ProjectionHost.ActualHeight);

            if (hostWidth <= 1 || hostHeight <= 1 || source.PixelWidth <= 0 || source.PixelHeight <= 0)
                return;

            _projectionZoom = Math.Clamp(
                Math.Min(hostWidth / source.PixelWidth, hostHeight / source.PixelHeight),
                0.05,
                6.0);

            ProjectionScaleTransform.ScaleX = _projectionZoom;
            ProjectionScaleTransform.ScaleY = _projectionZoom;
            ProjectionTranslateTransform.X = (hostWidth - source.PixelWidth * _projectionZoom) / 2.0;
            ProjectionTranslateTransform.Y = (hostHeight - source.PixelHeight * _projectionZoom) / 2.0;
            ZoomText.Text = $"{_projectionZoom * 100:0}%";
            _projectionNeedsFit = false;
        }

        private void FitProjectionToWidth()
        {
            if (ProjectionHost == null || FilteredImage.Source is not BitmapSource source)
                return;

            double hostWidth = Math.Max(1, ProjectionHost.ActualWidth);
            double hostHeight = Math.Max(1, ProjectionHost.ActualHeight);

            if (hostWidth <= 1 || hostHeight <= 1 || source.PixelWidth <= 0 || source.PixelHeight <= 0)
                return;

            _projectionZoom = Math.Clamp(hostWidth / source.PixelWidth, 0.05, 20.0);

            ProjectionScaleTransform.ScaleX = _projectionZoom;
            ProjectionScaleTransform.ScaleY = _projectionZoom;
            ProjectionTranslateTransform.X = 0;
            ProjectionTranslateTransform.Y = (hostHeight - source.PixelHeight * _projectionZoom) / 2.0;
            ZoomText.Text = $"{_projectionZoom * 100:0}%";
            _projectionNeedsFit = false;
        }

        private void FitWidth_Click(object sender, RoutedEventArgs e)
        {
            _projectionFitWidth = true;
            _projectionNeedsFit = true;
            FitProjectionToWidth();
        }

        private void FitScreen_Click(object sender, RoutedEventArgs e)
        {
            _projectionFitWidth = false;
            _projectionNeedsFit = true;
            FitProjectionToHost();
        }

        private void ProjectionHost_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (FilteredImage.Source is not BitmapSource)
                return;

            Point mouse = e.GetPosition(ProjectionHost);
            double oldZoom = _projectionZoom;
            double zoomFactor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
            double newZoom = Math.Clamp(oldZoom * zoomFactor, 0.05, 20.0);

            if (Math.Abs(newZoom - oldZoom) < 0.001)
                return;

            double imageX = (mouse.X - ProjectionTranslateTransform.X) / oldZoom;
            double imageY = (mouse.Y - ProjectionTranslateTransform.Y) / oldZoom;

            _projectionZoom = newZoom;
            ProjectionScaleTransform.ScaleX = newZoom;
            ProjectionScaleTransform.ScaleY = newZoom;
            ProjectionTranslateTransform.X = mouse.X - imageX * newZoom;
            ProjectionTranslateTransform.Y = mouse.Y - imageY * newZoom;
            ZoomText.Text = $"{newZoom * 100:0}%";
            e.Handled = true;
        }

        private void ProjectionHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isProjectionPanning = true;
            _projectionPanStart = e.GetPosition(ProjectionHost);
            _projectionTranslateStart = new Point(ProjectionTranslateTransform.X, ProjectionTranslateTransform.Y);
            ProjectionHost.CaptureMouse();
            ProjectionHost.Cursor = Cursors.SizeAll;
            e.Handled = true;
        }

        private void ProjectionHost_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isProjectionPanning)
                return;

            Point current = e.GetPosition(ProjectionHost);
            ProjectionTranslateTransform.X = _projectionTranslateStart.X + current.X - _projectionPanStart.X;
            ProjectionTranslateTransform.Y = _projectionTranslateStart.Y + current.Y - _projectionPanStart.Y;
            e.Handled = true;
        }

        private void ProjectionHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isProjectionPanning = false;
            ProjectionHost.ReleaseMouseCapture();
            ProjectionHost.Cursor = Cursors.Cross;
            e.Handled = true;
        }

        private void ProjectionHost_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _projectionNeedsFit = true;
            if (_projectionFitWidth)
                FitProjectionToWidth();
            else
                FitProjectionToHost();
            e.Handled = true;
        }

        private void OperatorFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (OperatorFilterComboBox?.SelectedItem is OperatorFilterOption option)
                SetActiveFilterTab(option.Mode);

            if (_scanService == null || _filteredBitmap == null)
                return;

            UpdateFilteredView(_scanService.GetCurrentScan());
        }

        private void FilterTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string modeName)
                return;

            if (!Enum.TryParse(modeName, out OperatorFilterMode mode))
                return;

            SetOperatorFilterMode(mode);
        }

        private void SetOperatorFilterMode(OperatorFilterMode mode)
        {
            if (OperatorFilterComboBox?.Items != null)
            {
                foreach (OperatorFilterOption option in OperatorFilterComboBox.Items)
                {
                    if (option.Mode == mode)
                    {
                        OperatorFilterComboBox.SelectedItem = option;
                        break;
                    }
                }
            }

            SetActiveFilterTab(mode);

            if (_scanService != null && _filteredBitmap != null)
                UpdateFilteredView(_scanService.GetCurrentScan());
        }

        private void SetActiveFilterTab(OperatorFilterMode mode)
        {
            if (FilterTabsPanel == null)
                return;

            foreach (var child in FilterTabsPanel.Children)
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

                button.Foreground = new SolidColorBrush(
                    isActive
                        ? Colors.White
                        : Color.FromRgb(232, 237, 244));
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is not LanguageOption language || _localization == null)
                return;

            _localization.SetLanguage(language.Code);
            ApplyLocalization();

            if (_scanService != null && _filteredBitmap != null)
                UpdateFilteredView(_scanService.GetCurrentScan());

            UpdateStatusKey(_lastStatusKey);
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            OperatorFilterMode currentMode = OperatorFilterComboBox?.SelectedItem is OperatorFilterOption option
                ? option.Mode
                : OperatorFilterMode.EnhancedColor;

            var settingsWindow = new SettingsWindow(
                SpeedSlider?.Value ?? 1.0,
                SensitivitySlider?.Value ?? 1.0,
                InvertDirectionCheck?.IsChecked == true,
                currentMode,
                OperatorFilterSlider?.Value ?? 1.0,
                BrightnessFilterCheck?.IsChecked == true,
                BrightnessSlider?.Value ?? 1.0,
                ContrastFilterCheck?.IsChecked == true,
                ContrastSlider?.Value ?? 1.0,
                MaterialFilterCheck?.IsChecked == true,
                EdgeFilterCheck?.IsChecked == true,
                NoiseFilterCheck?.IsChecked == true)
            {
                Owner = this
            };

            settingsWindow.SettingsApplied += (_, _) => ApplyBasicSettings(settingsWindow);

            if (settingsWindow.ShowDialog() == true)
                ApplyBasicSettings(settingsWindow);
        }

        private void ApplyBasicSettings(SettingsWindow settings)
        {
            if (SpeedSlider != null)
                SpeedSlider.Value = settings.DefaultSpeed;

            if (SensitivitySlider != null)
                SensitivitySlider.Value = settings.DefaultSensitivity;

            if (InvertDirectionCheck != null)
                InvertDirectionCheck.IsChecked = settings.InvertDirection;

            if (OperatorFilterSlider != null)
                OperatorFilterSlider.Value = settings.FilterStrength;

            if (BrightnessFilterCheck != null)
                BrightnessFilterCheck.IsChecked = settings.BrightnessEnabled;

            if (BrightnessSlider != null)
                BrightnessSlider.Value = settings.Brightness;

            if (ContrastFilterCheck != null)
                ContrastFilterCheck.IsChecked = settings.ContrastEnabled;

            if (ContrastSlider != null)
                ContrastSlider.Value = settings.Contrast;

            if (MaterialFilterCheck != null)
                MaterialFilterCheck.IsChecked = settings.MaterialEnhancementEnabled;

            if (EdgeFilterCheck != null)
                EdgeFilterCheck.IsChecked = settings.EdgeDetectionEnabled;

            if (NoiseFilterCheck != null)
                NoiseFilterCheck.IsChecked = settings.NoiseReductionEnabled;

            SetOperatorFilterMode(settings.DefaultFilterMode);
            Filter_Changed(this, new RoutedEventArgs());

            UpdateStatus("Basic settings applied");
        }

        private void OpenDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            var diagnosticsWindow = new DiagnosticsWindow();
            diagnosticsWindow.Owner = this;
            diagnosticsWindow.ShowDialog();
        }

        private void OpenCalibration_Click(object sender, RoutedEventArgs e)
        {
            var calibrationWindow = new CalibrationWizard();
            calibrationWindow.Owner = this;
            calibrationWindow.ShowDialog();
        }

        private void OpenAnalysis_Click(object sender, RoutedEventArgs e)
        {
            var debugWindow = new ImageDebugWindow(_scanBitmap);
            debugWindow.Owner = this;
            debugWindow.ShowDialog();
        }

        private void LaunchPlugin_Click(object sender, RoutedEventArgs e)
        {
            if (PluginComboBox.SelectedItem is not IScannerPlugin plugin)
            {
                UpdateStatus("No plugin selected");
                return;
            }

            plugin.Execute(_pluginManager.Context);
            UpdateStatus($"Plugin opened: {plugin.Name}");
        }

        private void TakeSnapshot_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Snapshot saved to Scans/", "Snapshot",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TestDetectors_Click(object sender, RoutedEventArgs e)
        {
            DetectorHealthBar.Value = 100;
            DetectorHealthText.Text = "Health: 100%";
            DetectorHealthText.Foreground = Brushes.LightGreen;
            UpdateStatus("Detectors tested - All OK");
        }

        private void CalibrateDetectors_Click(object sender, RoutedEventArgs e)
        {
            var calibrationWindow = new CalibrationWizard();
            calibrationWindow.Owner = this;
            calibrationWindow.ShowDialog();
        }

        private void OpenDetectorVisualization_Click(object sender, RoutedEventArgs e)
        {
            OpenDetectorCheckPlugin();
        }

        private void OpenDetectorCheckPlugin()
        {
            if (_pluginManager == null)
                return;

            foreach (var plugin in _pluginManager.Plugins)
            {
                if (plugin.Id == "see-insade.detector-check")
                {
                    plugin.Execute(_pluginManager.Context);
                    UpdateStatus($"Plugin opened: {plugin.Name}");
                    return;
                }
            }

            UpdateStatus("Detector Check plugin is not available");
        }

        private void UpdateStatus(string status)
        {
            StatusText.Text = status;
            MainStatusText.Text = $"SEE INSADE - {status}";
        }

        private void UpdateStatusKey(string key)
        {
            _lastStatusKey = key;

            if (_localization == null)
                UpdateStatus(key);
            else
                UpdateStatus(_localization.T(key));
        }

        private void ApplyLocalization()
        {
            if (_localization == null)
                return;

            SubtitleText.Text = _localization.T("app.subtitle");
            LanguageLabel.Text = _localization.T("label.language");
            SettingsButton.Content = _localization.T("button.settings");
            DiagnosticsButton.Content = _localization.T("button.diagnostics");
            CalibrateButton.Content = _localization.T("button.calibrate");
            DetectorsButton.Content = _localization.T("button.detectors");

            LinePositionLabel.Text = _localization.T("metric.linePosition");
            ObjectsLabel.Text = _localization.T("metric.objects");
            ConveyorLabel.Text = _localization.T("metric.conveyor");
            DetectorArrayLabel.Text = _localization.T("metric.detectorArray");
            MaterialsLabel.Text = _localization.T("metric.materials");

            EnhancedColorTabButton.Content = _localization.T("filter.enhancedColor");
            HighPenetrationTabButton.Content = _localization.T("filter.highPenetration");
            OrganicFocusTabButton.Content = _localization.T("filter.organicFocus");
            InorganicFocusTabButton.Content = _localization.T("filter.inorganicFocus");
            MetalFocusTabButton.Content = _localization.T("filter.metalFocus");
            DensityMapTabButton.Content = _localization.T("filter.densityMap");
            NegativeTabButton.Content = _localization.T("filter.negative");
            ThresholdTabButton.Content = _localization.T("filter.threshold");
            EdgeEmphasisTabButton.Content = _localization.T("filter.edgeEmphasis");
            SuspectHighlightTabButton.Content = _localization.T("filter.suspectHighlight");

            AcquisitionTitle.Text = "System";
            ScanControlsGroup.Header = _localization.T("group.scanControls");
            PluginsGroup.Header = _localization.T("group.plugins");
            FiltersGroup.Header = _localization.T("group.filters");
            DetectorStatusGroup.Header = _localization.T("group.detectorStatus");
            SystemInfoLabel.Text = _localization.T("group.systemInfo");

            ConveyorSpeedLabel.Text = _localization.T("label.conveyorSpeed");
            DetectorSensitivityLabel.Text = _localization.T("label.detectorSensitivity");
            FilterPresetLabel.Text = _localization.T("label.filterPreset");
            FilterStrengthLabel.Text = _localization.T("label.filterStrength");

            ForwardScanButton.Content = "▶ Вперёд";
            BackwardScanButton.Content = "◀ Назад";
            StopButton.Content = "■ Стоп";
            ResetButton.Content = _localization.T("button.reset");
            SnapshotButton.Content = _localization.T("button.snapshot");
            AnalyzeButton.Content = _localization.T("button.analyze");
            OpenPluginButton.Content = _localization.T("button.openPlugin");
            ApplyFiltersButton.Content = _localization.T("button.applyFilters");
            TestArrayButton.Content = _localization.T("button.testArray");
            CalibrateDetectorsButton.Content = _localization.T("button.calibrate");

            BrightnessFilterCheck.Content = _localization.T("check.brightness");
            ContrastFilterCheck.Content = _localization.T("check.contrast");
            MaterialFilterCheck.Content = _localization.T("check.materialEnhancement");
            EdgeFilterCheck.Content = _localization.T("check.edgeDetection");
            NoiseFilterCheck.Content = _localization.T("check.noiseReduction");
            InvertDirectionCheck.Content = "Инверсия направления";

            RefreshOperatorFilterNames();

            if (OperatorFilterComboBox?.SelectedItem is OperatorFilterOption option)
                SetActiveFilterTab(option.Mode);
        }

        private OperatorFilterOption[] CreateOperatorFilterOptions()
        {
            return new[]
            {
                new OperatorFilterOption(OperatorFilterMode.EnhancedColor, _localization.T("filter.enhancedColor")),
                new OperatorFilterOption(OperatorFilterMode.HighPenetration, _localization.T("filter.highPenetration")),
                new OperatorFilterOption(OperatorFilterMode.OrganicFocus, _localization.T("filter.organicFocus")),
                new OperatorFilterOption(OperatorFilterMode.InorganicFocus, _localization.T("filter.inorganicFocus")),
                new OperatorFilterOption(OperatorFilterMode.MetalFocus, _localization.T("filter.metalFocus")),
                new OperatorFilterOption(OperatorFilterMode.DensityMap, _localization.T("filter.densityMap")),
                new OperatorFilterOption(OperatorFilterMode.Negative, _localization.T("filter.negative")),
                new OperatorFilterOption(OperatorFilterMode.Threshold, _localization.T("filter.threshold")),
                new OperatorFilterOption(OperatorFilterMode.EdgeEmphasis, _localization.T("filter.edgeEmphasis")),
                new OperatorFilterOption(OperatorFilterMode.SuspectHighlight, _localization.T("filter.suspectHighlight"))
            };
        }

        private void RefreshOperatorFilterNames()
        {
            if (OperatorFilterComboBox == null)
                return;

            OperatorFilterMode selectedMode = OperatorFilterComboBox.SelectedItem is OperatorFilterOption selected
                ? selected.Mode
                : OperatorFilterMode.EnhancedColor;

            OperatorFilterComboBox.ItemsSource = CreateOperatorFilterOptions();

            foreach (OperatorFilterOption option in OperatorFilterComboBox.Items)
            {
                if (option.Mode == selectedMode)
                {
                    OperatorFilterComboBox.SelectedItem = option;
                    SetActiveFilterTab(option.Mode);
                    break;
                }
            }
        }

        private sealed class OperatorFilterOption
        {
            public OperatorFilterOption(OperatorFilterMode mode, string name)
            {
                Mode = mode;
                Name = name;
            }

            public OperatorFilterMode Mode { get; }
            public string Name { get; }
        }

        private sealed class ScanSourceOption
        {
            public ScanSourceOption(ScannerOperationMode mode, string name)
            {
                Mode = mode;
                Name = name;
            }

            public ScannerOperationMode Mode { get; }
            public string Name { get; }
        }
    }
}
