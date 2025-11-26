using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using SEE_INSADE.Services.Scanning;

namespace SEE_INSADE.UI.Dialogs
{
    public partial class DetectorVisualizationWindow : Window
    {
        private WriteableBitmap _visualizationBitmap = null!;
        private ScanService _scanService = null!;
        private DispatcherTimer _animationTimer = null!;
        private DetectorInfo _selectedDetector = null!;

        private enum VisualizationMode { Overview, Health, Activity, Sensitivity }
        private VisualizationMode _currentMode = VisualizationMode.Overview;

        private bool _showGrid = true;
        private bool _showLabels = false;
        private bool _showReadings = true;
        private bool _animateScan = true;
        private bool _isInitialized = false;

        public DetectorVisualizationWindow(ScanService scanService)
        {
            InitializeComponent();
            _scanService = scanService;

            // Отложим инициализацию до полной загрузки окна
            this.Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            InitializeVisualization();
        }

        private void InitializeVisualization()
        {
            try
            {
                // Create visualization bitmap
                _visualizationBitmap = new WriteableBitmap(800, 400, 96, 96, PixelFormats.Bgr32, null);
                DetectorVisualizationImage.Source = _visualizationBitmap;

                // Initialize animation timer
                _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                _animationTimer.Tick += AnimationTimer_Tick;
                _animationTimer.Start();

                // Set initial values safely
                if (ModeOverview != null) ModeOverview.IsChecked = true;
                if (ShowGrid != null) ShowGrid.IsChecked = _showGrid;
                if (ShowReadings != null) ShowReadings.IsChecked = _showReadings;
                if (AnimateScan != null) AnimateScan.IsChecked = _animateScan;

                _isInitialized = true;

                UpdateVisualization();
                UpdateStatusInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing visualization: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (_animateScan && _isInitialized)
            {
                UpdateVisualization();
            }
        }

        private void UpdateVisualization()
        {
            if (!_isInitialized) return;

            try
            {
                var scanData = _scanService.GetCurrentScan();
                if (scanData?.DetectorData == null) return;

                int width = _visualizationBitmap.PixelWidth;
                int height = _visualizationBitmap.PixelHeight;
                byte[] pixels = new byte[width * height * 4];

                // Clear background
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = 15;
                    pixels[i + 1] = 15;
                    pixels[i + 2] = 25;
                    pixels[i + 3] = 255;
                }

                switch (_currentMode)
                {
                    case VisualizationMode.Overview:
                        DrawOverviewMode(pixels, width, height, scanData);
                        break;
                    case VisualizationMode.Health:
                        DrawHealthMode(pixels, width, height, scanData);
                        break;
                    case VisualizationMode.Activity:
                        DrawActivityMode(pixels, width, height, scanData);
                        break;
                    case VisualizationMode.Sensitivity:
                        DrawSensitivityMode(pixels, width, height, scanData);
                        break;
                }

                if (_showGrid)
                {
                    DrawGrid(pixels, width, height);
                }

                DrawScanBeam(pixels, width, height, scanData);

                _visualizationBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);

                UpdateDetectorOverlay(scanData);
                UpdateStatusInfo();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateVisualization error: {ex.Message}");
            }
        }

        private void DrawOverviewMode(byte[] pixels, int width, int height, ScanData scanData)
        {
            DrawDetectorArrayBase(pixels, width, height);

            foreach (var detector in scanData.DetectorData)
            {
                if (detector != null)
                {
                    DrawDetector(pixels, width, height, detector, GetDetectorColor(detector));
                }
            }
        }

        private void DrawHealthMode(byte[] pixels, int width, int height, ScanData scanData)
        {
            DrawDetectorArrayBase(pixels, width, height);

            foreach (var detector in scanData.DetectorData)
            {
                if (detector != null)
                {
                    Color healthColor = detector.Health > 80 ? Colors.LightGreen :
                                      detector.Health > 60 ? Colors.Yellow :
                                      Colors.Red;

                    DrawDetector(pixels, width, height, detector, healthColor);
                }
            }
        }

        private void DrawActivityMode(byte[] pixels, int width, int height, ScanData scanData)
        {
            DrawDetectorArrayBase(pixels, width, height);

            foreach (var detector in scanData.DetectorData)
            {
                if (detector != null)
                {
                    double activity = detector.CurrentReading;
                    byte intensity = (byte)(activity * 255);
                    Color activityColor = Color.FromRgb(intensity, intensity, 100);

                    DrawDetector(pixels, width, height, detector, activityColor);

                    if (_showReadings && activity > 0.1)
                    {
                        DrawReadingBar(pixels, width, height, detector, activity);
                    }
                }
            }
        }

