using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using SEE_INSADE.Core.Config;

namespace SEE_INSADE.Services.Scanning
{
    public class ScanService
    {
        private const double BeamX = 0.0;
        private const double AirDensity = 0.01;
        private const double RayPathScale = 0.18;
        private const double NoiseAmplitude = 0.012;

        private readonly Random _random;
        private readonly List<ScanObject> _objects = new();
        private ScanData _currentScan = null!;
        private double _beltPosition;
        private int _capturedColumns;

        public ScanService()
        {
            _random = new Random();
            ResetScan();
        }

        public void ResetScan()
        {
            var config = ConfigManager.Current.ScanSettings;
            int width = config.Width;
            int height = config.Height;
            int detectorCount = height;

            _currentScan = new ScanData
            {
                Image = CreateBlankBitmap(width, height),
                MaterialMap = new MaterialType[width, height],
                DensityMap = new double[width, height],
                ScanPosition = 0,
                ObjectCount = 0,
                DetectorData = new DetectorInfo[detectorCount],
                ScanLines = new ScanLineData[width]
            };

            InitializeOutputMaps();
            InitializeDetectors(detectorCount, height);
            InitializeSceneObjects(width, height);
            InitializeScanColumns(width);

            _beltPosition = 0;
            _capturedColumns = 0;
            _currentScan.ObjectCount = _objects.Count;
        }

        public void UpdateScan(double speed = 1.0)
        {
            var config = ConfigManager.Current.ScanSettings;
            double beltStep = Math.Max(0.1, speed) * config.Speed;

            _currentScan.ScanPosition = _beltPosition;
            ShiftScanBufferLeft();
            AcquireDetectorColumn(_currentScan.Image.PixelWidth - 1, _beltPosition);

            _beltPosition += beltStep;
            _capturedColumns++;

            double sceneLength = config.Width + 300;
            if (_beltPosition >= sceneLength)
            {
                _beltPosition = 0;
                _capturedColumns = 0;
                ClearImageAndOutputMaps();
                InitializeScanColumns(_currentScan.Image.PixelWidth);
            }
        }

        private void AcquireDetectorColumn(int outputX, double beltPosition)
        {
            if (outputX < 0 || outputX >= _currentScan.Image.PixelWidth)
                return;

            var scanColumn = _currentScan.ScanLines[outputX];
            scanColumn.IsScanned = true;
            scanColumn.Timestamp = DateTime.Now;

            byte[] pixels = new byte[_currentScan.Image.PixelHeight * 4];

            for (int detectorIndex = 0; detectorIndex < _currentScan.DetectorData.Length; detectorIndex++)
            {
                var detector = _currentScan.DetectorData[detectorIndex];
                int y = detectorIndex;

                var sample = SampleSceneAtDetector(beltPosition, y);
                double reading = GetDetectorReading(sample.Material, sample.Density, detector);
                detector.CurrentReading = reading;

                _currentScan.MaterialMap[outputX, y] = sample.Material;
                _currentScan.DensityMap[outputX, y] = sample.Density;

                byte intensity = ToXrayIntensity(reading);
                int index = y * 4;
                pixels[index] = intensity;
                pixels[index + 1] = intensity;
                pixels[index + 2] = intensity;
                pixels[index + 3] = 255;
            }

            _currentScan.Image.WritePixels(
                new Int32Rect(outputX, 0, 1, _currentScan.Image.PixelHeight),
                pixels,
                4,
                0);
        }

        private void ShiftScanBufferLeft()
        {
            int width = _currentScan.Image.PixelWidth;
            int height = _currentScan.Image.PixelHeight;

            if (width <= 1)
                return;

            byte[] pixels = new byte[width * height * 4];
            _currentScan.Image.CopyPixels(pixels, width * 4, 0);

            int rowStride = width * 4;
            int shiftedBytes = (width - 1) * 4;
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * rowStride;
                Buffer.BlockCopy(pixels, rowStart + 4, pixels, rowStart, shiftedBytes);

                int lastPixel = rowStart + shiftedBytes;
                pixels[lastPixel] = 0;
                pixels[lastPixel + 1] = 0;
                pixels[lastPixel + 2] = 0;
                pixels[lastPixel + 3] = 255;
            }

