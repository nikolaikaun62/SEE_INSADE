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
        private const double LowEnergyKev = 80.0;
        private const double HighEnergyKev = 160.0;
        private const double RayPathScale = 1.0;
        private const double NoiseAmplitude = 0.012;
        private const double MinSignal = 0.0001;

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
                var measurement = MeasureDualEnergy(sample.Material, sample.Density, detector);

                detector.CurrentReading = measurement.DisplaySignal;
                detector.LowEnergyReading = measurement.LowEnergySignal;
                detector.HighEnergyReading = measurement.HighEnergySignal;
                detector.AttenuationRatio = measurement.AttenuationRatio;
                detector.EstimatedZ = measurement.EstimatedZ;
                detector.DetectedMaterial = measurement.DetectedMaterial;

                _currentScan.MaterialMap[outputX, y] = measurement.DetectedMaterial;
                _currentScan.DensityMap[outputX, y] = measurement.ArealDensity;

                byte intensity = ToXrayIntensity(measurement.DisplaySignal);
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

        private DualEnergyMeasurement MeasureDualEnergy(MaterialType material, double arealDensity, DetectorInfo detector)
        {
            double trueZ = GetEffectiveAtomicNumber(material);
            double lowMu = GetMassAttenuation(LowEnergyKev, trueZ);
            double highMu = GetMassAttenuation(HighEnergyKev, trueZ);

            double lowSignal = Transmit(lowMu, arealDensity, detector);
            double highSignal = Transmit(highMu, arealDensity, detector);

            double lowAbsorbance = -Math.Log(Math.Clamp(lowSignal / detector.Sensitivity, MinSignal, 1.0));
            double highAbsorbance = -Math.Log(Math.Clamp(highSignal / detector.Sensitivity, MinSignal, 1.0));
            double attenuationRatio = highAbsorbance > 0.00001 ? lowAbsorbance / highAbsorbance : 1.0;
            double estimatedZ = EstimateEffectiveAtomicNumber(attenuationRatio);
            MaterialType detectedMaterial = ClassifyMaterial(estimatedZ, highAbsorbance);

            return new DualEnergyMeasurement(
                LowEnergySignal: lowSignal,
                HighEnergySignal: highSignal,
                DisplaySignal: 0.62 * lowSignal + 0.38 * highSignal,
                AttenuationRatio: attenuationRatio,
                EstimatedZ: estimatedZ,
                ArealDensity: Math.Clamp(highAbsorbance / Math.Max(highMu, 0.00001), 0, 1.5),
                DetectedMaterial: detectedMaterial);
        }

        private double Transmit(double attenuationCoefficient, double arealDensity, DetectorInfo detector)
        {
            double signal = Math.Exp(-attenuationCoefficient * arealDensity * RayPathScale) * detector.Sensitivity;
            signal += (_random.NextDouble() - 0.5) * NoiseAmplitude;
            return Math.Clamp(signal, MinSignal, 1.0);
        }

        private double GetMassAttenuation(double energyKev, double effectiveZ)
        {
            double normalizedEnergy = energyKev / 100.0;
            double comptonTerm = 0.26;
            double photoElectricTerm = 0.0024 * Math.Pow(effectiveZ, 3.15) / Math.Pow(normalizedEnergy, 3.0);

            return comptonTerm + photoElectricTerm;
        }

        private double EstimateEffectiveAtomicNumber(double attenuationRatio)
        {
            double bestZ = 7.0;
            double bestError = double.MaxValue;

            for (double z = 5.0; z <= 32.0; z += 0.1)
            {
                double modelRatio = GetMassAttenuation(LowEnergyKev, z) / GetMassAttenuation(HighEnergyKev, z);
                double error = Math.Abs(modelRatio - attenuationRatio);

                if (error < bestError)
                {
                    bestError = error;
                    bestZ = z;
                }
            }

            return bestZ;
        }

        private MaterialType ClassifyMaterial(double estimatedZ, double highAbsorbance)
        {
            if (highAbsorbance < 0.015)
                return MaterialType.Air;

            return estimatedZ switch
            {
                < 7.2 => MaterialType.Organic,
                < 8.8 => MaterialType.Plastic,
                < 11.5 => MaterialType.Liquid,
                < 14.5 => MaterialType.Inorganic,
                < 17.0 => MaterialType.Glass,
                < 22.0 => MaterialType.LightMetal,
                < 28.0 => MaterialType.Electronics,
                _ => MaterialType.HeavyMetal
            };
        }

        private double GetEffectiveAtomicNumber(MaterialType material)
        {
            return material switch
            {
                MaterialType.Air => 7.3,
                MaterialType.Organic => 6.4,
                MaterialType.Plastic => 7.8,
                MaterialType.Liquid => 9.5,
                MaterialType.Inorganic => 12.5,
                MaterialType.Glass => 15.0,
                MaterialType.LightMetal => 18.0,
                MaterialType.Electronics => 23.0,
                MaterialType.HeavyMetal => 30.0,
                MaterialType.Mixed => 16.5,
                _ => 10.0
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
                    LowEnergyReading = 1.0,
                    HighEnergyReading = 1.0,
                    AttenuationRatio = 1.0,
                    EstimatedZ = GetEffectiveAtomicNumber(MaterialType.Air),
                    DetectedMaterial = MaterialType.Air,
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

            AddObject(90, 95, 135, 80, MaterialType.Organic, 0.72);
            AddObject(245, 145, 70, 110, MaterialType.Plastic, 0.60);
            AddObject(355, 185, 46, 46, MaterialType.HeavyMetal, 0.95);
            AddObject(455, 115, 95, 76, MaterialType.Electronics, 0.78);
            AddObject(625, 170, 110, 72, MaterialType.Glass, 0.58);
            AddObject(780, 210, 58, 58, MaterialType.LightMetal, 0.74);

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

        private readonly record struct DualEnergyMeasurement(
            double LowEnergySignal,
            double HighEnergySignal,
            double DisplaySignal,
            double AttenuationRatio,
            double EstimatedZ,
            double ArealDensity,
            MaterialType DetectedMaterial);

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
        public double LowEnergyReading { get; set; }
        public double HighEnergyReading { get; set; }
        public double AttenuationRatio { get; set; }
        public double EstimatedZ { get; set; }
        public MaterialType DetectedMaterial { get; set; }
        public double Health { get; set; }
    }

    public class ScanLineData
    {
        public int LineNumber { get; set; }
        public bool IsScanned { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