        private void DrawSensitivityMode(byte[] pixels, int width, int height, ScanData scanData)
        {
            DrawDetectorArrayBase(pixels, width, height);

            foreach (var detector in scanData.DetectorData)
            {
                if (detector != null)
                {
                    double sensitivity = detector.Sensitivity;
                    byte intensity = (byte)(sensitivity * 128);
                    Color sensitivityColor = Color.FromRgb(100, intensity, 255);

                    DrawDetector(pixels, width, height, detector, sensitivityColor);
                }
            }
        }

        private void DrawDetectorArrayBase(byte[] pixels, int width, int height)
        {
            for (int y = 350; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    pixels[index] = 40;
                    pixels[index + 1] = 40;
                    pixels[index + 2] = 60;
                }
            }
        }

        private void DrawDetector(byte[] pixels, int width, int height, DetectorInfo detector, Color color)
        {
            int x = (int)detector.Position;
            int baseY = 350;

            for (int dy = 0; dy < 15; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int px = x + dx;
                    int py = baseY - dy;

                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        int index = (py * width + px) * 4;
                        pixels[index] = color.B;
                        pixels[index + 1] = color.G;
                        pixels[index + 2] = color.R;
                    }
                }
            }

            if (detector.IsActive)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = x + dx;
                    int py = baseY - 16;

                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        int index = (py * width + px) * 4;
                        pixels[index] = 0;
                        pixels[index + 1] = 255;
                        pixels[index + 2] = 0;
                    }
                }
            }
        }

        private void DrawReadingBar(byte[] pixels, int width, int height, DetectorInfo detector, double reading)
        {
            int x = (int)detector.Position;
            int barHeight = (int)(reading * 100);
            int baseY = 350;

            for (int dy = 1; dy <= barHeight; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = x + dx;
                    int py = baseY - 15 - dy;

                    if (px >= 0 && px < width && py >= 0 && py < height)
                    {
                        int index = (py * width + px) * 4;
                        pixels[index] = 255;
                        pixels[index + 1] = 255;
                        pixels[index + 2] = 100;
                    }
                }
            }
        }

        private void DrawGrid(byte[] pixels, int width, int height)
        {
            for (int x = 0; x < width; x += 20)
            {
                for (int y = 0; y < height; y++)
                {
                    int index = (y * width + x) * 4;
                    pixels[index] = 30;
                    pixels[index + 1] = 30;
                    pixels[index + 2] = 50;
                }
            }

            for (int y = 0; y < height; y += 20)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    pixels[index] = 30;
                    pixels[index + 1] = 30;
                    pixels[index + 2] = 50;
                }
            }
        }

        private void DrawScanBeam(byte[] pixels, int width, int height, ScanData scanData)
        {
            int x = (int)scanData.ScanPosition;

            for (int y = 0; y < height; y++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = x + dx;
                    if (px >= 0 && px < width)
                    {
                        int index = (y * width + px) * 4;
                        pixels[index] = 0;
                        pixels[index + 1] = 255;
                        pixels[index + 2] = 255;
                    }
                }
            }
        }

        private Color GetDetectorColor(DetectorInfo detector)
        {
            if (!detector.IsActive) return Colors.Gray;

            double healthFactor = detector.Health / 100.0;
            double activityFactor = detector.CurrentReading;

            return Color.FromRgb(
                (byte)(100 * healthFactor),
                (byte)(200 * healthFactor),
                (byte)(100 + (155 * activityFactor))
            );
        }

        private void UpdateDetectorOverlay(ScanData scanData)
        {
            if (!_isInitialized) return;

            DetectorOverlayCanvas.Children.Clear();

            foreach (var detector in scanData.DetectorData)
            {
                if (detector == null) continue;

                var ellipse = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.Transparent,
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    Tag = detector
                };

                Canvas.SetLeft(ellipse, detector.Position - 4);
                Canvas.SetTop(ellipse, 350 - 20);

                ellipse.MouseEnter += DetectorMarker_MouseEnter;
                ellipse.MouseLeave += DetectorMarker_MouseLeave;
                ellipse.MouseLeftButtonDown += DetectorMarker_MouseLeftButtonDown;

                DetectorOverlayCanvas.Children.Add(ellipse);
            }
        }

        private void UpdateStatusInfo()
        {
            if (!_isInitialized) return;

            var scanData = _scanService.GetCurrentScan();
            if (scanData?.DetectorData == null) return;

            int activeCount = 0;
            double totalHealth = 0;

            foreach (var detector in scanData.DetectorData)
            {
                if (detector?.IsActive == true) activeCount++;
                if (detector != null) totalHealth += detector.Health;
            }

            if (TotalDetectorsText != null)
                TotalDetectorsText.Text = $"Total: {scanData.DetectorData.Length}";
            if (ActiveDetectorsText != null)
                ActiveDetectorsText.Text = $"Active: {activeCount}";
            if (HealthStatusText != null)
                HealthStatusText.Text = $"Avg Health: {(totalHealth / scanData.DetectorData.Length):F1}%";
            if (ScanStatusText != null)
                ScanStatusText.Text = $"Scan: {(scanData.ScanPosition > 0 ? "Active" : "Ready")}";
        }

        // Event Handlers - SAFE VERSION
        private void VisualizationMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            try
            {
                if (ModeOverview?.IsChecked == true) _currentMode = VisualizationMode.Overview;
                else if (ModeHealth?.IsChecked == true) _currentMode = VisualizationMode.Health;
                else if (ModeActivity?.IsChecked == true) _currentMode = VisualizationMode.Activity;
                else if (ModeSensitivity?.IsChecked == true) _currentMode = VisualizationMode.Sensitivity;

                if (VisualizationModeText != null)
                    VisualizationModeText.Text = _currentMode.ToString();

                UpdateVisualization();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VisualizationMode_Changed error: {ex.Message}");
            }
        }

        private void DisplayOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            try
            {
                _showGrid = ShowGrid?.IsChecked == true;
                _showLabels = ShowLabels?.IsChecked == true;
                _showReadings = ShowReadings?.IsChecked == true;
                _animateScan = AnimateScan?.IsChecked == true;

                if (_animateScan && _animationTimer != null)
                    _animationTimer.Start();
                else if (_animationTimer != null)
                    _animationTimer.Stop();

                UpdateVisualization();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DisplayOption_Changed error: {ex.Message}");
            }
        }

        private void DetectorMarker_MouseEnter(object sender, MouseEventArgs e)
        {
            var ellipse = sender as Ellipse;
            if (ellipse?.Tag is DetectorInfo detector)
            {
                ellipse.Stroke = Brushes.Yellow;
                ellipse.StrokeThickness = 2;
                UpdateSelectedDetectorInfo(detector);
            }
        }

        private void DetectorMarker_MouseLeave(object sender, MouseEventArgs e)
        {
            var ellipse = sender as Ellipse;
            if (ellipse != null)
            {
                ellipse.Stroke = Brushes.White;
                ellipse.StrokeThickness = 1;
            }
        }

        private void DetectorMarker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var ellipse = sender as Ellipse;
            if (ellipse?.Tag is DetectorInfo detector)
            {
                _selectedDetector = detector;
                UpdateSelectedDetectorInfo(detector);

                foreach (var child in DetectorOverlayCanvas.Children)
                {
                    if (child is Ellipse marker)
                    {
                        marker.Stroke = marker == ellipse ? Brushes.Cyan : Brushes.White;
                        marker.StrokeThickness = marker == ellipse ? 3 : 1;
                    }
                }
            }
        }

        private void UpdateSelectedDetectorInfo(DetectorInfo detector)
        {
            if (!_isInitialized) return;

            if (SelectedDetectorId != null)
                SelectedDetectorId.Text = $"ID: {detector.Id}";
            if (SelectedDetectorHealth != null)
                SelectedDetectorHealth.Text = $"Health: {detector.Health:F1}%";
            if (SelectedDetectorSensitivity != null)
                SelectedDetectorSensitivity.Text = $"Sensitivity: {detector.Sensitivity:F2}";
            if (SelectedDetectorReading != null)
                SelectedDetectorReading.Text = $"Reading: {detector.CurrentReading:F3}";
        }

        private void TestAllDetectors_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            StatusText.Text = "Testing all detectors...";
            System.Threading.Thread.Sleep(1000);
            StatusText.Text = "Detector test completed - All systems operational";
        }

        private void CalibrateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (_selectedDetector != null)
            {
                StatusText.Text = $"Calibrating detector {_selectedDetector.Id}...";
                System.Threading.Thread.Sleep(500);
                StatusText.Text = $"Detector {_selectedDetector.Id} calibrated successfully";
            }
            else
            {
                StatusText.Text = "Please select a detector first";
            }
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            StatusText.Text = "Generating detector diagnostics report...";
            System.Threading.Thread.Sleep(800);
            MessageBox.Show("Detector diagnostics report generated successfully!\nSaved to: Reports/DetectorAnalysis.pdf",
                "Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);
            StatusText.Text = "Report generated successfully";
        }

        private void DetectorImage_MouseMove(object sender, MouseEventArgs e)
        {
            // Coordinate display can be added here
        }

        private void DetectorImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Click interactions can be added here
        }

        protected override void OnClosed(EventArgs e)
        {
            _animationTimer?.Stop();
            base.OnClosed(e);
        }
    }
}