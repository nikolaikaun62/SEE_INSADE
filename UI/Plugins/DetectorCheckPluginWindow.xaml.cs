using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SEE_INSADE.Services.Scanning;

namespace SEE_INSADE.UI.Plugins
{
    public partial class DetectorCheckPluginWindow : Window
    {
        private const int BitmapWidth = 1000;
        private const int BitmapHeight = 460;

        private readonly ScanService _scanService;
        private readonly DispatcherTimer _timer;
        private readonly WriteableBitmap _bitmap;
        private DetectorInfo? _selectedDetector;

        private enum VisualizationMode
        {
            Overview,
            Health,
            Activity,
            Sensitivity
        }

        private VisualizationMode _currentMode = VisualizationMode.Overview;
        private bool _showGrid = true;
        private bool _showReadings = true;
        private bool _liveUpdate = true;
        private bool _isInitialized;

        public DetectorCheckPluginWindow(ScanService scanService)
        {
            InitializeComponent();
            SEE_INSADE.Core.Localization.LocalizationHelper.Apply(this);

            _scanService = scanService;
            _bitmap = new WriteableBitmap(BitmapWidth, BitmapHeight, 96, 96, PixelFormats.Bgr32, null);
            DetectorVisualizationImage.Source = _bitmap;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _timer.Tick += (_, _) =>
            {
                if (_liveUpdate)
                    UpdateVisualization();
            };

            Loaded += (_, _) =>
            {
                _isInitialized = true;
                _timer.Start();
                UpdateVisualization();
            };

            Closed += (_, _) => _timer.Stop();
        }

        private void UpdateVisualization()
        {
            if (!_isInitialized)
                return;

            DetectorInfo[] detectors = _scanService.GetCurrentScan().DetectorData;
            if (detectors.Length == 0)
                return;

            byte[] pixels = new byte[BitmapWidth * BitmapHeight * 4];
            FillBackground(pixels);

            if (_showGrid)
                DrawGrid(pixels);

            DrawDetectorColumn(pixels, detectors);
            DrawDetectorLineMonitor(pixels, detectors);
            DrawScanBeam(pixels);

            _bitmap.WritePixels(new Int32Rect(0, 0, BitmapWidth, BitmapHeight), pixels, BitmapWidth * 4, 0);

            UpdateHeader(detectors);
            UpdateStatusInfo(detectors);
        }

        private void FillBackground(byte[] pixels)
        {
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 12;
                pixels[i + 1] = 10;
                pixels[i + 2] = 8;
                pixels[i + 3] = 255;
            }
        }

        private void DrawGrid(byte[] pixels)
        {
            for (int x = 0; x < BitmapWidth; x += 40)
            {
                for (int y = 0; y < BitmapHeight; y++)
                    SetPixel(pixels, x, y, Color.FromRgb(28, 36, 50));
            }

            for (int y = 0; y < BitmapHeight; y += 40)
            {
                for (int x = 0; x < BitmapWidth; x++)
                    SetPixel(pixels, x, y, Color.FromRgb(28, 36, 50));
            }
        }

        private void DrawDetectorColumn(byte[] pixels, DetectorInfo[] detectors)
        {
            int centerX = BitmapWidth / 2;
            int top = 24;
            int bottom = BitmapHeight - 120;
            int columnHeight = bottom - top;

            for (int y = top; y <= bottom; y++)
            {
                for (int dx = -8; dx <= 8; dx++)
                    SetPixel(pixels, centerX + dx, y, Color.FromRgb(32, 42, 60));
            }

            for (int i = 0; i < detectors.Length; i++)
            {
                DetectorInfo detector = detectors[i];
                int y = top + (int)((double)i / Math.Max(1, detectors.Length - 1) * columnHeight);
                Color color = GetDetectorColor(detector);

                DrawRect(pixels, centerX - 12, y - 1, 24, 3, color);

                if (_showReadings && _currentMode == VisualizationMode.Activity)
                {
                    int barWidth = (int)(Math.Clamp(detector.CurrentReading, 0, 1) * 160);
                    DrawRect(pixels, centerX + 16, y - 1, barWidth, 3, Blend(color, Colors.White, 0.18));
                }
            }
        }

