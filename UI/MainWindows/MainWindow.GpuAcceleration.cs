using SEE_INSADE;
using SEE_INSADE.Core.Config;
using SEE_INSADE.Core.Imaging;
using SEE_INSADE.Services.Scanning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SEE_INSADE.UI.MainWindows
{
    public partial class MainWindow
    {
        private CheckBox? _gpuAccelerationCheckBox;
        private TextBlock? _gpuBackendText;
        private DispatcherTimer? _proStatusTimer;
        private ComboBox? _operatorPresetComboBox;
        private ComboBox? _renderQualityComboBox;
        private TextBlock? _proDashboardText;
        private TextBlock? _threatSummaryText;
        private TextBlock? _hotkeyHintText;
        private CheckBox? _showDashboardCheckBox;
        private CheckBox? _autoSaveReportCheckBox;
        private bool _proUiInitialized;

        private readonly ProPreset[] _proPresets =
        {
            new("Airport Standard", OperatorFilterMode.EnhancedColor, 1.00, 1.00, 1.00, true, false, true, 1.00),
            new("Maximum Penetration", OperatorFilterMode.HighPenetration, 1.85, 0.92, 1.35, false, false, false, 0.70),
            new("Organic Threat", OperatorFilterMode.OrganicFocus, 1.45, 1.08, 1.25, true, true, true, 0.85),
            new("Metal Search", OperatorFilterMode.MetalFocus, 1.55, 0.95, 1.40, true, true, false, 0.85),
            new("Edge Inspection", OperatorFilterMode.EdgeEmphasis, 1.35, 1.05, 1.50, true, true, false, 0.60),
            new("Low Noise", OperatorFilterMode.EnhancedColor, 0.85, 1.05, 1.05, true, false, true, 0.90),
            new("Density Inspector", OperatorFilterMode.DensityMap, 1.20, 1.00, 1.20, false, false, false, 0.75),
            new("High Contrast", OperatorFilterMode.Threshold, 1.35, 1.05, 1.70, false, false, false, 0.70)
        };

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            InitializeProOperatorUi();
        }

        private void InitializeProOperatorUi()
        {
            if (_proUiInitialized)
                return;

            _proUiInitialized = true;

            bool gpuEnabled = ConfigManager.Current.DisplaySettings.UseGpuAcceleration;
            if (_imageProcessor != null)
                _imageProcessor.UseGpuAcceleration = gpuEnabled;

            InstallProDashboardOverlay();
            UpgradeExistingMainWindowControls();
            InstallKeyboardShortcuts();
            ApplyPersistedProSettings();
            StartProStatusTimer();
            UpdateProStatusPanel();
        }

        private void InstallProDashboardOverlay()
        {
            if (Content is not Grid rootGrid)
                return;

            var panel = new Border
            {
                Width = 326,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 18, 18, 126),
                Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.FromRgb(8, 13, 20)) { Opacity = 0.95 },
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 62, 84)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.38,
                    Color = Color.FromRgb(0, 0, 0)
                }
            };

            Grid.SetRow(panel, 1);
            Panel.SetZIndex(panel, 50);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var stack = new StackPanel();
            scroll.Content = stack;
            panel.Child = scroll;

            stack.Children.Add(CreatePanelHeader());
            stack.Children.Add(CreateSeparator());
            stack.Children.Add(CreatePresetSection());
            stack.Children.Add(CreateSeparator());
            stack.Children.Add(CreatePerformanceSection());
            stack.Children.Add(CreateSeparator());
            stack.Children.Add(CreateGpuSection());
            stack.Children.Add(CreateSeparator());
            stack.Children.Add(CreateUsefulActionsSection());
            stack.Children.Add(CreateSeparator());
            stack.Children.Add(CreateLiveDashboardSection());

            rootGrid.Children.Add(panel);
        }

        private UIElement CreatePanelHeader()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            stack.Children.Add(new TextBlock
            {
                Text = "PRO OPERATOR PANEL",
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Presets • GPU • Export • Hotkeys",
                Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 181)),
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0)
            });
            return stack;
        }

        private UIElement CreatePresetSection()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("Operator presets"));

            _operatorPresetComboBox = new ComboBox
            {
                ItemsSource = _proPresets,
                DisplayMemberPath = nameof(ProPreset.Name),
                MinHeight = 34,
                Margin = new Thickness(0, 7, 0, 7),
                ToolTip = "Ready-made scanner views for different inspection tasks."
            };
            _operatorPresetComboBox.SelectionChanged += OperatorPresetComboBox_SelectionChanged;
            stack.Children.Add(_operatorPresetComboBox);

            var buttons = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 0) };
            buttons.Children.Add(CreateSmallButton("Auto tune", AutoTune_Click, "Adjust brightness/contrast from current density map."));
            buttons.Children.Add(CreateSmallButton("Reset view", ResetView_Click, "Return to Airport Standard view."));
            stack.Children.Add(buttons);

            return stack;
        }

        private UIElement CreatePerformanceSection()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("Performance"));

            _renderQualityComboBox = new ComboBox
            {
                ItemsSource = new[] { "High FPS", "Balanced", "Quality" },
                MinHeight = 34,
                Margin = new Thickness(0, 7, 0, 7),
                ToolTip = "High FPS: faster UI ticks. Balanced: default. Quality: slower but smoother inspection."
            };
            _renderQualityComboBox.SelectionChanged += RenderQualityComboBox_SelectionChanged;
            stack.Children.Add(_renderQualityComboBox);

            _showDashboardCheckBox = new CheckBox
            {
                Content = "Show live dashboard",
                IsChecked = ConfigManager.Current.DisplaySettings.ShowProDashboard,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 6, 0, 0)
            };
            _showDashboardCheckBox.Checked += (_, _) => SetDashboardVisibility(true);
            _showDashboardCheckBox.Unchecked += (_, _) => SetDashboardVisibility(false);
            stack.Children.Add(_showDashboardCheckBox);

            _autoSaveReportCheckBox = new CheckBox
            {
                Content = "Save TXT report with snapshots",
                IsChecked = ConfigManager.Current.DisplaySettings.AutoSaveAnalysisReport,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 6, 0, 0)
            };
            _autoSaveReportCheckBox.Checked += (_, _) => SetAutoReport(true);
            _autoSaveReportCheckBox.Unchecked += (_, _) => SetAutoReport(false);
            stack.Children.Add(_autoSaveReportCheckBox);

            return stack;
        }

        private UIElement CreateGpuSection()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("GPU backend"));

            _gpuAccelerationCheckBox = new CheckBox
            {
                Content = "Use GPU acceleration",
                IsChecked = ConfigManager.Current.DisplaySettings.UseGpuAcceleration,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 8, 0, 0),
                ToolTip = "ComputeSharp / DirectX 12 GPU filters. If unavailable, CPU fallback is used."
            };
            _gpuAccelerationCheckBox.Checked += GpuAccelerationChanged;
            _gpuAccelerationCheckBox.Unchecked += GpuAccelerationChanged;
            stack.Children.Add(_gpuAccelerationCheckBox);

            _gpuBackendText = new TextBlock
            {
                Text = CreateGpuBackendStatusText(),
                Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 181)),
                FontSize = 11,
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(_gpuBackendText);

            return stack;
        }

        private UIElement CreateUsefulActionsSection()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("Useful actions"));

            var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 7, 0, 0) };
            grid.Children.Add(CreateSmallButton("PNG snapshot", SaveSnapshot_Click, "Save the current filtered frame to Scans."));
            grid.Children.Add(CreateSmallButton("Analysis TXT", SaveAnalysisReport_Click, "Save material/density summary."));
            grid.Children.Add(CreateSmallButton("Suspect view", SuspectView_Click, "Fast switch to suspicious-object highlighting."));
            grid.Children.Add(CreateSmallButton("Metal view", MetalView_Click, "Fast switch to metal/electronics search."));
            grid.Children.Add(CreateSmallButton("Organic view", OrganicView_Click, "Fast switch to organics/liquids/plastics."));
            grid.Children.Add(CreateSmallButton("Penetration", PenetrationView_Click, "Fast switch to high penetration mode."));
            stack.Children.Add(grid);

            return stack;
        }

        private UIElement CreateLiveDashboardSection()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionTitle("Live diagnostics"));

            _proDashboardText = new TextBlock
            {
                Text = "Loading metrics...",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(216, 226, 239)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 7, 0, 0)
            };
            stack.Children.Add(_proDashboardText);

            _threatSummaryText = new TextBlock
            {
                Text = "Risk: --",
                Foreground = new SolidColorBrush(Color.FromRgb(55, 211, 181)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 9, 0, 0)
            };
            stack.Children.Add(_threatSummaryText);

            _hotkeyHintText = new TextBlock
            {
                Text = "Hotkeys: F5 forward • Space stop • F6 backward • Ctrl+S snapshot • Ctrl+1..8 presets • Ctrl+G GPU",
                Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 181)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0)
            };
            stack.Children.Add(_hotkeyHintText);

            return stack;
        }

        private TextBlock CreateSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromRgb(55, 211, 181)),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 3, 0, 0)
            };
        }

        private UIElement CreateSeparator()
        {
            return new Border
            {
                Height = 1,
                Margin = new Thickness(0, 12, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(37, 51, 70))
            };
        }

        private Button CreateSmallButton(string text, RoutedEventHandler handler, string? tooltip = null)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(3),
                Padding = new Thickness(8, 6, 8, 6),
                MinHeight = 32,
                Background = new SolidColorBrush(Color.FromRgb(24, 34, 48)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(53, 70, 94)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = tooltip
            };
            button.Click += handler;
            return button;
        }


        private void UpgradeExistingMainWindowControls()
        {
            try
            {
                if (SnapshotButton != null)
                {
                    SnapshotButton.Click -= TakeSnapshot_Click;
                    SnapshotButton.Click += SaveSnapshot_Click;
                    SnapshotButton.Content = "PNG + Report";
                    SnapshotButton.ToolTip = "Save real PNG snapshot and optional analysis report.";
                }

                if (DiagnosticsButton != null)
                    DiagnosticsButton.Visibility = Visibility.Visible;

                if (DetectorsButton != null)
                    DetectorsButton.Visibility = Visibility.Visible;

                if (AnalyzeButton != null)
                    AnalyzeButton.ToolTip = "Open pixel/image debug tools.";

                if (ForwardScanButton != null)
                    ForwardScanButton.ToolTip = "F5";

                if (StopButton != null)
                    StopButton.ToolTip = "Space";

                if (BackwardScanButton != null)
                    BackwardScanButton.ToolTip = "F6";

                if (ResetButton != null)
                    ResetButton.ToolTip = "Ctrl+R";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pro UI upgrade failed: {ex.Message}");
            }
        }

        private void InstallKeyboardShortcuts()
        {
            PreviewKeyDown -= MainWindow_ProPreviewKeyDown;
            PreviewKeyDown += MainWindow_ProPreviewKeyDown;
        }

        private void MainWindow_ProPreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (e.Key == Key.F5)
            {
                ForwardScan_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F6)
            {
                BackwardScan_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Space)
            {
                StopScan_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.R)
            {
                ResetScan_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.S)
            {
                SaveSnapshotAndMaybeReport();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.G)
            {
                if (_gpuAccelerationCheckBox != null)
                    _gpuAccelerationCheckBox.IsChecked = _gpuAccelerationCheckBox.IsChecked != true;
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key >= Key.D1 && e.Key <= Key.D8)
            {
                int index = e.Key - Key.D1;
                ApplyPresetByIndex(index);
                e.Handled = true;
            }
        }

        private void ApplyPersistedProSettings()
        {
            if (_renderQualityComboBox != null)
                _renderQualityComboBox.SelectedItem = ConfigManager.Current.DisplaySettings.RenderQualityMode;

            string presetName = ConfigManager.Current.DisplaySettings.LastOperatorPreset;
            ProPreset? preset = _proPresets.FirstOrDefault(item => item.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
            if (_operatorPresetComboBox != null)
                _operatorPresetComboBox.SelectedItem = preset ?? _proPresets[0];

            ApplyRenderQuality(ConfigManager.Current.DisplaySettings.RenderQualityMode, save: false);
        }

        private void OperatorPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_operatorPresetComboBox?.SelectedItem is ProPreset preset)
                ApplyPreset(preset, save: true);
        }

        private void ApplyPresetByIndex(int index)
        {
            if (index < 0 || index >= _proPresets.Length)
                return;

            if (_operatorPresetComboBox != null)
                _operatorPresetComboBox.SelectedItem = _proPresets[index];
            else
                ApplyPreset(_proPresets[index], save: true);
        }

        private void ApplyPreset(ProPreset preset, bool save)
        {
            if (OperatorFilterSlider != null) OperatorFilterSlider.Value = preset.Strength;
            if (BrightnessSlider != null) BrightnessSlider.Value = preset.Brightness;
            if (ContrastSlider != null) ContrastSlider.Value = preset.Contrast;
            if (MaterialFilterCheck != null) MaterialFilterCheck.IsChecked = preset.MaterialEnhancement;
            if (EdgeFilterCheck != null) EdgeFilterCheck.IsChecked = preset.EdgeDetection;
            if (NoiseFilterCheck != null) NoiseFilterCheck.IsChecked = preset.NoiseReduction;
            if (SensitivitySlider != null) SensitivitySlider.Value = preset.Sensitivity;

            SetOperatorFilterMode(preset.Mode);
            Filter_Changed(this, new RoutedEventArgs());

            if (save)
            {
                ConfigManager.Current.DisplaySettings.LastOperatorPreset = preset.Name;
                ConfigManager.Current.FilterSettings.ActivePreset = preset.Name;
                ConfigManager.Save();
            }

            UpdateStatus($"Preset applied: {preset.Name}");
        }

        private void RenderQualityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_renderQualityComboBox?.SelectedItem is string mode)
                ApplyRenderQuality(mode, save: true);
        }

        private void ApplyRenderQuality(string mode, bool save)
        {
            int scanMs = mode switch
            {
                "High FPS" => 8,
                "Quality" => 33,
                _ => 16
            };

            int uiMs = mode switch
            {
                "High FPS" => 250,
                "Quality" => 750,
                _ => 500
            };

            if (_scanTimer != null)
                _scanTimer.Interval = TimeSpan.FromMilliseconds(scanMs);

            if (_uiTimer != null)
                _uiTimer.Interval = TimeSpan.FromMilliseconds(uiMs);

            if (save)
            {
                ConfigManager.Current.DisplaySettings.RenderQualityMode = mode;
                ConfigManager.Save();
                UpdateStatus($"Render quality: {mode}");
            }
        }

        private void SetDashboardVisibility(bool visible)
        {
            ConfigManager.Current.DisplaySettings.ShowProDashboard = visible;
            ConfigManager.Save();

            if (_proDashboardText != null)
                _proDashboardText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (_threatSummaryText != null)
                _threatSummaryText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetAutoReport(bool enabled)
        {
            ConfigManager.Current.DisplaySettings.AutoSaveAnalysisReport = enabled;
            ConfigManager.Save();
        }

        private void StartProStatusTimer()
        {
            _proStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _proStatusTimer.Tick += (_, _) => UpdateProStatusPanel();
            _proStatusTimer.Start();
        }

        private void UpdateProStatusPanel()
        {
            UpdateGpuBackendStatus();

            if (_proDashboardText == null && _threatSummaryText == null)
                return;

            var scan = _scanService?.GetCurrentScan();
            if (scan == null)
                return;

            var stats = AnalyzeCurrentScan(scan.MaterialMap, scan.DensityMap);
            string backend = _imageProcessor?.LastRenderBackend ?? "CPU";
            string gpu = _imageProcessor?.IsGpuAvailable == true ? "available" : "not available";

            if (_proDashboardText != null)
            {
                _proDashboardText.Text =
                    $"Backend : {backend}\n" +
                    $"GPU     : {gpu}\n" +
                    $"Speed   : {SpeedSlider?.Value.ToString("0.0", CultureInfo.InvariantCulture) ?? "--"}x\n" +
                    $"Frame   : {_frameCount}\n" +
                    $"Objects : {scan.ObjectCount}\n" +
                    $"Pixels  : {stats.NonAirPixels:N0}\n" +
                    $"Dense   : {stats.DensePixels:N0}\n" +
                    $"Organic : {stats.OrganicPixels:N0}\n" +
                    $"Metal   : {stats.MetalPixels:N0}\n" +
                    $"Memory  : {GC.GetTotalMemory(false) / 1024 / 1024} MB";
            }

            if (_threatSummaryText != null)
            {
                _threatSummaryText.Text = CreateRiskSummary(stats);
                _threatSummaryText.Foreground = stats.RiskScore switch
                {
                    >= 70 => new SolidColorBrush(Color.FromRgb(239, 111, 108)),
                    >= 35 => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                    _ => new SolidColorBrush(Color.FromRgb(55, 211, 181))
                };
            }

            if (_hotkeyHintText != null)
                _hotkeyHintText.Visibility = ConfigManager.Current.DisplaySettings.ShowHotkeyHints ? Visibility.Visible : Visibility.Collapsed;
        }

        private static ScanStats AnalyzeCurrentScan(MaterialType[,] materialMap, double[,] densityMap)
        {
            int width = materialMap.GetLength(0);
            int height = materialMap.GetLength(1);
            var byMaterial = new Dictionary<MaterialType, int>();
            var stats = new ScanStats();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    MaterialType material = materialMap[x, y];
                    double density = densityMap[x, y];

                    if (material != MaterialType.Air && material != MaterialType.Unknown)
                    {
                        stats.NonAirPixels++;
                        byMaterial.TryGetValue(material, out int count);
                        byMaterial[material] = count + 1;
                    }

                    if (density > 0.82)
                        stats.DensePixels++;

                    if (IsOrganic(material))
                        stats.OrganicPixels++;

                    if (IsMetal(material) || material == MaterialType.Electronics)
                        stats.MetalPixels++;

                    if ((IsOrganic(material) && density > 0.55) || ((IsMetal(material) || material == MaterialType.Electronics) && density > 0.62))
                        stats.SuspectPixels++;
                }
            }

            stats.ByMaterial = byMaterial;
            stats.RiskScore = CalculateRiskScore(stats, width * height);
            return stats;
        }

        private static int CalculateRiskScore(ScanStats stats, int totalPixels)
        {
            if (totalPixels <= 0)
                return 0;

            double suspectRatio = stats.SuspectPixels / (double)totalPixels;
            double denseRatio = stats.DensePixels / (double)totalPixels;
            double metalRatio = stats.MetalPixels / (double)totalPixels;
            double score = suspectRatio * 820 + denseRatio * 260 + metalRatio * 80;
            return (int)Math.Clamp(score, 0, 100);
        }

        private static string CreateRiskSummary(ScanStats stats)
        {
            string level = stats.RiskScore switch
            {
                >= 70 => "HIGH",
                >= 35 => "MEDIUM",
                _ => "NORMAL"
            };

            return $"Risk: {level} ({stats.RiskScore}/100) • suspect pixels: {stats.SuspectPixels:N0}";
        }

        private static bool IsOrganic(MaterialType material)
        {
            return material is MaterialType.Organic or MaterialType.Plastic or MaterialType.Liquid or MaterialType.Sugar;
        }

        private static bool IsMetal(MaterialType material)
        {
            return material is MaterialType.Aluminum or MaterialType.LightMetal or MaterialType.Iron or MaterialType.HeavyMetal or MaterialType.Gold or MaterialType.Lead;
        }

        private void GpuAccelerationChanged(object sender, RoutedEventArgs e)
        {
            bool enabled = _gpuAccelerationCheckBox?.IsChecked == true;

            ConfigManager.Current.DisplaySettings.UseGpuAcceleration = enabled;
            ConfigManager.Save();

            if (_imageProcessor != null)
                _imageProcessor.UseGpuAcceleration = enabled;

            if (_scanService != null)
                UpdateFilteredView(_scanService.GetCurrentScan());

            UpdateGpuBackendStatus();
            UpdateStatus(enabled ? "GPU acceleration enabled" : "GPU acceleration disabled");
        }

        private void UpdateGpuBackendStatus()
        {
            if (_gpuBackendText == null)
                return;

            _gpuBackendText.Text = CreateGpuBackendStatusText();
        }

        private string CreateGpuBackendStatusText()
        {
            if (_imageProcessor == null)
                return "Render backend: CPU";

            bool requested = ConfigManager.Current.DisplaySettings.UseGpuAcceleration;
            string requestedText = requested ? "ON" : "OFF";
            string availableText = _imageProcessor.IsGpuAvailable ? "available" : "not available";
            string statusText = string.IsNullOrWhiteSpace(_imageProcessor.GpuStatus) ? availableText : _imageProcessor.GpuStatus;

            return $"Render backend: {_imageProcessor.LastRenderBackend}\nGPU: {requestedText}, {statusText}";
        }

        private void AutoTune_Click(object sender, RoutedEventArgs e)
        {
            var scan = _scanService?.GetCurrentScan();
            if (scan == null)
                return;

            double average = 0;
            double max = 0;
            int count = 0;
            int width = scan.DensityMap.GetLength(0);
            int height = scan.DensityMap.GetLength(1);

            for (int x = 0; x < width; x += 2)
            {
                for (int y = 0; y < height; y += 2)
                {
                    double density = scan.DensityMap[x, y];
                    if (density <= 0.02)
                        continue;

                    average += density;
                    max = Math.Max(max, density);
                    count++;
                }
            }

            average = count > 0 ? average / count : 0.3;

            if (BrightnessSlider != null)
                BrightnessSlider.Value = Math.Clamp(1.18 - average * 0.18, 0.75, 1.35);

            if (ContrastSlider != null)
                ContrastSlider.Value = Math.Clamp(1.08 + max * 0.28, 1.0, 1.75);

            if (OperatorFilterSlider != null)
                OperatorFilterSlider.Value = Math.Clamp(1.0 + max * 0.22, 0.9, 1.65);

            if (MaterialFilterCheck != null)
                MaterialFilterCheck.IsChecked = true;

            Filter_Changed(this, new RoutedEventArgs());
            UpdateStatus("Auto tune applied");
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            ApplyPresetByIndex(0);
        }

        private void SaveSnapshot_Click(object sender, RoutedEventArgs e)
        {
            SaveSnapshotAndMaybeReport();
        }

        private void SaveAnalysisReport_Click(object sender, RoutedEventArgs e)
        {
            string path = SaveAnalysisReport();
            UpdateStatus(string.IsNullOrWhiteSpace(path) ? "Analysis report failed" : $"Analysis report saved: {path}");
        }

        private void SuspectView_Click(object sender, RoutedEventArgs e)
        {
            SetOperatorFilterMode(OperatorFilterMode.SuspectHighlight);
            if (OperatorFilterSlider != null) OperatorFilterSlider.Value = 1.45;
            if (MaterialFilterCheck != null) MaterialFilterCheck.IsChecked = true;
            if (EdgeFilterCheck != null) EdgeFilterCheck.IsChecked = true;
            Filter_Changed(this, new RoutedEventArgs());
        }

        private void MetalView_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(_proPresets.First(item => item.Name == "Metal Search"), save: true);
        }

        private void OrganicView_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(_proPresets.First(item => item.Name == "Organic Threat"), save: true);
        }

        private void PenetrationView_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreset(_proPresets.First(item => item.Name == "Maximum Penetration"), save: true);
        }

        private void SaveSnapshotAndMaybeReport()
        {
            string snapshotPath = SavePngSnapshot();
            string reportPath = string.Empty;

            if (ConfigManager.Current.DisplaySettings.AutoSaveAnalysisReport)
                reportPath = SaveAnalysisReport();

            if (!string.IsNullOrWhiteSpace(snapshotPath) && !string.IsNullOrWhiteSpace(reportPath))
                UpdateStatus($"Snapshot + report saved: {Path.GetFileName(snapshotPath)}");
            else if (!string.IsNullOrWhiteSpace(snapshotPath))
                UpdateStatus($"Snapshot saved: {Path.GetFileName(snapshotPath)}");
            else
                UpdateStatus("Snapshot failed");
        }

        private string SavePngSnapshot()
        {
            try
            {
                if (_filteredBitmap == null)
                    return string.Empty;

                string directory = GetSnapshotDirectory();
                Directory.CreateDirectory(directory);

                string fileName = $"SEE_INSADE_snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string path = Path.Combine(directory, fileName);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_filteredBitmap));

                using var stream = File.Create(path);
                encoder.Save(stream);
                return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Snapshot save failed: {ex.Message}");
                return string.Empty;
            }
        }

        private string SaveAnalysisReport()
        {
            try
            {
                var scan = _scanService?.GetCurrentScan();
                if (scan == null)
                    return string.Empty;

                string directory = GetSnapshotDirectory();
                Directory.CreateDirectory(directory);

                var stats = AnalyzeCurrentScan(scan.MaterialMap, scan.DensityMap);
                string fileName = $"SEE_INSADE_analysis_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string path = Path.Combine(directory, fileName);

                var builder = new StringBuilder();
                builder.AppendLine("SEE INSADE scan analysis");
                builder.AppendLine($"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                builder.AppendLine($"Backend: {_imageProcessor?.LastRenderBackend ?? "CPU"}");
                builder.AppendLine($"GPU available: {_imageProcessor?.IsGpuAvailable}");
                builder.AppendLine($"Preset: {ConfigManager.Current.DisplaySettings.LastOperatorPreset}");
                builder.AppendLine($"Render quality: {ConfigManager.Current.DisplaySettings.RenderQualityMode}");
                builder.AppendLine($"Objects: {scan.ObjectCount}");
                builder.AppendLine($"Non-air pixels: {stats.NonAirPixels}");
                builder.AppendLine($"Dense pixels: {stats.DensePixels}");
                builder.AppendLine($"Organic pixels: {stats.OrganicPixels}");
                builder.AppendLine($"Metal pixels: {stats.MetalPixels}");
                builder.AppendLine($"Suspect pixels: {stats.SuspectPixels}");
                builder.AppendLine($"Risk score: {stats.RiskScore}/100");
                builder.AppendLine();
                builder.AppendLine("Material distribution:");

                foreach (var pair in stats.ByMaterial.OrderByDescending(pair => pair.Value))
                    builder.AppendLine($"- {pair.Key}: {pair.Value}");

                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
                return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Analysis report save failed: {ex.Message}");
                return string.Empty;
            }
        }

        private static string GetSnapshotDirectory()
        {
            string configured = ConfigManager.Current.DisplaySettings.SnapshotDirectory;
            if (string.IsNullOrWhiteSpace(configured))
                configured = "Scans";

            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(Environment.CurrentDirectory, configured);
        }

        private sealed class ProPreset
        {
            public ProPreset(
                string name,
                OperatorFilterMode mode,
                double strength,
                double brightness,
                double contrast,
                bool materialEnhancement,
                bool edgeDetection,
                bool noiseReduction,
                double sensitivity)
            {
                Name = name;
                Mode = mode;
                Strength = strength;
                Brightness = brightness;
                Contrast = contrast;
                MaterialEnhancement = materialEnhancement;
                EdgeDetection = edgeDetection;
                NoiseReduction = noiseReduction;
                Sensitivity = sensitivity;
            }

            public string Name { get; }
            public OperatorFilterMode Mode { get; }
            public double Strength { get; }
            public double Brightness { get; }
            public double Contrast { get; }
            public bool MaterialEnhancement { get; }
            public bool EdgeDetection { get; }
            public bool NoiseReduction { get; }
            public double Sensitivity { get; }
        }

        private sealed class ScanStats
        {
            public int NonAirPixels { get; set; }
            public int DensePixels { get; set; }
            public int OrganicPixels { get; set; }
            public int MetalPixels { get; set; }
            public int SuspectPixels { get; set; }
            public int RiskScore { get; set; }
            public Dictionary<MaterialType, int> ByMaterial { get; set; } = new();
        }
    }
}
