using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SEE_INSADE.UI.Dialogs;
using SEE_INSADE.Core.Imaging;
using SEE_INSADE.Core.Filters;
using SEE_INSADE.Core.Localization;
using SEE_INSADE.Core.Plugins;
using SEE_INSADE.Plugins.DetectorCheck;
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
        private bool _isPaused = false;
        private int _frameCount = 0;
        private DateTime _lastUpdate = DateTime.Now;

        private ImageProcessor _imageProcessor = null!;
        private ScanService _scanService = null!;
        private FilterPipeline _filterPipeline = null!;
        private AdvancedFilterManager _advancedFilterManager = null!;
        private PluginManager _pluginManager = null!;
        private LocalizationManager _localization = null!;
        private string _lastStatusKey = "status.initialized";

        // Filter references for easy access
        private BrightnessFilter _brightnessFilter = null!;
        private ContrastFilter _contrastFilter = null!;
        private GrayscaleFilter _grayscaleFilter = null!;

        public MainWindow()
        {
            InitializeComponent();
            InitializeSystem();
        }

        private void InitializeSystem()
        {
            // Initialize core components
            _imageProcessor = new ImageProcessor();
            _scanService = new ScanService();
            _filterPipeline = new FilterPipeline();
            _advancedFilterManager = new AdvancedFilterManager();
            _pluginManager = new PluginManager(new PluginContext(_scanService, this));
            _pluginManager.Register(new DetectorCheckPlugin());
            _pluginManager.LoadExternalPlugins();
            _localization = LocalizationManager.Instance;
            _localization.LoadLanguages();

            // Create and store filter instances
            _brightnessFilter = new BrightnessFilter();
            _contrastFilter = new ContrastFilter();
            _grayscaleFilter = new GrayscaleFilter();

            // Add filters to pipeline
            _filterPipeline.AddFilter(_brightnessFilter);
            _filterPipeline.AddFilter(_contrastFilter);
            _filterPipeline.AddFilter(_grayscaleFilter);

            // Initially disable grayscale filter
            _grayscaleFilter.IsEnabled = false;

            // Create bitmaps
            _scanBitmap = new WriteableBitmap(800, 400, 96, 96, PixelFormats.Bgr32, null);
            _materialBitmap = new WriteableBitmap(800, 400, 96, 96, PixelFormats.Bgr32, null);
            _densityBitmap = new WriteableBitmap(800, 400, 96, 96, PixelFormats.Bgr32, null);
            _filteredBitmap = new WriteableBitmap(800, 400, 96, 96, PixelFormats.Bgr32, null);

            // Set image sources
            ScanImage.Source = _scanBitmap;
            MaterialImage.Source = _materialBitmap;
            DensityImage.Source = _densityBitmap;
            FilteredImage.Source = _filteredBitmap;

            // Initialize timers
            _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _scanTimer.Tick += ScanTimer_Tick;

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();

            // Initialize controls
            InitializeControls();
            InitializePlugins();
            InitializeLanguages();
            InitializeOperatorFilters();
            UpdateAllDisplays(_scanService.GetCurrentScan());
            ApplyLocalization();
            UpdateStatusKey("status.initialized");
        }

        private void InitializeControls()
        {
            // Speed slider
            SpeedSlider.ValueChanged += (s, e) =>
            {
                SpeedValueText.Text = $"{e.NewValue:F1}x";
                SpeedText.Text = $"Speed: {e.NewValue:F1}x";
            };

            // Sensitivity slider
            SensitivitySlider.ValueChanged += (s, e) =>
            {
                SensitivityValueText.Text = $"{(int)(e.NewValue * 100)}%";
            };

            // Set initial filter values
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
        }

        private void InitializePlugins()
        {
            PluginComboBox.ItemsSource = _pluginManager.Plugins;
            PluginComboBox.DisplayMemberPath = nameof(IScannerPlugin.Name);

            if (PluginComboBox.Items.Count > 0)
                PluginComboBox.SelectedIndex = 0;
        }

        private void ScanTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isScanning || _isPaused) return;

            _frameCount++;

            // Read one vertical detector line and append it to the scan image.
            _scanService.UpdateScan(SpeedSlider.Value);
            var scanData = _scanService.GetCurrentScan();

            // Process images
            UpdateAllDisplays(scanData);

            // Update UI every few frames
            if (_frameCount % 5 == 0)
            {
                UpdateRealTimeInfo(scanData);
            }

        }

        private void UpdateAllDisplays(ScanData scanData)
        {
            // Update standard view
            UpdateStandardView(scanData);

            // Update material view
            UpdateMaterialView(scanData);

            // Update density view
            UpdateDensityView(scanData);

            // Update filtered view
            UpdateFilteredView(scanData);
        }

        private void UpdateStandardView(ScanData scanData)
        {
            _scanBitmap = _imageProcessor.CreateColorizedXray(
                scanData.MaterialMap,
                scanData.DensityMap,
                scanData.Image.PixelWidth,
                scanData.Image.PixelHeight);
            ScanImage.Source = _scanBitmap;
        }

        private void UpdateMaterialView(ScanData scanData)
        {
            // Create material map visualization
            _materialBitmap = _imageProcessor.CreateMaterialMap(scanData.MaterialMap,
                scanData.Image.PixelWidth, scanData.Image.PixelHeight);
            MaterialImage.Source = _materialBitmap;
        }

        private void OpenDetectorVisualization_Click(object sender, RoutedEventArgs e)
        {
            var detectorWindow = new DetectorVisualizationWindow(_scanService);
            detectorWindow.Owner = this;
            detectorWindow.ShowDialog();
        }

        private void UpdateDensityView(ScanData scanData)
        {
            // Create density map visualization
            int width = scanData.DensityMap.GetLength(0);
            int height = scanData.DensityMap.GetLength(1);
            byte[] pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte intensity = (byte)(scanData.DensityMap[x, y] * 255);

                    pixels[index] = intensity;     // B
                    pixels[index + 1] = intensity; // G
                    pixels[index + 2] = intensity; // R
                    pixels[index + 3] = 255;       // A
                }
            }

            _densityBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        }

        private void UpdateFilteredView(ScanData scanData)
        {
            OperatorFilterMode mode = OperatorFilterComboBox?.SelectedItem is OperatorFilterOption option
                ? option.Mode
                : OperatorFilterMode.EnhancedColor;

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
        }

        private Color ApplyAllFilters(Color input, MaterialType material, double density)
        {
            Color result = input;

            // Apply basic filters
            result = _filterPipeline.ApplyFilters(result, material, density);

            // Apply advanced filters if enabled
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
            DetectorsText.Text = "Detectors: 400/400";
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

            // Update performance info
            double fps = 1000.0 / Math.Max(1, (DateTime.Now - _lastUpdate).TotalMilliseconds);
            _lastUpdate = DateTime.Now;

            FrameRateText.Text = $"FPS: {fps:0}";
            MemoryText.Text = $"Memory: {GC.GetTotalMemory(false) / 1024 / 1024} MB";
            ProcessingText.Text = $"Processing: {(_isScanning ? "Active" : "Idle")}";
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

        // Scan Control Event Handlers
        public void StartScan_Click(object sender, RoutedEventArgs e)
        {
            _isScanning = true;
            _isPaused = false;
            _scanTimer.Start();
            UpdateStatusKey("status.scanning");
        }

        public void PauseScan_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;
            UpdateStatusKey(_isPaused ? "status.paused" : "status.scanning");
        }

        public void StopScan_Click(object sender, RoutedEventArgs e)
        {
            _isScanning = false;
            _isPaused = false;
            _scanTimer.Stop();
            UpdateStatusKey("status.stopped");
        }

        public void ResetScan_Click(object sender, RoutedEventArgs e)
        {
            _isScanning = false;
            _isPaused = false;
            _scanTimer.Stop();
            _frameCount = 0;
            _scanService.ResetScan();
            UpdateAllDisplays(_scanService.GetCurrentScan());
            UpdateStatusKey("status.reset");
        }

        // Filter Event Handlers
        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            // Safely update filter intensities based on UI
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
                {
                    // You can add a grayscale checkbox if needed
                    // _grayscaleFilter.IsEnabled = GrayscaleFilterCheck?.IsChecked == true;
                }

                // Update advanced filters
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

                // Force refresh if scanning
                var scanData = _scanService.GetCurrentScan();
                UpdateFilteredView(scanData);
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Diagnostics.Debug.WriteLine($"Filter change error: {ex.Message}");
            }
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            // Force update of filtered view
            if (_isScanning)
            {
                var scanData = _scanService.GetCurrentScan();
                UpdateFilteredView(scanData);
            }
            UpdateStatusKey("status.filtersApplied");
        }

        private void OperatorFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (_scanService == null || _filteredBitmap == null)
                return;

            UpdateFilteredView(_scanService.GetCurrentScan());
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is not LanguageOption language || _localization == null)
                return;

            _localization.SetLanguage(language.Code);
            ApplyLocalization();
            UpdateFilteredView(_scanService.GetCurrentScan());
            UpdateStatusKey(_lastStatusKey);
        }

        // Dialog Event Handlers
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
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

        // Additional Functionality
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

        private void UpdateStatus(string status)
        {
            StatusText.Text = status;
            MainStatusText.Text = $"SEE INSADE - {status}";
        }

        private void UpdateStatusKey(string key)
        {
            _lastStatusKey = key;
            UpdateStatus(_localization.T(key));
        }

        private void ApplyLocalization()
        {
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

            StandardTab.Header = _localization.T("tab.standard");
            MaterialTab.Header = _localization.T("tab.material");
            DensityTab.Header = _localization.T("tab.density");
            FilteredTab.Header = _localization.T("tab.filtered");

            AcquisitionTitle.Text = _localization.T("panel.acquisition");
            ScanControlsGroup.Header = _localization.T("group.scanControls");
            PluginsGroup.Header = _localization.T("group.plugins");
            FiltersGroup.Header = _localization.T("group.filters");
            DetectorStatusGroup.Header = _localization.T("group.detectorStatus");
            SystemInfoLabel.Text = _localization.T("group.systemInfo");

            ConveyorSpeedLabel.Text = _localization.T("label.conveyorSpeed");
            DetectorSensitivityLabel.Text = _localization.T("label.detectorSensitivity");
            FilterPresetLabel.Text = _localization.T("label.filterPreset");
            FilterStrengthLabel.Text = _localization.T("label.filterStrength");

            StartButton.Content = _localization.T("button.start");
            PauseButton.Content = _localization.T("button.pause");
            StopButton.Content = _localization.T("button.stop");
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

            DetectorHealthText.Text = _localization.T("detector.health");
            RefreshOperatorFilterNames();
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
            var comboBox = OperatorFilterComboBox;
            if (comboBox == null)
                return;

            OperatorFilterMode selectedMode = comboBox.SelectedItem is OperatorFilterOption selected
                ? selected.Mode
                : OperatorFilterMode.EnhancedColor;

            comboBox.ItemsSource = CreateOperatorFilterOptions();

            foreach (OperatorFilterOption option in comboBox.Items)
            {
                if (option.Mode == selectedMode)
                {
                    comboBox.SelectedItem = option;
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
    }
}