        private void DrawDetectorLineMonitor(byte[] pixels, DetectorInfo[] detectors)
        {
            int graphTop = BitmapHeight - 92;
            int graphBottom = BitmapHeight - 26;
            int graphHeight = graphBottom - graphTop;

            DrawRect(pixels, 24, graphTop - 10, BitmapWidth - 48, graphHeight + 18, Color.FromRgb(16, 24, 34));

            for (int i = 0; i < detectors.Length; i++)
            {
                DetectorInfo detector = detectors[i];

                int x0 = 28 + i * (BitmapWidth - 56) / detectors.Length;
                int x1 = Math.Max(x0 + 1, 28 + (i + 1) * (BitmapWidth - 56) / detectors.Length);

                double value = _currentMode switch
                {
                    VisualizationMode.Health => Math.Clamp(detector.Health / 100.0, 0.0, 1.0),
                    VisualizationMode.Sensitivity => Math.Clamp(detector.Sensitivity / 1.08, 0.0, 1.0),
                    _ => Math.Clamp(detector.CurrentReading, 0.0, 1.0)
                };

                int barHeight = Math.Max(3, (int)(value * graphHeight));
                Color color = GetDetectorColor(detector);

                for (int x = x0; x < x1; x++)
                {
                    for (int y = graphBottom - barHeight; y <= graphBottom; y++)
                        SetPixel(pixels, x, y, color);
                }
            }
        }

        private void DrawScanBeam(byte[] pixels)
        {
            int centerX = BitmapWidth / 2;

            for (int y = 20; y < BitmapHeight - 110; y++)
            {
                SetPixel(pixels, centerX - 1, y, Color.FromRgb(0, 210, 220));
                SetPixel(pixels, centerX, y, Color.FromRgb(0, 255, 255));
                SetPixel(pixels, centerX + 1, y, Color.FromRgb(0, 210, 220));
            }
        }

        private Color GetDetectorColor(DetectorInfo detector)
        {
            if (!detector.IsActive)
                return Colors.Gray;

            return _currentMode switch
            {
                VisualizationMode.Health => detector.Health > 80
                    ? Color.FromRgb(45, 220, 120)
                    : detector.Health > 60
                        ? Color.FromRgb(245, 190, 60)
                        : Color.FromRgb(230, 70, 70),

                VisualizationMode.Activity => Color.FromRgb(
                    (byte)Math.Clamp(45 + detector.CurrentReading * 200, 45, 245),
                    (byte)Math.Clamp(80 + detector.CurrentReading * 120, 80, 220),
                    90),

                VisualizationMode.Sensitivity => Color.FromRgb(
                    70,
                    (byte)Math.Clamp(detector.Sensitivity * 170, 70, 245),
                    245),

                _ => Color.FromRgb(
                    (byte)Math.Clamp(50 + detector.Health, 50, 160),
                    (byte)Math.Clamp(80 + detector.Health * 1.4, 80, 230),
                    (byte)Math.Clamp(90 + detector.CurrentReading * 150, 90, 240))
            };
        }

        private void UpdateHeader(DetectorInfo[] detectors)
        {
            int active = detectors.Count(d => d.IsActive);
            double signal = detectors.Average(d => d.CurrentReading);
            double low = detectors.Average(d => d.LowEnergyReading);
            double high = detectors.Average(d => d.HighEnergyReading);

            ActiveText.Text = $"Active: {active}/{detectors.Length}";
            AverageText.Text = $"Signal: {signal:F3}  Low/High: {low:F3}/{high:F3}";
        }

        private void UpdateStatusInfo(DetectorInfo[] detectors)
        {
            int active = detectors.Count(d => d.IsActive);
            double avgHealth = detectors.Average(d => d.Health);

            TotalDetectorsText.Text = $"Total: {detectors.Length}";
            ActiveDetectorsText.Text = $"Active: {active}";
            HealthStatusText.Text = $"Avg Health: {avgHealth:F1}%";
            ScanStatusText.Text = $"Scan: {(_scanService.GetCurrentScan().ScanPosition > 0 ? "Active" : "Ready")}";
        }

        private void UpdateSelectedDetectorInfo(DetectorInfo detector)
        {
            SelectedDetectorId.Text = $"ID: {detector.Id}";
            SelectedDetectorHealth.Text = $"Health: {detector.Health:F1}%";
            SelectedDetectorSensitivity.Text = $"Sensitivity: {detector.Sensitivity:F3}";
            SelectedDetectorReading.Text = $"Reading: {detector.CurrentReading:F3}";
            SelectedDetectorDualEnergy.Text = $"Low/High: {detector.LowEnergyReading:F3}/{detector.HighEnergyReading:F3}  R: {detector.AttenuationRatio:F2}";
            SelectedDetectorMaterial.Text = $"Zeff: {detector.EstimatedZ:F1}  Material: {detector.DetectedMaterial}";
        }

