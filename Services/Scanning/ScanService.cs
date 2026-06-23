using System;
using System.Windows.Media.Imaging;
using SEE_INSADE.Core.Config;

namespace SEE_INSADE.Services.Scanning
{
    public class ScanService
    {
        private ScanData _currentScan = null!;
        private Random _random;
        private double _scanProgress = 0;
        private int _currentScanLine = 0;

        public ScanService()
        {
            _random = new Random();
            ResetScan();
        }

        public void ResetScan()
        {
            var config = ConfigManager.Current.ScanSettings;
            _currentScan = new ScanData
            {
                Image = CreateBlankBitmap(config.Width, config.Height),
                MaterialMap = new MaterialType[config.Width, config.Height],
                DensityMap = new double[config.Width, config.Height],
                ScanPosition = 0,
                ObjectCount = 0,
                DetectorData = new DetectorInfo[config.DetectorCount],
                ScanLines = new ScanLineData[config.Height]
            };

            InitializeDetectors();
            InitializeTestObjects();
            InitializeScanLines();
            _scanProgress = 0;
            _currentScanLine = 0;
        }

        public void UpdateScan(double speed = 1.0)
        {
            var config = ConfigManager.Current.ScanSettings;

            // Двигаем конвейер
            _currentScan.ScanPosition += speed;
            _scanProgress = _currentScan.ScanPosition / config.Width;

            // Сканируем текущую строку детекторами
            ScanCurrentLine();

            // Переходим к следующей строке
            _currentScanLine = (int)(_currentScan.ScanPosition / 2) % config.Height;

            if (_currentScan.ScanPosition > config.Width + 200)
            {
                ResetScan();
            }
        }

        private void ScanCurrentLine()
        {
            if (_currentScanLine >= _currentScan.ScanLines.Length) return;

            var scanLine = _currentScan.ScanLines[_currentScanLine];
            scanLine.IsScanned = true;
            scanLine.Timestamp = DateTime.Now;

            // Эмулируем работу каждого детектора в этой строке
            for (int detectorIndex = 0; detectorIndex < _currentScan.DetectorData.Length; detectorIndex++)
            {
                var detector = _currentScan.DetectorData[detectorIndex];
                if (detector.IsActive)
                {
                    // Получаем показания детектора для текущей позиции
                    double reading = GetDetectorReading(detectorIndex, _currentScanLine);
                    detector.CurrentReading = reading;

                    // Обновляем изображение на основе показаний
                    UpdatePixelFromDetectorReading(detectorIndex, _currentScanLine, reading);
                }
            }
        }

        private double GetDetectorReading(int detectorX, int scanLineY)
        {
            // Эмулируем физику рентгеновского луча
            double baseIntensity = 1.0; // Исходная интенсивность рентгена
            double transmittedIntensity = baseIntensity;

            // Проходим луч через все объекты по пути к детектору
            for (int objectY = 0; objectY <= scanLineY; objectY++)
            {
                var material = _currentScan.MaterialMap[detectorX, objectY];
                var density = _currentScan.DensityMap[detectorX, objectY];

                // Коэффициент ослабления в зависимости от материала
                double attenuation = GetMaterialAttenuation(material, density);
                transmittedIntensity *= Math.Exp(-attenuation * 0.1); // Закон Бугера-Ламберта-Бера
            }

            // Детектор регистрирует оставшуюся интенсивность
            double reading = transmittedIntensity / baseIntensity;

            // Добавляем небольшой шум
            reading += (_random.NextDouble() - 0.5) * 0.02;

            return Math.Max(0, Math.Min(1, reading));
        }

        private double GetMaterialAttenuation(MaterialType material, double density)
        {
            // Коэффициенты ослабления для разных материалов
            return material switch
            {
                MaterialType.Air => 0.01 * density,
                MaterialType.Organic => 0.5 * density,
                MaterialType.Inorganic => 1.0 * density,
                MaterialType.Plastic => 0.8 * density,
                MaterialType.Glass => 1.2 * density,
                MaterialType.Liquid => 0.3 * density,
                MaterialType.LightMetal => 2.0 * density,
                MaterialType.HeavyMetal => 5.0 * density,
                MaterialType.Electronics => 3.0 * density,
                _ => 0.1 * density
            };
        }

        private void UpdatePixelFromDetectorReading(int x, int y, double reading)
        {
            // Преобразуем показания детектора в цвет пикселя
            byte intensity = (byte)((1.0 - reading) * 255);

            var pixels = new byte[4];
            pixels[0] = intensity; // B
            pixels[1] = intensity; // G  
            pixels[2] = intensity; // R
            pixels[3] = 255;       // A

            _currentScan.Image.WritePixels(new System.Windows.Int32Rect(x, y, 1, 1), pixels, 4, 0);
        }

        private void InitializeDetectors()
        {
            var config = ConfigManager.Current.ScanSettings;
            for (int i = 0; i < config.DetectorCount; i++)
            {
                _currentScan.DetectorData[i] = new DetectorInfo
                {
                    Id = i,
                    Position = i * (config.Width / (double)config.DetectorCount),
                    IsActive = true,
                    Sensitivity = 0.9 + (_random.NextDouble() * 0.2),
                    CurrentReading = 0.0,
                    Health = 95 + (_random.NextDouble() * 10)
                };
            }
        }

        private void InitializeScanLines()
        {
            var config = ConfigManager.Current.ScanSettings;
            for (int i = 0; i < config.Height; i++)
            {
                _currentScan.ScanLines[i] = new ScanLineData
                {
                    LineNumber = i,
                    IsScanned = false,
                    Timestamp = DateTime.MinValue
                };
            }
        }

        private void InitializeTestObjects()
        {
            int width = _currentScan.MaterialMap.GetLength(0);
            int height = _currentScan.MaterialMap.GetLength(1);

            // Очищаем массивы (воздух)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    _currentScan.MaterialMap[x, y] = MaterialType.Air;
                    _currentScan.DensityMap[x, y] = 0.01;
                }
            }

            // Создаем реалистичные объекты
            AddObject(100, 150, 120, 80, MaterialType.Organic, 0.7);
            AddObject(250, 120, 60, 100, MaterialType.Plastic, 0.6);
            AddObject(350, 180, 40, 40, MaterialType.HeavyMetal, 0.9);
            AddObject(450, 140, 80, 60, MaterialType.Electronics, 0.8);
            AddObject(600, 160, 100, 70, MaterialType.Glass, 0.5);
            AddObject(750, 200, 50, 50, MaterialType.LightMetal, 0.7);

            _currentScan.ObjectCount = 6;
        }

        private void AddObject(int x, int y, int width, int height, MaterialType material, double baseDensity)
        {
            for (int objY = y; objY < y + height && objY < _currentScan.MaterialMap.GetLength(1); objY++)
            {
                for (int objX = x; objX < x + width && objX < _currentScan.MaterialMap.GetLength(0); objX++)
                {
                    _currentScan.MaterialMap[objX, objY] = material;
                    double variation = 1.0 + (_random.NextDouble() * 0.4 - 0.2);
                    _currentScan.DensityMap[objX, objY] = baseDensity * variation;
                }
            }
        }

        public ScanData GetCurrentScan()
        {
            return _currentScan;
        }

        public double GetScanProgress() => _scanProgress;

        public int GetCurrentScanLine() => _currentScanLine;

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
            bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), blackPixels, width * 4, 0);

            return bitmap;
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