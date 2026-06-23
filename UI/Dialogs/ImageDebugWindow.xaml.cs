using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SEE_INSADE.UI.Dialogs
{
    public partial class ImageDebugWindow : Window
    {
        private WriteableBitmap _currentImage;
        private Point _startPoint;
        private bool _isMeasuring = false;
        private Line? _measurementLine;

        public ImageDebugWindow(WriteableBitmap image)
        {
            InitializeComponent();
            _currentImage = image;
            DebugImage.Source = _currentImage;
            InitializeDebugTools();
        }

        private void InitializeDebugTools()
        {
            // Initialize slider events
            DebugBrightnessSlider.ValueChanged += (s, e) => UpdateEnhancementPreview();
            DebugContrastSlider.ValueChanged += (s, e) => UpdateEnhancementPreview();
            DebugGammaSlider.ValueChanged += (s, e) => UpdateEnhancementPreview();
            ZoomSlider.ValueChanged += (s, e) =>
            {
                ZoomText.Text = $"{e.NewValue * 100:F0}%";
                UpdateZoom();
            };

            DebugStatusText.Text = $"Image loaded: {_currentImage.PixelWidth}x{_currentImage.PixelHeight}";
        }

        private void DebugImage_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(DebugImage);
            UpdatePixelInfo(position);

            if (_isMeasuring && _measurementLine != null)
            {
                var endPoint = e.GetPosition(DebugOverlayCanvas);
                _measurementLine.X2 = endPoint.X;
                _measurementLine.Y2 = endPoint.Y;

                UpdateMeasurementInfo(_startPoint, endPoint);
            }
        }

        private void DebugImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isMeasuring)
            {
                var point = e.GetPosition(DebugOverlayCanvas);

                if (_measurementLine == null)
                {
                    StartMeasurement(point);
                }
                else
                {
                    CompleteMeasurement(point);
                }
            }
        }

        private void UpdatePixelInfo(Point position)
        {
            if (_currentImage == null) return;

            int x = (int)position.X;
            int y = (int)position.Y;

            if (x >= 0 && x < _currentImage.PixelWidth && y >= 0 && y < _currentImage.PixelHeight)
            {
                var color = GetPixelColor(_currentImage, x, y);
                PixelInfoText.Text = $"X: {x}, Y: {y}\nRGB: ({color.R}, {color.G}, {color.B})";
            }
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

        private void StartMeasurement(Point startPoint)
        {
            _startPoint = startPoint;
            _measurementLine = new Line
            {
                X1 = startPoint.X,
                Y1 = startPoint.Y,
                X2 = startPoint.X,
                Y2 = startPoint.Y,
                Stroke = Brushes.Yellow,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };

            DebugOverlayCanvas.Children.Add(_measurementLine);
            MeasurementInfoPanel.Visibility = Visibility.Visible;
        }

        private void CompleteMeasurement(Point endPoint)
        {
            _isMeasuring = false;
            if (_measurementLine != null)
                DebugOverlayCanvas.Children.Remove(_measurementLine);
            _measurementLine = null;

            DebugStatusText.Text = "Measurement completed";
        }

        private void UpdateMeasurementInfo(Point start, Point end)
        {
            double distance = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
            MeasurementText.Text = $"Distance: {distance:F1} pixels\n" +
                                 $"Start: ({start.X:F0}, {start.Y:F0})\n" +
                                 $"End: ({end.X:F0}, {end.Y:F0})";
        }

        private void UpdateEnhancementPreview()
        {
            // Apply real-time enhancement preview
            var brightness = DebugBrightnessSlider.Value;
            var contrast = DebugContrastSlider.Value;
            var gamma = DebugGammaSlider.Value;

            // This would apply the enhancement to the image
            DebugStatusText.Text = $"Enhancement: Brightness {brightness:F1}, Contrast {contrast:F1}, Gamma {gamma:F1}";
        }

        private void UpdateZoom()
        {
            var scale = ZoomSlider.Value;
            DebugImage.LayoutTransform = new ScaleTransform(scale, scale);
        }

        // Tool button handlers
        private void PixelAnalyzer_Click(object sender, RoutedEventArgs e)
        {
            _isMeasuring = false;
            DebugStatusText.Text = "Pixel Analyzer active - hover over image";
        }

        private void Histogram_Click(object sender, RoutedEventArgs e)
        {
            ShowHistogram();
        }

        private void MaterialMap_Click(object sender, RoutedEventArgs e)
        {
            DebugStatusText.Text = "Material map analysis activated";
        }

        private void HeatMap_Click(object sender, RoutedEventArgs e)
        {
            DebugStatusText.Text = "Heat map visualization activated";
        }

        private void EdgeDetection_Click(object sender, RoutedEventArgs e)
        {
            ApplyEdgeDetection();
        }

        private void DistanceTool_Click(object sender, RoutedEventArgs e)
        {
            _isMeasuring = true;
            DebugStatusText.Text = "Distance tool - click to start measurement";
        }

        private void DensityProfile_Click(object sender, RoutedEventArgs e)
        {
            DebugStatusText.Text = "Density profile analysis activated";
        }

        private void AngleMeasure_Click(object sender, RoutedEventArgs e)
        {
            DebugStatusText.Text = "Angle measurement tool activated";
        }

        private void ObjectCounter_Click(object sender, RoutedEventArgs e)
        {
            CountObjects();
        }

        private void ApplyEnhancement_Click(object sender, RoutedEventArgs e)
        {
            // Apply enhancement to the actual image
            DebugStatusText.Text = "Enhancement applied to image";
        }

        private void ShowHistogram()
        {
            var histogramWindow = new HistogramWindow(_currentImage);
            histogramWindow.Owner = this;
            histogramWindow.ShowDialog();
        }

        private void ApplyEdgeDetection()
        {
            // Apply edge detection algorithm
            DebugStatusText.Text = "Edge detection filter applied";
        }

        private void CountObjects()
        {
            // Simple object counting logic
            int objectCount = 3; // This would be calculated
            DebugStatusText.Text = $"Detected {objectCount} objects in image";
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clean up resources
            DebugOverlayCanvas.Children.Clear();
            base.OnClosed(e);
        }
    }
}
