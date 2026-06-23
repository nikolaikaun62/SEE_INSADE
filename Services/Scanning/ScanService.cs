using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using SEE_INSADE.Core.Config;

namespace SEE_INSADE.Services.Scanning
{
    public enum ScanDirection
    {
        Forward,
        Backward
    }

    public class ScanService
    {
        private const double BeamX = 0.0;
        private const double AirDensity = 0.01;
        private const double LowEnergyKev = 80.0;
        private const double HighEnergyKev = 160.0;
        private const double RayPathScale = 1.0;
        private const double NoiseAmplitude = 0.010;
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
            UpdateScan(speed, ScanDirection.Forward);
        }

        public void UpdateScan(double speed, ScanDirection direction)
        {
            var config = ConfigManager.Current.ScanSettings;
            double beltStep = Math.Max(0.1, speed) * config.Speed;
            double sceneLength = config.Width + 300;

            _currentScan.ScanPosition = _beltPosition;

            if (direction == ScanDirection.Forward)
            {
                // Visual movement: left -> right.
                ShiftScanBufferRight();
                AcquireDetectorColumn(0, _beltPosition);
                _beltPosition += beltStep;

                if (_beltPosition >= sceneLength)
                {
                    _beltPosition = 0;
                    RestartVisualScan();
                }
            }
            else
            {
                // Visual movement: right -> left.
                ShiftScanBufferLeft();
                AcquireDetectorColumn(_currentScan.Image.PixelWidth - 1, _beltPosition);
                _beltPosition -= beltStep;

                if (_beltPosition <= 0)
                {
                    _beltPosition = sceneLength;
                    RestartVisualScan();
                }
            }

            _capturedColumns++;
        }

        private void RestartVisualScan()
        {
            _capturedColumns = 0;
            ClearImageAndOutputMaps();
            InitializeSceneObjects(_currentScan.Image.PixelWidth, _currentScan.Image.PixelHeight);
            InitializeScanColumns(_currentScan.Image.PixelWidth);
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

        private void ShiftScanBufferRight()
        {
            int width = _currentScan.Image.PixelWidth;
            int height = _currentScan.Image.PixelHeight;

            if (width <= 1)
                return;

            byte[] pixels = new byte[width * height * 4];
            _currentScan.Image.CopyPixels(pixels, width * 4, 0);

            int rowStride = width * 4;

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * rowStride;

                Buffer.BlockCopy(pixels, rowStart, pixels, rowStart + 4, (width - 1) * 4);

                pixels[rowStart] = 0;
                pixels[rowStart + 1] = 0;
                pixels[rowStart + 2] = 0;
                pixels[rowStart + 3] = 255;
            }

            for (int x = width - 1; x >= 1; x--)
            {
                for (int y = 0; y < height; y++)
                {
                    _currentScan.MaterialMap[x, y] = _currentScan.MaterialMap[x - 1, y];
                    _currentScan.DensityMap[x, y] = _currentScan.DensityMap[x - 1, y];
                }

                _currentScan.ScanLines[x] = _currentScan.ScanLines[x - 1];
                _currentScan.ScanLines[x].LineNumber = x;
            }

            for (int y = 0; y < height; y++)
            {
                _currentScan.MaterialMap[0, y] = MaterialType.Air;
                _currentScan.DensityMap[0, y] = AirDensity;
            }

            _currentScan.ScanLines[0] = new ScanLineData
            {
                LineNumber = 0,
                IsScanned = false,
                Timestamp = DateTime.MinValue
            };

            _currentScan.Image.WritePixels(new Int32Rect(0, 0, width, height), pixels, rowStride, 0);
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

            bool hasHit = false;
            double totalDensity = 0.0;
            double strongestVisualWeight = 0.0;
            MaterialType dominantMaterial = MaterialType.Air;

            foreach (var scanObject in _objects)
            {
                if (!scanObject.Contains(worldX, detectorY))
                    continue;

                double thickness = scanObject.GetThicknessFactor(worldX, detectorY);
                double texture = scanObject.IsStructured
                    ? 1.0
                    : 1.0 + (_random.NextDouble() - 0.5) * 0.08;

                double density = scanObject.BaseDensity * thickness * texture;
                totalDensity += density;
                hasHit = true;

                double visualWeight = density * scanObject.VisualPriority;

                if (visualWeight > strongestVisualWeight)
                {
                    strongestVisualWeight = visualWeight;
                    dominantMaterial = scanObject.Material;
                }
            }

            if (!hasHit)
                return new SceneSample(MaterialType.Air, AirDensity);

            return new SceneSample(dominantMaterial, Math.Clamp(totalDensity, 0.02, 2.35));
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

            MaterialType detectedMaterial = PreserveVisibleMaterial(material)
                ? material
                : ClassifyMaterial(estimatedZ, highAbsorbance);

            return new DualEnergyMeasurement(
                LowEnergySignal: lowSignal,
                HighEnergySignal: highSignal,
                DisplaySignal: 0.62 * lowSignal + 0.38 * highSignal,
                AttenuationRatio: attenuationRatio,
                EstimatedZ: estimatedZ,
                // Keep physical accumulated density for display.
                // This makes steel thickness steps visually different instead of saturating into one dark tone.
                ArealDensity: Math.Clamp(arealDensity, 0, 2.35),
                DetectedMaterial: detectedMaterial);
        }

