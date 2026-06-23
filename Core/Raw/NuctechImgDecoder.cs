using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SEE_INSADE.Core.Raw
{
    public sealed class NuctechImgScan
    {
        public string FilePath { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public string SerialNumber { get; init; } = string.Empty;
        public string ScanTimeText { get; init; } = string.Empty;
        public int Width { get; init; }
        public int Height { get; init; }
        public int DataOffset { get; init; }
        public int TrailingBytes { get; init; }
        public WriteableBitmap Bitmap { get; init; } = null!;
        public MaterialType[,] MaterialMap { get; init; } = null!;
        public double[,] DensityMap { get; init; } = null!;
    }

    public static class NuctechImgDecoder
    {
        private const int DefaultDetectorHeight = 876;
        private const int DefaultMultiPlaneOffset = 640;
        private const int DefaultPlaneCount = 4;
        private const int MaxAutoHeaderSearchBytes = 96 * 1024;

        public static NuctechImgScan Decode(
            string filePath,
            int detectorHeight = DefaultDetectorHeight,
            int manualOffset = -1,
            bool rotate90Clockwise = true,
            bool flipHorizontal = false,
            bool flipVertical = false)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is empty.", nameof(filePath));

            byte[] data = File.ReadAllBytes(filePath);
            int headerHeight = TryReadHeaderDetectorHeight(data);

            if (manualOffset < 0 &&
                TryDecodeNativeOisPng(
                    data,
                    filePath,
                    rotate90Clockwise,
                    flipHorizontal,
                    flipVertical,
                    out NuctechImgScan? nativeScan))
            {
                return nativeScan!;
            }

            if (TryDecodeNuctechContainer(
                    data,
                    filePath,
                    manualOffset,
                    rotate90Clockwise,
                    flipHorizontal,
                    flipVertical,
                    out NuctechImgScan? containerScan))
            {
                return containerScan!;
            }

            if (detectorHeight <= 0)
                detectorHeight = headerHeight > 0 ? headerHeight : DefaultDetectorHeight;
            else if (manualOffset < 0 && headerHeight > 0 && Math.Abs(detectorHeight - headerHeight) > 16)
                detectorHeight = headerHeight;

            if (TryDecodeMultiPlane16(
                    data,
                    filePath,
                    detectorHeight,
                    manualOffset,
                    rotate90Clockwise,
                    flipHorizontal,
                    flipVertical,
                    out NuctechImgScan? multiPlaneScan))
            {
                return multiPlaneScan!;
            }

            DecodeLayout layout = manualOffset >= 0
                ? CreateManualLayout(data, detectorHeight, manualOffset)
                : DetectBestLayout(data, detectorHeight);

            if (layout.Width <= 0 || layout.Height <= 0)
                throw new InvalidDataException("Unable to detect image dimensions.");

            byte[] pixels = new byte[layout.Width * layout.Height * 4];
            MaterialType[,] materialMap = new MaterialType[layout.Width, layout.Height];
            double[,] densityMap = new double[layout.Width, layout.Height];

            int sourceIndex = layout.Offset;
            int imageBytes = layout.Width * layout.Height * 4;

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    int src = sourceIndex + (y * layout.Width + x) * 4;
                    int dst = (y * layout.Width + x) * 4;

                    if (src + 3 >= data.Length || src >= sourceIndex + imageBytes)
                    {
                        pixels[dst] = 255;
                        pixels[dst + 1] = 255;
                        pixels[dst + 2] = 255;
                        pixels[dst + 3] = 255;
                        materialMap[x, y] = MaterialType.Air;
                        densityMap[x, y] = 0;
                        continue;
                    }

                    byte b = data[src];
                    byte g = data[src + 1];
                    byte r = data[src + 2];
                    byte tag = data[src + 3];

                    pixels[dst] = b;
                    pixels[dst + 1] = g;
                    pixels[dst + 2] = r;
                    pixels[dst + 3] = 255;

                    materialMap[x, y] = ClassifyMaterial(r, g, b, tag);
                    densityMap[x, y] = EstimateDensity(r, g, b, tag);
                }
            }

            WriteableBitmap bitmap = CreateBitmap(layout.Width, layout.Height, pixels);
            NuctechImgScan scan = new NuctechImgScan
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Model = TryExtractModel(data),
                SerialNumber = TryExtractSerial(data),
                ScanTimeText = TryExtractDateTime(data),
                Width = layout.Width,
                Height = layout.Height,
                DataOffset = layout.Offset,
                TrailingBytes = layout.TrailingBytes,
                Bitmap = bitmap,
                MaterialMap = materialMap,
                DensityMap = densityMap
            };

            if (rotate90Clockwise || flipHorizontal || flipVertical)
                scan = Transform(scan, rotate90Clockwise, flipHorizontal, flipVertical);

            return scan;
        }

        private static bool TryDecodeNuctechContainer(
            byte[] data,
            string filePath,
            int manualOffset,
            bool rotate90Clockwise,
            bool flipHorizontal,
            bool flipVertical,
            out NuctechImgScan? scan)
        {
            scan = null;

            if (data.Length < 900)
                return false;

            int sectionType = BitConverter.ToInt32(data, 56);

            if (sectionType == 3)
                return TryDecodeGroupedU16Container(data, filePath, manualOffset, rotate90Clockwise, flipHorizontal, flipVertical, out scan);

            return TryDecodeBgraContainer(data, filePath, manualOffset, rotate90Clockwise, flipHorizontal, flipVertical, out scan);
        }

        private static bool TryDecodeBgraContainer(
            byte[] data,
            string filePath,
            int manualOffset,
            bool rotate90Clockwise,
            bool flipHorizontal,
            bool flipVertical,
            out NuctechImgScan? scan)
        {
            scan = null;

            if (data.Length < 244)
                return false;

            int width = BitConverter.ToUInt16(data, 68);
            int height = BitConverter.ToUInt16(data, 70);
            int dataOffset = manualOffset >= 0 ? manualOffset : 244;

            if (width < 40 || height < 40 || width > 5000 || height > 3000)
                return false;

            if (dataOffset + width * height * 4 > data.Length)
                return false;

            byte[] pixels = new byte[width * height * 4];
            MaterialType[,] materialMap = new MaterialType[width, height];
            double[,] densityMap = new double[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int src = dataOffset + (y * width + x) * 4;
                    int dst = (y * width + x) * 4;
                    byte b = data[src];
                    byte g = data[src + 1];
                    byte r = data[src + 2];
                    byte tag = data[src + 3];

                    pixels[dst] = b;
                    pixels[dst + 1] = g;
                    pixels[dst + 2] = r;
                    pixels[dst + 3] = 255;
                    materialMap[x, y] = ClassifyMaterial(r, g, b, tag);
                    densityMap[x, y] = EstimateDensity(r, g, b, tag);
                }
            }

            scan = CreateScan(filePath, data, width, height, dataOffset, data.Length - (dataOffset + width * height * 4), pixels, materialMap, densityMap);

            if (rotate90Clockwise || flipHorizontal || flipVertical)
                scan = Transform(scan, rotate90Clockwise, flipHorizontal, flipVertical);

            return true;
        }

        private static bool TryDecodeGroupedU16Container(
            byte[] data,
            string filePath,
            int manualOffset,
            bool rotate90Clockwise,
            bool flipHorizontal,
            bool flipVertical,
            out NuctechImgScan? scan)
        {
            scan = null;

            const int baseOffset = 640;
            if (data.Length < baseOffset + 176)
                return false;

            int blockLength = BitConverter.ToInt32(data, baseOffset);
            int payloadLength = BitConverter.ToInt32(data, baseOffset + 16);
            int fullWidth = BitConverter.ToUInt16(data, baseOffset + 8);
            int height = BitConverter.ToUInt16(data, baseOffset + 10);
            int dataOffset = manualOffset >= 0
                ? manualOffset
                : baseOffset + Math.Max(176, blockLength - payloadLength);

            if (fullWidth < 120 || height < 40 || fullWidth > 12000 || height > 3000)
                return false;

            int width = fullWidth;

            if (dataOffset + width * height * 2 > data.Length)
                return false;

            ushort[] intensitySamples = new ushort[width * height];

            for (int y = 0; y < height; y++)
            {
                int rowStart = dataOffset + y * width * 2;

                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    intensitySamples[index] = BitConverter.ToUInt16(data, rowStart + x * 2);
                }
            }

            (double intensityMin, double intensityMax) = GetPercentileRange(intensitySamples, 0.005, 0.995);

            byte[] pixels = new byte[width * height * 4];
            byte[] graySamples = new byte[width * height];
            MaterialType[,] materialMap = new MaterialType[width, height];
            double[,] densityMap = new double[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    double normalized = Normalize(intensitySamples[index], intensityMin, intensityMax);
                    double density = Math.Clamp(normalized * 2.35, 0.0, 2.35);
                    MaterialType material = ClassifySingleEnergy(density);
                    graySamples[index] = (byte)Math.Clamp(255 - (normalized * 255), 0, 255);
                    materialMap[x, y] = material;
                    densityMap[x, y] = density;
                }
            }

            byte[] displayGray = SmoothDetectorPattern(graySamples, width, height);

            for (int i = 0; i < displayGray.Length; i++)
            {
                int pixelIndex = i * 4;
                byte gray = displayGray[i];
                pixels[pixelIndex] = gray;
                pixels[pixelIndex + 1] = gray;
                pixels[pixelIndex + 2] = gray;
                pixels[pixelIndex + 3] = 255;
            }

            scan = CreateScan(filePath, data, width, height, dataOffset, Math.Max(0, data.Length - (dataOffset + width * height * 2)), pixels, materialMap, densityMap);

            if (rotate90Clockwise || flipHorizontal || flipVertical)
                scan = Transform(scan, rotate90Clockwise, flipHorizontal, flipVertical);

            return true;
        }

        private static NuctechImgScan CreateScan(
            string filePath,
            byte[] data,
            int width,
            int height,
            int dataOffset,
            int trailingBytes,
            byte[] pixels,
            MaterialType[,] materialMap,
            double[,] densityMap)
        {
            return new NuctechImgScan
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Model = TryExtractModel(data),
                SerialNumber = TryExtractSerial(data),
                ScanTimeText = TryExtractDateTime(data),
                Width = width,
                Height = height,
                DataOffset = dataOffset,
                TrailingBytes = trailingBytes,
                Bitmap = CreateBitmap(width, height, pixels),
                MaterialMap = materialMap,
                DensityMap = densityMap
            };
        }

        private static bool TryDecodeNativeOisPng(
            byte[] data,
            string filePath,
            bool rotate90Clockwise,
            bool flipHorizontal,
            bool flipVertical,
            out NuctechImgScan? scan)
        {
            scan = null;

            string? helperPath = FindNativeHelperPath();
            string? sdkPath = FindOisSdkPath();

            if (helperPath == null || sdkPath == null)
                return false;

            string workDir = Path.Combine(Path.GetTempPath(), "SEE_INSADE_OIS_IMG_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = helperPath,
                    Arguments = $"--sdk {Quote(sdkPath)} --img {Quote(filePath)} --out {Quote(workDir)}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using Process? process = Process.Start(startInfo);
                if (process == null)
                    return false;

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(30000))
                {
                    try { process.Kill(true); } catch { }
                    return false;
                }

                _ = stdoutTask.GetAwaiter().GetResult();
                _ = stderrTask.GetAwaiter().GetResult();

                if (process.ExitCode != 0)
                    return false;

                string pngPath = Path.Combine(workDir, "view0.png");
                if (!File.Exists(pngPath))
                    pngPath = Directory.EnumerateFiles(workDir, "view*.png").OrderBy(path => path).FirstOrDefault() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(pngPath) || !File.Exists(pngPath))
                    return false;

                scan = CreateScanFromPng(filePath, data, pngPath);

                if (rotate90Clockwise || flipHorizontal || flipVertical)
                    scan = Transform(scan, rotate90Clockwise, flipHorizontal, flipVertical);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static NuctechImgScan CreateScanFromPng(string filePath, byte[] sourceData, string pngPath)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(pngPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            var converted = new FormatConvertedBitmap(image, PixelFormats.Bgr32, null, 0);
            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            byte[] pixels = new byte[width * height * 4];
            converted.CopyPixels(pixels, width * 4, 0);

            MaterialType[,] materialMap = new MaterialType[width, height];
            double[,] densityMap = new double[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte b = pixels[index];
                    byte g = pixels[index + 1];
                    byte r = pixels[index + 2];

                    materialMap[x, y] = ClassifyMaterial(r, g, b, 0);
                    densityMap[x, y] = EstimateDensity(r, g, b, 0);
                    pixels[index + 3] = 255;
                }
            }

            return new NuctechImgScan
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Model = TryExtractModel(sourceData),
                SerialNumber = TryExtractSerial(sourceData),
                ScanTimeText = TryExtractDateTime(sourceData),
                Width = width,
                Height = height,
                DataOffset = 0,
                TrailingBytes = 0,
                Bitmap = CreateBitmap(width, height, pixels),
                MaterialMap = materialMap,
                DensityMap = densityMap
            };
        }

        private static string? FindNativeHelperPath()
        {
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "Tools", "OisImgNativeDecodeHelper", "OisImgNativeDecodeHelper.exe"),
                Path.Combine(Environment.CurrentDirectory, "Tools", "OisImgNativeDecodeHelper", "bin", "Debug", "net8.0-windows", "win-x86", "OisImgNativeDecodeHelper.exe"),
                Path.Combine(Environment.CurrentDirectory, "Tools", "OisImgNativeDecodeHelper", "bin", "Release", "net8.0-windows", "win-x86", "OisImgNativeDecodeHelper.exe"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Tools", "OisImgNativeDecodeHelper", "bin", "Debug", "net8.0-windows", "win-x86", "OisImgNativeDecodeHelper.exe")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Tools", "OisImgNativeDecodeHelper", "bin", "Release", "net8.0-windows", "win-x86", "OisImgNativeDecodeHelper.exe"))
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static string? FindOisSdkPath()
        {
            string? envPath = Environment.GetEnvironmentVariable("SEE_INSADE_OIS_SDK");
            string[] candidates =
            {
                envPath ?? string.Empty,
                Path.Combine(AppContext.BaseDirectory, "Plugins", "NuctechImg", "NativeRuntime"),
                Path.Combine(Environment.CurrentDirectory, "Plugins", "NuctechImg", "NativeRuntime"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Plugins", "NuctechImg", "NativeRuntime")),
                @"D:\OISV3\Plug-ins\Plugin_WeKnow\sdk",
                @"C:\OISV3\Plug-ins\Plugin_WeKnow\sdk",
                @"D:\XRayV3\Plug-ins\Plugin_WeKnow\sdk",
                @"C:\XRayV3\Plug-ins\Plugin_WeKnow\sdk"
            };

            return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "img2png.dll")));
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static bool TryDecodeMultiPlane16(
            byte[] data,
            string filePath,
            int detectorHeight,
            int manualOffset,
            bool rotate90Clockwise,
            bool flipHorizontal,
            bool flipVertical,
            out NuctechImgScan? scan)
        {
            scan = null;

            if (detectorHeight < 128 || detectorHeight > 4096)
                return false;

            int offset = manualOffset >= 0
                ? Math.Clamp(manualOffset, 0, Math.Max(0, data.Length - 4))
                : DefaultMultiPlaneOffset;

            int usable = data.Length - offset;
            int fullRowWidth = usable / (detectorHeight * 4);
            int planeWidth = fullRowWidth / DefaultPlaneCount;

            if (planeWidth < 40 || planeWidth > 3000)
                return false;

            int trailing = usable - fullRowWidth * detectorHeight * 4;
            int selectedPlane = 0;

            ushort[] low = new ushort[planeWidth * detectorHeight];
            ushort[] high = new ushort[planeWidth * detectorHeight];

            for (int y = 0; y < detectorHeight; y++)
            {
                int rowStart = offset + (y * fullRowWidth + selectedPlane * planeWidth) * 4;

                for (int x = 0; x < planeWidth; x++)
                {
                    int source = rowStart + x * 4;
                    if (source + 3 >= data.Length)
                        return false;

                    int index = y * planeWidth + x;
                    low[index] = BitConverter.ToUInt16(data, source);
                    high[index] = BitConverter.ToUInt16(data, source + 2);
                }
            }

            (double lowMin, double lowMax) = GetPercentileRange(low, 0.005, 0.995);
            (double highMin, double highMax) = GetPercentileRange(high, 0.005, 0.995);

            if (highMax - highMin < 64)
                return false;

            byte[] pixels = new byte[planeWidth * detectorHeight * 4];
            MaterialType[,] materialMap = new MaterialType[planeWidth, detectorHeight];
            double[,] densityMap = new double[planeWidth, detectorHeight];

            for (int y = 0; y < detectorHeight; y++)
            {
                for (int x = 0; x < planeWidth; x++)
                {
                    int sampleIndex = y * planeWidth + x;
                    double lowNorm = Normalize(low[sampleIndex], lowMin, lowMax);
                    double highNorm = Normalize(high[sampleIndex], highMin, highMax);
                    double density = Math.Clamp(highNorm * 2.35, 0.0, 2.35);
                    MaterialType material = ClassifyDualEnergy(lowNorm, highNorm, density);
                    Color color = ColorizeDualEnergy(material, density, highNorm, lowNorm);

                    int pixelIndex = sampleIndex * 4;
                    pixels[pixelIndex] = color.B;
                    pixels[pixelIndex + 1] = color.G;
                    pixels[pixelIndex + 2] = color.R;
                    pixels[pixelIndex + 3] = 255;
                    materialMap[x, y] = material;
                    densityMap[x, y] = density;
                }
            }

            scan = new NuctechImgScan
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Model = TryExtractModel(data),
                SerialNumber = TryExtractSerial(data),
                ScanTimeText = TryExtractDateTime(data),
                Width = planeWidth,
                Height = detectorHeight,
                DataOffset = offset,
                TrailingBytes = Math.Max(0, trailing),
                Bitmap = CreateBitmap(planeWidth, detectorHeight, pixels),
                MaterialMap = materialMap,
                DensityMap = densityMap
            };

            if (rotate90Clockwise || flipHorizontal || flipVertical)
                scan = Transform(scan, rotate90Clockwise, flipHorizontal, flipVertical);

            return true;
        }

        private static int TryReadHeaderDetectorHeight(byte[] data)
        {
            if (data.Length < 64)
                return 0;

            if (data.Length >= DefaultMultiPlaneOffset + 12 && BitConverter.ToInt32(data, 56) == 3)
            {
                int containerHeight = BitConverter.ToUInt16(data, DefaultMultiPlaneOffset + 10);
                if (containerHeight is >= 128 and <= 4096)
                    return containerHeight;
            }

            if (data.Length >= 72)
            {
                int bgraHeight = BitConverter.ToUInt16(data, 70);
                if (bgraHeight is >= 128 and <= 4096)
                    return bgraHeight;
            }

            int height = BitConverter.ToInt32(data, 60);
            return height is >= 128 and <= 4096 ? height : 0;
        }

        private static (double Min, double Max) GetPercentileRange(ushort[] values, double lowPercentile, double highPercentile)
        {
            int step = Math.Max(1, values.Length / 200_000);
            var sample = new List<ushort>(values.Length / step + 1);

            for (int i = 0; i < values.Length; i += step)
                sample.Add(values[i]);

            sample.Sort();

            int lowIndex = Math.Clamp((int)(sample.Count * lowPercentile), 0, sample.Count - 1);
            int highIndex = Math.Clamp((int)(sample.Count * highPercentile), 0, sample.Count - 1);
            double min = sample[lowIndex];
            double max = sample[highIndex];

            if (max <= min)
                max = min + 1;

            return (min, max);
        }

        private static double Normalize(ushort value, double min, double max)
        {
            return Math.Clamp((value - min) / (max - min), 0.0, 1.0);
        }

        private static MaterialType ClassifyDualEnergy(double lowNorm, double highNorm, double density)
        {
            if (density < 0.10)
                return MaterialType.Air;

            double materialRatio = lowNorm - highNorm;

            if (density > 1.55 || highNorm > 0.78)
                return MaterialType.Iron;

            if (materialRatio > 0.24)
                return MaterialType.Organic;

            if (materialRatio < -0.12)
                return MaterialType.Aluminum;

            if (density > 0.55)
                return MaterialType.Inorganic;

            return MaterialType.Mixed;
        }

        private static MaterialType ClassifySingleEnergy(double density)
        {
            if (density < 0.08)
                return MaterialType.Air;

            if (density > 1.65)
                return MaterialType.Iron;

            if (density > 0.90)
                return MaterialType.Inorganic;

            if (density > 0.28)
                return MaterialType.Organic;

            return MaterialType.Mixed;
        }

        private static byte[] SmoothDetectorPattern(byte[] source, int width, int height)
        {
            byte[] result = new byte[source.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sum = 0;
                    int count = 0;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= height)
                            continue;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= width)
                                continue;

                            sum += source[yy * width + xx];
                            count++;
                        }
                    }

                    result[y * width + x] = (byte)(sum / Math.Max(1, count));
                }
            }

            return result;
        }

        private static Color ColorizeDualEnergy(MaterialType material, double density, double highNorm, double lowNorm)
        {
            if (material == MaterialType.Air)
                return Colors.White;

            double opacity = Math.Clamp(0.22 + density * 0.46, 0.0, 0.92);
            double darkness = Math.Clamp(highNorm * 0.44, 0.0, 0.72);
            Color baseColor = material switch
            {
                MaterialType.Organic => Color.FromRgb(238, 138, 48),
                MaterialType.Inorganic => Color.FromRgb(89, 176, 106),
                MaterialType.Aluminum => Color.FromRgb(72, 142, 232),
                MaterialType.Iron or MaterialType.HeavyMetal => Color.FromRgb(34, 64, 128),
                _ => Color.FromRgb(118, 160, 132)
            };

            byte r = BlendChannel(255, baseColor.R, opacity, darkness);
            byte g = BlendChannel(255, baseColor.G, opacity, darkness);
            byte b = BlendChannel(255, baseColor.B, opacity, darkness);

            return Color.FromRgb(r, g, b);
        }

        private static byte BlendChannel(byte white, byte color, double opacity, double darkening)
        {
            double blended = white * (1.0 - opacity) + color * opacity;
            blended *= 1.0 - darkening;
            return (byte)Math.Clamp(blended, 0, 255);
        }

        private static DecodeLayout CreateManualLayout(byte[] data, int detectorHeight, int offset)
        {
            offset = Math.Clamp(offset, 0, data.Length - 4);

            int usable = data.Length - offset;
            int width = usable / Math.Max(1, detectorHeight * 4);
            int trailing = usable - width * detectorHeight * 4;

            return new DecodeLayout(offset, Math.Max(1, width), detectorHeight, Math.Max(0, trailing), 0);
        }

        private static DecodeLayout DetectBestLayout(byte[] data, int detectorHeight)
        {
            int maxOffset = Math.Min(MaxAutoHeaderSearchBytes, Math.Max(0, data.Length - detectorHeight * 4));
            DecodeLayout best = new(0, Math.Max(1, data.Length / Math.Max(1, detectorHeight * 4)), detectorHeight, 0, double.MaxValue);

            for (int offset = 0; offset <= maxOffset; offset += 4)
            {
                int usable = data.Length - offset;
                int width = usable / (detectorHeight * 4);
                int trailing = usable - width * detectorHeight * 4;

                if (width < 40 || width > 3000)
                    continue;

                if (trailing >= detectorHeight * 4)
                    continue;

                double score = ScoreCandidate(data, offset, width, detectorHeight, trailing);

                if (score < best.Score)
                    best = new DecodeLayout(offset, width, detectorHeight, trailing, score);
            }

            return best;
        }

        private static double ScoreCandidate(byte[] data, int offset, int width, int height, int trailing)
        {
            // The observed Nuctech files are B/G/R/X-like.
            // The 4th channel is usually a low-valued material/class marker.
            // A good candidate therefore has a smooth, low fourth channel and non-flat BGR.
            int sampleRows = Math.Min(height, 128);
            int sampleCols = Math.Min(width, 128);
            int rowStep = Math.Max(1, height / sampleRows);
            int colStep = Math.Max(1, width / sampleCols);

            double tagSum = 0;
            double tagSq = 0;
            double colorVarSum = 0;
            double colorSum = 0;
            int count = 0;

            for (int y = 0; y < height; y += rowStep)
            {
                for (int x = 0; x < width; x += colStep)
                {
                    int index = offset + (y * width + x) * 4;
                    if (index + 3 >= data.Length)
                        continue;

                    double b = data[index];
                    double g = data[index + 1];
                    double r = data[index + 2];
                    double tag = data[index + 3];

                    tagSum += tag;
                    tagSq += tag * tag;

                    double avg = (r + g + b) / 3.0;
                    colorSum += avg;
                    colorVarSum += Math.Abs(r - avg) + Math.Abs(g - avg) + Math.Abs(b - avg);
                    count++;
                }
            }

            if (count == 0)
                return double.MaxValue;

            double tagMean = tagSum / count;
            double tagVar = Math.Max(0, tagSq / count - tagMean * tagMean);
            double tagStd = Math.Sqrt(tagVar);
            double colorMean = colorSum / count;
            double colorVariation = colorVarSum / count;

            double trailingPenalty = trailing > 8 ? 2.0 : 0.0;
            double tooFlatPenalty = colorVariation < 2.0 ? 500.0 : 0.0;
            double tooDarkPenalty = colorMean < 2.0 ? 500.0 : 0.0;

            return tagMean * 2.0 + tagStd * 1.5 + trailingPenalty + tooFlatPenalty + tooDarkPenalty;
        }

        private static MaterialType ClassifyMaterial(byte r, byte g, byte b, byte tag)
        {
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            int saturation = max - min;
            double brightness = (r + g + b) / 3.0;

            if (brightness > 238 && saturation < 18)
                return MaterialType.Air;

            if (brightness < 38)
                return MaterialType.HeavyMetal;

            if (b > r + 18 && b > g + 8)
            {
                if (brightness < 85)
                    return MaterialType.Iron;

                return MaterialType.Aluminum;
            }

            if (g > r + 10 && g >= b)
                return MaterialType.Salt;

            if (r > g + 18 && r > b + 18)
                return MaterialType.Organic;

            if (r > 170 && g > 110 && b < 120)
                return MaterialType.Sugar;

            if (saturation < 22 && brightness < 130)
                return MaterialType.HeavyMetal;

            return MaterialType.Mixed;
        }

        private static double EstimateDensity(byte r, byte g, byte b, byte tag)
        {
            double brightness = (r + g + b) / 3.0;
            double darkness = 1.0 - brightness / 255.0;
            double tagDensity = Math.Clamp(tag / 32.0, 0.0, 1.0);

            return Math.Clamp(darkness * 1.65 + tagDensity * 0.45, 0.0, 2.35);
        }

        private static WriteableBitmap CreateBitmap(int width, int height, byte[] pixels)
        {
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            return bitmap;
        }

        private static NuctechImgScan Transform(NuctechImgScan source, bool rotate90Clockwise, bool flipHorizontal, bool flipVertical)
        {
            int srcW = source.Width;
            int srcH = source.Height;
            int dstW = rotate90Clockwise ? srcH : srcW;
            int dstH = rotate90Clockwise ? srcW : srcH;

            byte[] srcPixels = new byte[srcW * srcH * 4];
            source.Bitmap.CopyPixels(srcPixels, srcW * 4, 0);

            byte[] dstPixels = new byte[dstW * dstH * 4];
            MaterialType[,] dstMaterialMap = new MaterialType[dstW, dstH];
            double[,] dstDensityMap = new double[dstW, dstH];

            for (int sy = 0; sy < srcH; sy++)
            {
                for (int sx = 0; sx < srcW; sx++)
                {
                    int dx;
                    int dy;

                    if (rotate90Clockwise)
                    {
                        dx = srcH - 1 - sy;
                        dy = sx;
                    }
                    else
                    {
                        dx = sx;
                        dy = sy;
                    }

                    if (flipHorizontal)
                        dx = dstW - 1 - dx;

                    if (flipVertical)
                        dy = dstH - 1 - dy;

                    int srcIndex = (sy * srcW + sx) * 4;
                    int dstIndex = (dy * dstW + dx) * 4;

                    dstPixels[dstIndex] = srcPixels[srcIndex];
                    dstPixels[dstIndex + 1] = srcPixels[srcIndex + 1];
                    dstPixels[dstIndex + 2] = srcPixels[srcIndex + 2];
                    dstPixels[dstIndex + 3] = 255;

                    dstMaterialMap[dx, dy] = source.MaterialMap[sx, sy];
                    dstDensityMap[dx, dy] = source.DensityMap[sx, sy];
                }
            }

            return new NuctechImgScan
            {
                FilePath = source.FilePath,
                FileName = source.FileName,
                Model = source.Model,
                SerialNumber = source.SerialNumber,
                ScanTimeText = source.ScanTimeText,
                Width = dstW,
                Height = dstH,
                DataOffset = source.DataOffset,
                TrailingBytes = source.TrailingBytes,
                Bitmap = CreateBitmap(dstW, dstH, dstPixels),
                MaterialMap = dstMaterialMap,
                DensityMap = dstDensityMap
            };
        }

        private static string TryExtractModel(byte[] data)
        {
            return ExtractFirstUnicodeString(data, s => s.Contains("CX") || s.Contains("XT") || s.Contains("NUCTECH"));
        }

        private static string TryExtractSerial(byte[] data)
        {
            return ExtractFirstUnicodeString(data, s => s.StartsWith("TFN", StringComparison.OrdinalIgnoreCase));
        }

        private static string TryExtractDateTime(byte[] data)
        {
            return ExtractFirstUnicodeString(data, s => s.Contains("-") && s.Contains(":"));
        }

        private static string ExtractFirstUnicodeString(byte[] data, Func<string, bool> predicate)
        {
            foreach (string value in ExtractUnicodeStrings(data.Take(Math.Min(data.Length, 4096)).ToArray()))
            {
                if (predicate(value))
                    return value;
            }

            return string.Empty;
        }

        private static IEnumerable<string> ExtractUnicodeStrings(byte[] data)
        {
            var builder = new StringBuilder();

            for (int i = 0; i + 1 < data.Length; i += 2)
            {
                ushort code = BitConverter.ToUInt16(data, i);
                char ch = (char)code;

                if (ch >= 32 && ch <= 126)
                {
                    builder.Append(ch);
                }
                else
                {
                    if (builder.Length >= 6)
                        yield return builder.ToString();

                    builder.Clear();
                }
            }

            if (builder.Length >= 6)
                yield return builder.ToString();
        }

        private readonly record struct DecodeLayout(int Offset, int Width, int Height, int TrailingBytes, double Score);
    }
}