            for (int x = 0; x < width - 1; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _currentScan.MaterialMap[x, y] = _currentScan.MaterialMap[x + 1, y];
                    _currentScan.DensityMap[x, y] = _currentScan.DensityMap[x + 1, y];
                }

                _currentScan.ScanLines[x] = _currentScan.ScanLines[x + 1];
                _currentScan.ScanLines[x].LineNumber = x;
            }

            for (int y = 0; y < height; y++)
            {
                _currentScan.MaterialMap[width - 1, y] = MaterialType.Air;
                _currentScan.DensityMap[width - 1, y] = AirDensity;
            }

            _currentScan.ScanLines[width - 1] = new ScanLineData
            {
                LineNumber = width - 1,
                IsScanned = false,
                Timestamp = DateTime.MinValue
            };

            _currentScan.Image.WritePixels(new Int32Rect(0, 0, width, height), pixels, rowStride, 0);
        }

        private SceneSample SampleSceneAtDetector(double beltPosition, int detectorY)
        {
            double worldX = BeamX + beltPosition;
            foreach (var scanObject in _objects)
            {
                if (scanObject.Contains(worldX, detectorY))
                {
                    double localX = (worldX - scanObject.X) / Math.Max(1, scanObject.Width);
                    double localY = (detectorY - scanObject.Y) / Math.Max(1, scanObject.Height);
                    double shape = 0.78 + 0.22 * Math.Sin(localX * Math.PI) * Math.Sin(localY * Math.PI);
                    double density = scanObject.BaseDensity * shape;

                    return new SceneSample(scanObject.Material, density);
                }
            }

            return new SceneSample(MaterialType.Air, AirDensity);
        }

        private double GetDetectorReading(MaterialType material, double density, DetectorInfo detector)
        {
            double attenuation = GetMaterialAttenuation(material, density);
            double transmittedIntensity = Math.Exp(-attenuation * RayPathScale);
            double calibratedReading = transmittedIntensity * detector.Sensitivity;
            calibratedReading += (_random.NextDouble() - 0.5) * NoiseAmplitude;

            return Math.Clamp(calibratedReading, 0, 1);
        }

        private double GetMaterialAttenuation(MaterialType material, double density)
        {
            return material switch
            {
                MaterialType.Air => 0.01 * density,
                MaterialType.Organic => 0.50 * density,
                MaterialType.Inorganic => 1.00 * density,
                MaterialType.Plastic => 0.70 * density,
                MaterialType.Glass => 1.10 * density,
                MaterialType.Liquid => 0.35 * density,
                MaterialType.LightMetal => 1.80 * density,
                MaterialType.HeavyMetal => 5.50 * density,
                MaterialType.Electronics => 3.20 * density,
                MaterialType.Mixed => 2.20 * density,
                _ => 0.10 * density
            };
        }

        private static byte ToXrayIntensity(double detectorReading)
        {
            return (byte)Math.Clamp((1.0 - detectorReading) * 255.0, 0, 255);
        }

        private void InitializeDetectors(int detectorCount, int imageHeight)
        {
            for (int i = 0; i < detectorCount; i++)
            {
                _currentScan.DetectorData[i] = new DetectorInfo
                {
                    Id = i,
                    Position = i,
                    IsActive = true,
                    Sensitivity = 0.96 + (_random.NextDouble() * 0.08),
                    CurrentReading = 1.0,
                    Health = 95 + (_random.NextDouble() * 5)
                };
            }
        }

        private void InitializeScanColumns(int width)
        {
            for (int i = 0; i < width; i++)
            {
                _currentScan.ScanLines[i] = new ScanLineData
                {
                    LineNumber = i,
                    IsScanned = false,
                    Timestamp = DateTime.MinValue
                };
            }
        }

        private void InitializeOutputMaps()
        {
            int width = _currentScan.MaterialMap.GetLength(0);
            int height = _currentScan.MaterialMap.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _currentScan.MaterialMap[x, y] = MaterialType.Air;
                    _currentScan.DensityMap[x, y] = AirDensity;
                }
            }
        }

        private void InitializeSceneObjects(int scanWidth, int scanHeight)
        {
            _objects.Clear();

            AddObject(90, 95, 135, 80, MaterialType.Organic, 0.75);
            AddObject(245, 145, 70, 110, MaterialType.Plastic, 0.62);
            AddObject(355, 185, 46, 46, MaterialType.HeavyMetal, 0.95);
            AddObject(455, 115, 95, 76, MaterialType.Electronics, 0.86);
            AddObject(625, 170, 110, 72, MaterialType.Glass, 0.55);
            AddObject(780, 210, 58, 58, MaterialType.LightMetal, 0.72);

            // A mixed bag-like region makes the line scanner output less synthetic.
            AddObject(520, 230, 70, 95, MaterialType.Mixed, 0.65);
        }

        private void AddObject(double x, double y, double width, double height, MaterialType material, double baseDensity)
        {
            _objects.Add(new ScanObject(x, y, width, height, material, baseDensity));
        }

        private void ClearImageAndOutputMaps()
        {
            InitializeOutputMaps();

            byte[] pixels = new byte[_currentScan.Image.PixelWidth * _currentScan.Image.PixelHeight * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
                pixels[i + 3] = 255;
            }

            _currentScan.Image.WritePixels(
                new Int32Rect(0, 0, _currentScan.Image.PixelWidth, _currentScan.Image.PixelHeight),
                pixels,
                _currentScan.Image.PixelWidth * 4,
                0);
        }

        public ScanData GetCurrentScan()
        {
            return _currentScan;
        }

        public double GetScanProgress()
        {
            var config = ConfigManager.Current.ScanSettings;
            return Math.Clamp(_beltPosition / (config.Width + 300.0), 0, 1);
        }

        public int GetCurrentScanLine() => Math.Min(_capturedColumns, _currentScan.Image.PixelWidth);

        private WriteableBitmap CreateBlankBitmap(int width, int height)
        {
            var bitmap = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);

            byte[] blackPixels = new byte[width * height * 4];
            for (int i = 0; i < blackPixels.Length; i += 4)
            {
                blackPixels[i] = 0;
                blackPixels[i + 1] = 0;
                blackPixels[i + 2] = 0;
                blackPixels[i + 3] = 255;
            }
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), blackPixels, width * 4, 0);

            return bitmap;
        }

        private readonly record struct SceneSample(MaterialType Material, double Density);

        private sealed class ScanObject
        {
            public ScanObject(double x, double y, double width, double height, MaterialType material, double baseDensity)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Material = material;
                BaseDensity = baseDensity;
            }

            public double X { get; }
            public double Y { get; }
            public double Width { get; }
            public double Height { get; }
            public MaterialType Material { get; }
            public double BaseDensity { get; }

            public bool Contains(double x, double y)
            {
                if (x < X || x >= X + Width || y < Y || y >= Y + Height)
                    return false;

                double centerX = X + Width / 2.0;
                double centerY = Y + Height / 2.0;
                double normalizedX = (x - centerX) / (Width / 2.0);
                double normalizedY = (y - centerY) / (Height / 2.0);

                return normalizedX * normalizedX + normalizedY * normalizedY <= 1.0;
            }
        }
    }

    public class ScanData
    {
        public WriteableBitmap Image { get; set; } = null!;
        public MaterialType[,] MaterialMap { get; set; } = null!;
        public double[,] DensityMap { get; set; } = null!;
        public DetectorInfo[] DetectorData { get; set; } = null!;
        public ScanLineData[] ScanLines { get; set; } = null!;
        public double ScanPosition { get; set; }
        public int ObjectCount { get; set; }
    }

    public class DetectorInfo
    {
        public int Id { get; set; }
        public double Position { get; set; }
        public bool IsActive { get; set; }
        public double Sensitivity { get; set; }
        public double CurrentReading { get; set; }
        public double Health { get; set; }
    }

    public class ScanLineData
    {
        public int LineNumber { get; set; }
        public bool IsScanned { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
