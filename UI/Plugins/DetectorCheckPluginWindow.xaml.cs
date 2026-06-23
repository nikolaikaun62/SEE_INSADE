using System;
using System.Linq;
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
        private const int BitmapHeight = 260;

        private readonly ScanService _scanService;
        private readonly DispatcherTimer _timer;
        private readonly WriteableBitmap _bitmap;

        public DetectorCheckPluginWindow(ScanService scanService)
        {
            InitializeComponent();
            SEE_INSADE.Core.Localization.LocalizationHelper.Apply(this);

            _scanService = scanService;
            _bitmap = new WriteableBitmap(BitmapWidth, BitmapHeight, 96, 96, PixelFormats.Bgr32, null);
            DetectorLineImage.Source = _bitmap;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _timer.Tick += (_, _) => UpdateDetectorLine();

            Loaded += (_, _) =>
            {
                _timer.Start();
                UpdateDetectorLine();
            };

            Closed += (_, _) => _timer.Stop();
        }

        private void UpdateDetectorLine()
        {
            DetectorInfo[] detectors = _scanService.GetCurrentScan().DetectorData;
            if (detectors.Length == 0)
                return;

            byte[] pixels = new byte[BitmapWidth * BitmapHeight * 4];
            FillBackground(pixels);
            DrawGuideLines(pixels);

            int activeCount = 0;
            double signalTotal = 0;
            double lowTotal = 0;
            double highTotal = 0;

            for (int i = 0; i < detectors.Length; i++)
            {
                DetectorInfo detector = detectors[i];
                double signal = Math.Clamp(detector.CurrentReading, 0.0, 1.0);
                double sensitivity = Math.Clamp(detector.Sensitivity / 1.08, 0.0, 1.0);
                int x0 = i * BitmapWidth / detectors.Length;
                int x1 = Math.Max(x0 + 1, (i + 1) * BitmapWidth / detectors.Length);
                int barHeight = Math.Max(8, (int)(signal * (BitmapHeight - 58)));
                int y0 = BitmapHeight - 30 - barHeight;
                Color color = GetDetectorColor(signal, sensitivity, detector.Health);

                if (detector.IsActive)
                    activeCount++;

                signalTotal += signal;
                lowTotal += detector.LowEnergyReading;
                highTotal += detector.HighEnergyReading;

                for (int x = x0; x < x1; x++)
                {
                    for (int y = y0; y < BitmapHeight - 30; y++)
                    {
                        SetPixel(pixels, x, y, color);
                    }

                    SetPixel(pixels, x, BitmapHeight - 28, Blend(color, Colors.White, 0.35));
                    SetPixel(pixels, x, BitmapHeight - 27, Blend(color, Colors.White, 0.18));
                }
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, BitmapWidth, BitmapHeight), pixels, BitmapWidth * 4, 0);

            double averageSignal = signalTotal / detectors.Length;
            double averageLow = lowTotal / detectors.Length;
            double averageHigh = highTotal / detectors.Length;

            ActiveText.Text = $"Active: {activeCount}/{detectors.Length}";
            AverageText.Text = $"Signal: {averageSignal:F3}  Low/High: {averageLow:F3}/{averageHigh:F3}";
        }

        private static void FillBackground(byte[] pixels)
        {
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
                pixels[i + 1] = 255;
                pixels[i + 2] = 255;
                pixels[i + 3] = 255;
            }
        }

        private static void DrawGuideLines(byte[] pixels)
        {
            for (int y = 34; y < BitmapHeight - 24; y += 46)
            {
                for (int x = 0; x < BitmapWidth; x++)
                {
                    SetPixel(pixels, x, y, Color.FromRgb(226, 232, 240));
                }
            }
        }

        private static Color GetDetectorColor(double signal, double sensitivity, double health)
        {
            if (health < 60)
                return Color.FromRgb(221, 64, 64);

            byte red = (byte)Math.Clamp(245 - signal * 96, 54, 245);
            byte green = (byte)Math.Clamp(120 + sensitivity * 110, 90, 235);
            byte blue = (byte)Math.Clamp(68 + signal * 126, 68, 220);

            return Color.FromRgb(red, green, blue);
        }

        private static Color Blend(Color foreground, Color background, double backgroundAmount)
        {
            double foregroundAmount = 1.0 - backgroundAmount;
            return Color.FromRgb(
                (byte)(foreground.R * foregroundAmount + background.R * backgroundAmount),
                (byte)(foreground.G * foregroundAmount + background.G * backgroundAmount),
                (byte)(foreground.B * foregroundAmount + background.B * backgroundAmount));
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

        private void DetectorLineImage_MouseMove(object sender, MouseEventArgs e)
        {
            DetectorInfo[] detectors = _scanService.GetCurrentScan().DetectorData;
            if (detectors.Length == 0)
                return;

            Point point = e.GetPosition(DetectorLineImage);
            int detectorIndex = (int)Math.Clamp(point.X / Math.Max(1, DetectorLineImage.ActualWidth) * detectors.Length, 0, detectors.Length - 1);
            DetectorInfo detector = detectors[detectorIndex];

            SelectedText.Text =
                $"Detector {detector.Id}: signal {detector.CurrentReading:F3}, sensitivity {detector.Sensitivity:F3}, " +
                $"low/high {detector.LowEnergyReading:F3}/{detector.HighEnergyReading:F3}, " +
                $"Zeff {detector.EstimatedZ:F1}, material {detector.DetectedMaterial}";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