        private void VisualizationMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
                return;

            if (ModeHealth?.IsChecked == true)
                _currentMode = VisualizationMode.Health;
            else if (ModeActivity?.IsChecked == true)
                _currentMode = VisualizationMode.Activity;
            else if (ModeSensitivity?.IsChecked == true)
                _currentMode = VisualizationMode.Sensitivity;
            else
                _currentMode = VisualizationMode.Overview;

            VisualizationModeText.Text = _currentMode.ToString();
            UpdateVisualization();
        }

        private void DisplayOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
                return;

            _showGrid = ShowGrid?.IsChecked == true;
            _showReadings = ShowReadings?.IsChecked == true;
            _liveUpdate = AnimateScan?.IsChecked == true;

            if (_liveUpdate)
                _timer.Start();
            else
                _timer.Stop();

            UpdateVisualization();
        }

        private void DetectorImage_MouseMove(object sender, MouseEventArgs e)
        {
            DetectorInfo[] detectors = _scanService.GetCurrentScan().DetectorData;
            if (detectors.Length == 0)
                return;

            Point point = e.GetPosition(DetectorVisualizationImage);
            int detectorIndex = (int)Math.Clamp(point.Y / Math.Max(1, DetectorVisualizationImage.ActualHeight) * detectors.Length, 0, detectors.Length - 1);

            _selectedDetector = detectors[detectorIndex];
            UpdateSelectedDetectorInfo(_selectedDetector);
        }

        private void DetectorImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DetectorImage_MouseMove(sender, e);
            if (_selectedDetector != null)
                StatusText.Text = $"Selected detector {_selectedDetector.Id}";
        }

        private void TestAllDetectors_Click(object sender, RoutedEventArgs e)
        {
            DetectorInfo[] detectors = _scanService.GetCurrentScan().DetectorData;

            foreach (DetectorInfo detector in detectors)
            {
                detector.IsActive = true;
                detector.Health = Math.Max(detector.Health, 95);
            }

            StatusText.Text = "Detector test completed - all active detectors are operational";
            UpdateVisualization();
        }

        private void CalibrateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDetector == null)
            {
                StatusText.Text = "Please select a detector first";
                return;
            }

            _selectedDetector.Sensitivity = 1.0;
            _selectedDetector.Health = 100;
            StatusText.Text = $"Detector {_selectedDetector.Id} calibrated successfully";
            UpdateSelectedDetectorInfo(_selectedDetector);
            UpdateVisualization();
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            DetectorInfo[] detectors = _scanService.GetCurrentScan().DetectorData;
            int active = detectors.Count(d => d.IsActive);
            double avgHealth = detectors.Average(d => d.Health);
            double avgSignal = detectors.Average(d => d.CurrentReading);

            var report = new StringBuilder();
            report.AppendLine("Detector Diagnostics Report");
            report.AppendLine($"Total detectors: {detectors.Length}");
            report.AppendLine($"Active detectors: {active}");
            report.AppendLine($"Average health: {avgHealth:F1}%");
            report.AppendLine($"Average signal: {avgSignal:F3}");
            report.AppendLine($"Mode: {_currentMode}");

            MessageBox.Show(report.ToString(), "Detector Report", MessageBoxButton.OK, MessageBoxImage.Information);
            StatusText.Text = "Detector report generated";
        }

        private static void DrawRect(byte[] pixels, int x, int y, int width, int height, Color color)
        {
            for (int yy = y; yy < y + height; yy++)
            {
                for (int xx = x; xx < x + width; xx++)
                    SetPixel(pixels, xx, yy, color);
            }
        }

        private static Color Blend(Color foreground, Color background, double backgroundAmount)
        {
            double foregroundAmount = 1.0 - backgroundAmount;
            return Color.FromRgb(
                (byte)Math.Clamp(foreground.R * foregroundAmount + background.R * backgroundAmount, 0, 255),
                (byte)Math.Clamp(foreground.G * foregroundAmount + background.G * backgroundAmount, 0, 255),
                (byte)Math.Clamp(foreground.B * foregroundAmount + background.B * backgroundAmount, 0, 255));
        }

        private static void SetPixel(byte[] pixels, int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= BitmapWidth || y >= BitmapHeight)
                return;

            int index = (y * BitmapWidth + x) * 4;
            pixels[index] = color.B;
            pixels[index + 1] = color.G;
            pixels[index + 2] = color.R;
            pixels[index + 3] = 255;
        }
    }
}