        private static bool PreserveVisibleMaterial(MaterialType material)
        {
            return material is MaterialType.Salt
                or MaterialType.Sugar
                or MaterialType.Aluminum
                or MaterialType.LightMetal
                or MaterialType.Iron
                or MaterialType.HeavyMetal
                or MaterialType.Gold
                or MaterialType.Lead
                or MaterialType.Mixed;
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

            for (double z = 5.0; z <= 79.0; z += 0.2)
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
                < 7.0 => MaterialType.Organic,
                < 7.8 => MaterialType.Sugar,
                < 8.8 => MaterialType.Plastic,
                < 11.5 => MaterialType.Liquid,
                < 13.5 => MaterialType.Inorganic,
                < 14.6 => MaterialType.Aluminum,
                < 16.5 => MaterialType.Glass,
                < 20.0 => MaterialType.Salt,
                < 24.0 => MaterialType.LightMetal,
                < 28.5 => MaterialType.Iron,
                < 34.0 => MaterialType.Electronics,
                < 55.0 => MaterialType.HeavyMetal,
                < 72.0 => MaterialType.Lead,
                _ => MaterialType.Gold
            };
        }

        private double GetEffectiveAtomicNumber(MaterialType material)
        {
            return material switch
            {
                MaterialType.Air => 7.3,
                MaterialType.Organic => 6.4,
                MaterialType.Sugar => 6.8,
                MaterialType.Plastic => 7.8,
                MaterialType.Liquid => 9.5,
                MaterialType.Inorganic => 12.5,
                MaterialType.Aluminum => 13.0,
                MaterialType.Glass => 15.0,
                MaterialType.Salt => 16.0,
                MaterialType.LightMetal => 18.0,
                MaterialType.Iron => 26.0,
                MaterialType.Electronics => 23.0,
                MaterialType.HeavyMetal => 45.0,
                MaterialType.Lead => 82.0,
                MaterialType.Gold => 79.0,
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

            // Photo-like STP objects: transparent orange square plates and long side cases.
            AddPhotoStpPlate(70, 55, 1.05, -0.02);
            AddPhotoStpLongCase(300, 245, 1.00, 0.02);

            AddPhotoStpPlate(455, 70, 1.18, 0.035);
            AddPhotoStpLongCase(735, 252, 1.15, -0.015);

            AddPhotoStpPlate(945, 78, 1.08, -0.015);
            AddPhotoStpLongCase(1195, 252, 0.98, 0.02);

            AddLooseSceneObjects(scanWidth, scanHeight);

            _currentScan.ObjectCount = _objects.Count;
        }

        private void AddLooseSceneObjects(int scanWidth, int scanHeight)
        {
            double sceneLength = scanWidth + 300;
            int objectCount = _random.Next(5, 10);

            for (int i = 0; i < objectCount; i++)
            {
                MaterialType material = PickRandomMaterial();
                ScanShape shape = PickShapeForMaterial(material);

                double width = RandomRange(16, 85);
                double height = RandomRange(9, 58);

                if (shape == ScanShape.Capsule)
                {
                    width *= 1.55;
                    height *= 0.45;
                }

                if (IsMetal(material))
                {
                    width *= RandomRange(0.40, 0.75);
                    height *= RandomRange(0.35, 0.70);
                }

                double maxX = Math.Max(25, sceneLength - width - 20);
                double maxY = Math.Max(20, scanHeight - height - 15);

                double x = RandomRange(20, maxX);
                double y = RandomRange(15, maxY);
                double density = GetBaseDensity(material) * RandomRange(0.75, 1.25);
                double rotation = RandomRange(0, Math.PI);

                AddObject(x, y, width, height, material, density, shape, rotation, 1.0, false);
            }
        }

        private void AddPhotoStpPlate(double x, double y, double scale, double rotation)
        {
            // Larger photo-like carrier plate, but all test elements are kept inside it.
            double w = 250 * scale;
            double h = 230 * scale;

            // Transparent orange STP carrier plate.
            AddObject(x, y, w, h, MaterialType.Plastic, 0.12, ScanShape.RoundedRectangle, rotation, 0.12, true);
            AddObject(x + 5 * scale, y + 5 * scale, w - 10 * scale, h - 10 * scale, MaterialType.Plastic, 0.07, ScanShape.RoundedRectangle, rotation, 0.08, true);

            // Outer rim.
            AddObject(x + 2 * scale, y + 2 * scale, w - 4 * scale, 4 * scale, MaterialType.Plastic, 0.42, ScanShape.Capsule, rotation, 0.28, true);
            AddObject(x + 2 * scale, y + h - 7 * scale, w - 4 * scale, 4 * scale, MaterialType.Plastic, 0.42, ScanShape.Capsule, rotation, 0.28, true);
            AddObject(x + 2 * scale, y + 2 * scale, 4 * scale, h - 4 * scale, MaterialType.Plastic, 0.42, ScanShape.Capsule, rotation, 0.28, true);
            AddObject(x + w - 6 * scale, y + 2 * scale, 4 * scale, h - 4 * scale, MaterialType.Plastic, 0.42, ScanShape.Capsule, rotation, 0.28, true);

            // Side handle/loop, kept outside functional test zone but attached to the plate.
            AddObject(x + w - 3 * scale, y + 75 * scale, 36 * scale, 68 * scale, MaterialType.Plastic, 0.24, ScanShape.Ellipse, rotation, 0.20, true);

            // ---------------------------------------------------------
            // TEST 1 & 2: single wire resolution + useful penetration.
            // Row 0: wires without metal cover.
            // Rows 1-3: the same wires under steel plates 4.8 / 7.9 / 11.1 mm.
            // ---------------------------------------------------------
            double t12x = x + 18 * scale;
            double t12y = y + 18 * scale;
            double colW = 30 * scale;
            double rowH = 20 * scale;
            double t12W = 4 * colW;
            double t12H = 4 * rowH;

            AddObject(t12x, t12y, t12W, t12H, MaterialType.Aluminum, 0.16, ScanShape.Rectangle, rotation, 0.20, true);

            // Steel covers. There is no organic/orange strip here.
            AddObject(t12x, t12y + rowH,     t12W, rowH, MaterialType.Iron, 0.34, ScanShape.Rectangle, rotation, 2.10, true); // 4.8 mm steel
            AddObject(t12x, t12y + rowH * 2, t12W, rowH, MaterialType.Iron, 0.52, ScanShape.Rectangle, rotation, 2.25, true); // 7.9 mm steel
            AddObject(t12x, t12y + rowH * 3, t12W, rowH, MaterialType.Iron, 0.72, ScanShape.Rectangle, rotation, 2.40, true); // 11.1 mm steel

            for (int r = 0; r <= 4; r++)
            {
                AddObject(t12x, t12y + r * rowH, t12W, 1.0 * scale, MaterialType.HeavyMetal, 0.75, ScanShape.Capsule, rotation, 0.80, true);
            }

            double[] wireDiameters = { 0.8, 1.0, 1.35, 1.8 };
            double[] wireDensities = { 1.25, 1.30, 1.36, 1.42 };

            for (int c = 0; c < 4; c++)
            {
                double cx = t12x + c * colW + 12 * scale;

                AddWireSnake(cx, t12y + 3 * scale,              rowH - 6 * scale, wireDiameters[c] * scale, rotation, wireDensities[c]);
                AddWireSnake(cx, t12y + rowH + 3 * scale,       rowH - 6 * scale, wireDiameters[c] * scale, rotation, wireDensities[c]);
                AddWireSnake(cx, t12y + rowH * 2 + 3 * scale,   rowH - 6 * scale, wireDiameters[c] * scale, rotation, wireDensities[c]);
                AddWireSnake(cx, t12y + rowH * 3 + 3 * scale,   rowH - 6 * scale, wireDiameters[c] * scale, rotation, wireDensities[c]);
            }

            // ---------------------------------------------------------
            // TEST 5: material discrimination.
            // Salt is mineral/inorganic, sugar is organic.
            // ---------------------------------------------------------
            double t5x = x + 165 * scale;
            double t5y = y + 18 * scale;
            AddObject(t5x, t5y,              58 * scale, 24 * scale, MaterialType.Salt,  0.90, ScanShape.Rectangle, rotation, 1.65, true);
            AddObject(t5x, t5y + 26 * scale, 58 * scale, 24 * scale, MaterialType.Sugar, 0.58, ScanShape.Rectangle, rotation, 1.45, true);

            // ---------------------------------------------------------
            // TEST 4B: lead rod UNDER steel covers.
            // Steel steps: 14,16,18,20,22,24,26,28,30 mm.
            // Lead does not repaint the area as a top layer; it adds density underneath.
            // ---------------------------------------------------------
            double t4bx = x + 160 * scale;
            double t4by = y + 76 * scale;
            double stepW = 70 * scale;
            double stepH = 13 * scale;
            double rodX = t4bx + 31 * scale;

            int[] steelThicknessMm = { 14, 16, 18, 20, 22, 24, 26, 28, 30 };

            // Lead rod is added first and with lower visual priority than steel.
            // Its density contributes to attenuation under every steel step.
            AddObject(
                rodX,
                t4by,
                8 * scale,
                stepH * steelThicknessMm.Length,
                MaterialType.Lead,
                1.85,
                ScanShape.Rectangle,
                rotation,
                1.15,
                true);

            for (int i = 0; i < steelThicknessMm.Length; i++)
            {
                double normalized = (steelThicknessMm[i] - 14) / 16.0;

                // Lower density range prevents saturation, so 14..30 mm are visibly different.
                double density = 0.20 + normalized * 0.85;

                AddObject(
                    t4bx,
                    t4by + i * stepH,
                    stepW,
                    stepH,
                    MaterialType.Iron,
                    density,
                    ScanShape.Rectangle,
                    rotation,
                    3.0 + normalized,
                    true);

                AddObject(
                    t4bx,
                    t4by + i * stepH,
                    stepW,
                    0.9 * scale,
                    MaterialType.HeavyMetal,
                    0.45,
                    ScanShape.Capsule,
                    rotation,
                    0.70,
                    true);
            }

            // ---------------------------------------------------------
            // TEST 3: spatial resolution. Groups of cuts/bars with different spacing.
            // ---------------------------------------------------------
            double t3x = x + 20 * scale;
            double t3y = y + 112 * scale;

            AddObject(t3x, t3y, 26 * scale, 24 * scale, MaterialType.Aluminum, 0.58, ScanShape.Rectangle, rotation, 1.00, true);
            AddWireGroup(t3x + 4 * scale, t3y + 4 * scale, scale, rotation, vertical: true, count: 4, gap: 4.0, length: 16, material: MaterialType.Iron, density: 1.12);

            AddObject(t3x + 38 * scale, t3y, 32 * scale, 24 * scale, MaterialType.Aluminum, 0.58, ScanShape.Rectangle, rotation, 1.00, true);
            AddWireGroup(t3x + 43 * scale, t3y + 4 * scale, scale * 1.55, rotation, vertical: true, count: 4, gap: 7.2, length: 16, material: MaterialType.Iron, density: 1.20);

            AddObject(t3x + 82 * scale, t3y, 26 * scale, 24 * scale, MaterialType.Aluminum, 0.58, ScanShape.Rectangle, rotation, 1.00, true);
            AddWireGroup(t3x + 86 * scale, t3y + 4 * scale, scale, rotation, vertical: true, count: 4, gap: 4.6, length: 16, material: MaterialType.Iron, density: 1.12);

            AddObject(t3x, t3y + 34 * scale, 28 * scale, 22 * scale, MaterialType.Aluminum, 0.58, ScanShape.Rectangle, rotation, 1.00, true);
            AddWireGroup(t3x + 4 * scale, t3y + 39 * scale, scale, rotation, vertical: false, count: 4, gap: 3.8, length: 20, material: MaterialType.Iron, density: 1.12);

            AddObject(t3x + 38 * scale, t3y + 34 * scale, 32 * scale, 22 * scale, MaterialType.Aluminum, 0.58, ScanShape.Rectangle, rotation, 1.00, true);
            AddWireGroup(t3x + 42 * scale, t3y + 38 * scale, scale * 1.55, rotation, vertical: false, count: 4, gap: 7.0, length: 24, material: MaterialType.Iron, density: 1.20);

            AddObject(t3x + 82 * scale, t3y + 34 * scale, 28 * scale, 22 * scale, MaterialType.Aluminum, 0.58, ScanShape.Rectangle, rotation, 1.00, true);
            AddWireGroup(t3x + 86 * scale, t3y + 39 * scale, scale, rotation, vertical: false, count: 4, gap: 4.2, length: 20, material: MaterialType.Iron, density: 1.12);

            // ---------------------------------------------------------
            // TEST 4A: simple penetration thin metal plates 0.15 / 0.10 / 0.05 mm.
            // Kept inside the STP carrier.
            // ---------------------------------------------------------
            double t4ax = x + 22 * scale;
            double t4ay = y + 190 * scale;
            AddObject(t4ax,              t4ay, 34 * scale, 22 * scale, MaterialType.Aluminum, 0.15, ScanShape.Rectangle, rotation, 1.25, true); // 0.15 mm
            AddObject(t4ax + 45 * scale, t4ay, 34 * scale, 22 * scale, MaterialType.Aluminum, 0.10, ScanShape.Rectangle, rotation, 1.18, true); // 0.10 mm
            AddObject(t4ax + 90 * scale, t4ay, 34 * scale, 22 * scale, MaterialType.Aluminum, 0.05, ScanShape.Rectangle, rotation, 1.12, true); // 0.05 mm

            AddTickMarksTestAccurate(x, y, scale, rotation);
        }

        private void AddWireSnake(double x, double y, double height, double thickness, double rotation, double density)
        {
            // Approximation of a curved single wire using small overlapping segments.
            int segments = 7;
            double segH = height / segments;
            for (int i = 0; i < segments; i++)
            {
                double phase = (double)i / (segments - 1);
                double shift = Math.Sin(phase * Math.PI * 1.2) * (4.0 + thickness) * 0.8;
                AddObject(
                    x + shift,
                    y + i * segH,
                    Math.Max(0.9, thickness),
                    Math.Max(1.8, segH + 0.8),
                    MaterialType.Iron,
                    density,
                    ScanShape.Capsule,
                    rotation,
                    5.2,
                    true);
            }
        }

        private void AddWireGroup(double x, double y, double scale, double rotation, bool vertical, int count, double gap, double length, MaterialType material, double density)
        {
            for (int i = 0; i < count; i++)
            {
                double offset = i * gap * scale;

                if (vertical)
                {
                    AddObject(
                        x + offset,
                        y,
                        Math.Max(0.8, 1.05 * scale),
                        length * scale,
                        material,
                        density,
                        ScanShape.Capsule,
                        rotation,
                        4.5,
                        true);
                }
                else
                {
                    AddObject(
                        x,
                        y + offset,
                        length * scale,
                        Math.Max(0.8, 1.05 * scale),
                        material,
                        density,
                        ScanShape.Capsule,
                        rotation,
                        4.5,
                        true);
                }
            }
        }

        private void AddTickMarksTestAccurate(double ix, double iy, double scale, double rotation)
        {
            // Simple dark marks imitating printed labels around test fields.
            for (int i = 0; i < 4; i++)
            {
                AddObject(ix + i * 30 * scale + 6 * scale, iy - 5 * scale, 8 * scale, 1.0 * scale, MaterialType.HeavyMetal, 1.10, ScanShape.Capsule, rotation, 1.5, true);
            }

            for (int i = 0; i < 3; i++)
            {
                AddObject(ix + 118 * scale, iy + 30 * scale + i * 28 * scale, 7 * scale, 1.0 * scale, MaterialType.HeavyMetal, 1.10, ScanShape.Capsule, rotation, 1.5, true);
            }

            for (int i = 0; i < 9; i++)
            {
                AddObject(ix + 141 * scale, iy + 73 * scale + i * 14 * scale, 7 * scale, 1.0 * scale, MaterialType.HeavyMetal, 1.10, ScanShape.Capsule, rotation, 1.5, true);
            }
        }

        private void AddPhotoStpLongCase(double x, double y, double scale, double rotation)
        {
            double w = 165 * scale;
            double h = 42 * scale;

            AddObject(x, y, w, h, MaterialType.Plastic, 0.16, ScanShape.RoundedRectangle, rotation, 0.15, true);
            AddObject(x + 4 * scale, y + 4 * scale, w - 8 * scale, 3 * scale, MaterialType.Plastic, 0.40, ScanShape.Capsule, rotation, 0.35, true);
            AddObject(x + 4 * scale, y + h - 7 * scale, w - 8 * scale, 3 * scale, MaterialType.Plastic, 0.40, ScanShape.Capsule, rotation, 0.35, true);
            AddObject(x + 10 * scale, y + 14 * scale, 92 * scale, 10 * scale, MaterialType.Iron, 1.58, ScanShape.Rectangle, rotation, 2.8, true);
            AddObject(x + 56 * scale, y + 12 * scale, 7 * scale, 14 * scale, MaterialType.Lead, 2.20, ScanShape.Rectangle, rotation, 5.8, true);
            AddObject(x + 104 * scale, y + 14 * scale, 26 * scale, 10 * scale, MaterialType.Aluminum, 1.05, ScanShape.Rectangle, rotation, 2.0, true);
            AddObject(x + 133 * scale, y + 14 * scale, 12 * scale, 10 * scale, MaterialType.Salt, 0.88, ScanShape.Rectangle, rotation, 1.5, true);

            AddObject(x + w - 5 * scale, y + 7 * scale, 26 * scale, 28 * scale, MaterialType.Plastic, 0.30, ScanShape.Ellipse, rotation, 0.25, true);
        }

        private void AddObject(
            double x,
            double y,
            double width,
            double height,
            MaterialType material,
            double baseDensity,
            ScanShape shape,
            double rotation,
            double visualPriority,
            bool isStructured)
        {
            _objects.Add(new ScanObject(x, y, width, height, material, baseDensity, shape, rotation, visualPriority, isStructured));
        }

        private MaterialType PickRandomMaterial()
        {
            double roll = _random.NextDouble();

            if (roll < 0.12) return MaterialType.Organic;
            if (roll < 0.20) return MaterialType.Sugar;
            if (roll < 0.28) return MaterialType.Salt;
            if (roll < 0.39) return MaterialType.Plastic;
            if (roll < 0.48) return MaterialType.Liquid;
            if (roll < 0.56) return MaterialType.Inorganic;
            if (roll < 0.64) return MaterialType.Glass;
            if (roll < 0.73) return MaterialType.Aluminum;
            if (roll < 0.81) return MaterialType.LightMetal;
            if (roll < 0.90) return MaterialType.Iron;
            if (roll < 0.95) return MaterialType.Electronics;
            if (roll < 0.985) return MaterialType.HeavyMetal;
            if (roll < 0.995) return MaterialType.Lead;

            return MaterialType.Gold;
        }

        private ScanShape PickShapeForMaterial(MaterialType material)
        {
            if (material is MaterialType.Aluminum or MaterialType.Iron or MaterialType.LightMetal)
                return _random.NextDouble() < 0.70 ? ScanShape.Capsule : ScanShape.Rectangle;

            if (material == MaterialType.Gold)
                return _random.NextDouble() < 0.55 ? ScanShape.Ellipse : ScanShape.Rectangle;

            if (material is MaterialType.Salt or MaterialType.Sugar)
                return _random.NextDouble() < 0.75 ? ScanShape.Rectangle : ScanShape.Ellipse;

            if (material == MaterialType.Electronics)
                return ScanShape.Rectangle;

            return (ScanShape)_random.Next(0, 3);
        }

        private double GetBaseDensity(MaterialType material)
        {
            return material switch
            {
                MaterialType.Air => 0.01,
                MaterialType.Organic => RandomRange(0.32, 0.85),
                MaterialType.Sugar => RandomRange(0.52, 0.95),
                MaterialType.Salt => RandomRange(0.75, 1.20),
                MaterialType.Plastic => RandomRange(0.34, 0.95),
                MaterialType.Liquid => RandomRange(0.55, 1.05),
                MaterialType.Inorganic => RandomRange(0.72, 1.20),
                MaterialType.Glass => RandomRange(0.65, 1.10),
                MaterialType.Aluminum => RandomRange(0.78, 1.25),
                MaterialType.LightMetal => RandomRange(0.90, 1.35),
                MaterialType.Iron => RandomRange(1.05, 1.70),
                MaterialType.Electronics => RandomRange(0.80, 1.45),
                MaterialType.HeavyMetal => RandomRange(1.20, 1.90),
                MaterialType.Lead => RandomRange(1.90, 2.30),
                MaterialType.Gold => RandomRange(1.55, 2.25),
                MaterialType.Mixed => RandomRange(0.75, 1.50),
                _ => RandomRange(0.30, 1.00)
            };
        }

        private static bool IsMetal(MaterialType material)
        {
            return material is MaterialType.Aluminum
                or MaterialType.LightMetal
                or MaterialType.Iron
                or MaterialType.HeavyMetal
                or MaterialType.Gold
                or MaterialType.Lead;
        }

        private double RandomRange(double min, double max)
        {
            if (max <= min)
                return min;

            return min + _random.NextDouble() * (max - min);
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

        private enum ScanShape
        {
            Ellipse,
            Rectangle,
            RoundedRectangle,
            Capsule
        }

        private sealed class ScanObject
        {
            public ScanObject(
                double x,
                double y,
                double width,
                double height,
                MaterialType material,
                double baseDensity,
                ScanShape shape,
                double rotation,
                double visualPriority,
                bool isStructured)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Material = material;
                BaseDensity = baseDensity;
                Shape = shape;
                Rotation = rotation;
                VisualPriority = visualPriority;
                IsStructured = isStructured;
            }

            public double X { get; }
            public double Y { get; }
            public double Width { get; }
            public double Height { get; }
            public MaterialType Material { get; }
            public double BaseDensity { get; }
            public ScanShape Shape { get; }
            public double Rotation { get; }
            public double VisualPriority { get; }
            public bool IsStructured { get; }

            public bool Contains(double x, double y)
            {
                ToLocal(x, y, out double localX, out double localY);

                double halfW = Width / 2.0;
                double halfH = Height / 2.0;

                if (halfW <= 0 || halfH <= 0)
                    return false;

                return Shape switch
                {
                    ScanShape.Rectangle =>
                        Math.Abs(localX) <= halfW &&
                        Math.Abs(localY) <= halfH,

                    ScanShape.RoundedRectangle =>
                        ContainsRoundedRectangle(localX, localY, halfW, halfH),

                    ScanShape.Ellipse =>
                        Math.Pow(localX / halfW, 2) +
                        Math.Pow(localY / halfH, 2) <= 1.0,

                    ScanShape.Capsule =>
                        ContainsCapsule(localX, localY),

                    _ => false
                };
            }

            public double GetThicknessFactor(double x, double y)
            {
                ToLocal(x, y, out double localX, out double localY);

                double halfW = Width / 2.0;
                double halfH = Height / 2.0;

                if (halfW <= 0 || halfH <= 0)
                    return 0.0;

                double radius = Shape switch
                {
                    ScanShape.Rectangle or ScanShape.RoundedRectangle =>
                        Math.Max(Math.Abs(localX) / halfW, Math.Abs(localY) / halfH),

                    ScanShape.Ellipse =>
                        Math.Sqrt(Math.Pow(localX / halfW, 2) + Math.Pow(localY / halfH, 2)),

                    ScanShape.Capsule =>
                        GetCapsuleRadius(localX, localY),

                    _ => 1.0
                };

                radius = Math.Clamp(radius, 0.0, 1.0);

                if (Shape is ScanShape.Rectangle or ScanShape.RoundedRectangle)
                    return 0.82 + 0.18 * (1.0 - radius);

                return 0.25 + Math.Sqrt(Math.Max(0.0, 1.0 - radius * radius)) * 0.90;
            }

            private void ToLocal(double x, double y, out double localX, out double localY)
            {
                double centerX = X + Width / 2.0;
                double centerY = Y + Height / 2.0;

                double dx = x - centerX;
                double dy = y - centerY;

                double cos = Math.Cos(-Rotation);
                double sin = Math.Sin(-Rotation);

                localX = dx * cos - dy * sin;
                localY = dx * sin + dy * cos;
            }

            private static bool ContainsRoundedRectangle(double localX, double localY, double halfW, double halfH)
            {
                double radius = Math.Min(halfW, halfH) * 0.12;
                double innerW = halfW - radius;
                double innerH = halfH - radius;

                if (Math.Abs(localX) <= innerW && Math.Abs(localY) <= halfH)
                    return true;

                if (Math.Abs(localX) <= halfW && Math.Abs(localY) <= innerH)
                    return true;

                double cx = Math.Sign(localX) * innerW;
                double cy = Math.Sign(localY) * innerH;
                double dx = localX - cx;
                double dy = localY - cy;

                return dx * dx + dy * dy <= radius * radius;
            }

            private bool ContainsCapsule(double localX, double localY)
            {
                double radius = Height / 2.0;

                if (radius <= 0)
                    return false;

                if (Width <= Height)
                {
                    double ellipse =
                        Math.Pow(localX / (Width / 2.0), 2) +
                        Math.Pow(localY / (Height / 2.0), 2);

                    return ellipse <= 1.0;
                }

                double halfLine = (Width - Height) / 2.0;

                if (Math.Abs(localX) <= halfLine && Math.Abs(localY) <= radius)
                    return true;

                double capCenterX = localX < 0 ? -halfLine : halfLine;
                double dx = localX - capCenterX;
                double dy = localY;

                return dx * dx + dy * dy <= radius * radius;
            }

            private double GetCapsuleRadius(double localX, double localY)
            {
                double radius = Height / 2.0;

                if (radius <= 0)
                    return 1.0;

                if (Width <= Height)
                {
                    return Math.Sqrt(
                        Math.Pow(localX / (Width / 2.0), 2) +
                        Math.Pow(localY / (Height / 2.0), 2));
                }

                double halfLine = (Width - Height) / 2.0;

                double dx = Math.Max(Math.Abs(localX) - halfLine, 0.0);
                double dy = localY;

                return Math.Sqrt(dx * dx + dy * dy) / radius;
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
