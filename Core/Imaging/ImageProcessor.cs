using SEE_INSADE.Core.Filters;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SEE_INSADE.Core.Imaging
{
    public class ImageProcessor
    {
        private FilterPipeline _filterPipeline;

        public ImageProcessor()
        {
            _filterPipeline = new FilterPipeline();
        }

        public WriteableBitmap ProcessImage(WriteableBitmap source, MaterialType[,] materialMap, double[,] densityMap)
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;
            var result = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            byte[] pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    Color originalColor = GetPixelColor(source, x, y);
                    MaterialType material = GetMapValue(materialMap, x, y, MaterialType.Unknown);
                    double density = GetMapValue(densityMap, x, y, 0);
                    Color filteredColor = _filterPipeline.ApplyFilters(originalColor, material, density);

                    pixels[index] = filteredColor.B;
                    pixels[index + 1] = filteredColor.G;
                    pixels[index + 2] = filteredColor.R;
                    pixels[index + 3] = 255;
                }
            }

            result.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            return result;
        }

        private Color GetPixelColor(WriteableBitmap bitmap, int x, int y)
        {
            try
            {
                byte[] pixel = new byte[4];
                bitmap.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0);
                return Color.FromRgb(pixel[2], pixel[1], pixel[0]);
            }
            catch
            {
                return Colors.Black;
            }
        }

        public void UpdateFilters(FilterPipeline pipeline)
        {
            _filterPipeline = pipeline;
        }

        public WriteableBitmap CreateMaterialMap(MaterialType[,] materialMap, int width, int height)
        {
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            byte[] pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    Color color = GetMaterialColor(GetMapValue(materialMap, x, y, MaterialType.Unknown));

                    pixels[index] = color.B;
                    pixels[index + 1] = color.G;
                    pixels[index + 2] = color.R;
                    pixels[index + 3] = 255;
                }
            }

            bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            return bitmap;
        }

        public WriteableBitmap CreateColorizedXray(MaterialType[,] materialMap, double[,] densityMap, int width, int height)
        {
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            byte[] pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    MaterialType material = GetMapValue(materialMap, x, y, MaterialType.Air);
                    double density = GetMapValue(densityMap, x, y, 0);
                    Color color = GetXrayMaterialColor(material, density);

                    pixels[index] = color.B;
                    pixels[index + 1] = color.G;
                    pixels[index + 2] = color.R;
                    pixels[index + 3] = 255;
                }
            }

            bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            return bitmap;
        }

        public WriteableBitmap CreateOperatorFilterView(
            MaterialType[,] materialMap,
            double[,] densityMap,
            int width,
            int height,
            OperatorFilterMode mode,
            double intensity)
        {
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            byte[] pixels = new byte[width * height * 4];
            intensity = Math.Clamp(intensity, 0.1, 3.0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    MaterialType material = GetMapValue(materialMap, x, y, MaterialType.Air);
                    double density = Math.Clamp(GetMapValue(densityMap, x, y, 0), 0.0, 1.5);
                    Color color = GetOperatorFilterColor(materialMap, densityMap, x, y, material, density, mode, intensity);

                    pixels[index] = color.B;
                    pixels[index + 1] = color.G;
                    pixels[index + 2] = color.R;
                    pixels[index + 3] = 255;
                }
            }

            bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            return bitmap;
        }

        private Color GetMaterialColor(MaterialType material)
        {
            return material switch
            {
                MaterialType.Organic => Color.FromRgb(238, 138, 48),
                MaterialType.Plastic => Color.FromRgb(245, 167, 62),
                MaterialType.Liquid => Color.FromRgb(235, 183, 76),
                MaterialType.Inorganic => Color.FromRgb(56, 126, 220),
                MaterialType.Glass => Color.FromRgb(73, 165, 220),
                MaterialType.LightMetal => Color.FromRgb(36, 101, 194),
                MaterialType.HeavyMetal => Color.FromRgb(20, 38, 86),
                MaterialType.Electronics => Color.FromRgb(50, 150, 116),
                MaterialType.Mixed => Color.FromRgb(76, 165, 82),
                MaterialType.Air => Colors.White,
                _ => Color.FromRgb(245, 248, 252)
            };
        }

        private Color GetXrayMaterialColor(MaterialType material, double density)
        {
            if (material == MaterialType.Air || material == MaterialType.Unknown)
                return Colors.White;

            Color baseColor = GetMaterialColor(material);
            double opacity = Math.Clamp(0.32 + density * 0.68, 0.0, 1.0);
            double darkening = material is MaterialType.HeavyMetal or MaterialType.LightMetal
                ? Math.Clamp(density * 0.55, 0.0, 0.68)
                : Math.Clamp(density * 0.28, 0.0, 0.42);

            byte r = BlendChannel(255, baseColor.R, opacity, darkening);
            byte g = BlendChannel(255, baseColor.G, opacity, darkening);
            byte b = BlendChannel(255, baseColor.B, opacity, darkening);

            return Color.FromRgb(r, g, b);
        }

        private Color GetOperatorFilterColor(
            MaterialType[,] materialMap,
            double[,] densityMap,
            int x,
            int y,
            MaterialType material,
            double density,
            OperatorFilterMode mode,
            double intensity)
        {
            Color baseColor = GetXrayMaterialColor(material, density);

            return mode switch
            {
                OperatorFilterMode.EnhancedColor => BoostSaturation(baseColor, 0.35 * intensity),
                OperatorFilterMode.HighPenetration => HighPenetrationColor(material, density, intensity),
                OperatorFilterMode.OrganicFocus => FocusMaterial(baseColor, material is MaterialType.Organic or MaterialType.Plastic or MaterialType.Liquid, intensity),
                OperatorFilterMode.InorganicFocus => FocusMaterial(baseColor, material is MaterialType.Inorganic or MaterialType.Glass or MaterialType.LightMetal, intensity),
                OperatorFilterMode.MetalFocus => FocusMaterial(baseColor, material is MaterialType.LightMetal or MaterialType.HeavyMetal or MaterialType.Electronics, intensity),
                OperatorFilterMode.DensityMap => DensityColor(density),
                OperatorFilterMode.Negative => Color.FromRgb((byte)(255 - baseColor.R), (byte)(255 - baseColor.G), (byte)(255 - baseColor.B)),
                OperatorFilterMode.Threshold => DensityThreshold(material, density, intensity),
                OperatorFilterMode.EdgeEmphasis => EdgeColor(materialMap, densityMap, x, y, baseColor, intensity),
                OperatorFilterMode.SuspectHighlight => SuspectHighlightColor(baseColor, material, density, intensity),
                _ => baseColor
            };
        }

        private static Color FocusMaterial(Color input, bool isTarget, double intensity)
        {
            if (isTarget)
                return BoostSaturation(input, 0.55 * intensity);

            byte gray = ToGray(input);
            byte faded = (byte)Math.Clamp(gray + 38, 0, 255);
            return Color.FromRgb(faded, faded, faded);
        }

        private Color HighPenetrationColor(MaterialType material, double density, double intensity)
        {
            if (material == MaterialType.Air || material == MaterialType.Unknown)
                return Colors.White;

            double penetration = Math.Clamp(density * intensity, 0.0, 1.0);
            byte shade = (byte)Math.Clamp(255 - penetration * 235, 0, 255);

            if (material is MaterialType.HeavyMetal or MaterialType.LightMetal)
                return Color.FromRgb((byte)(shade * 0.35), (byte)(shade * 0.45), shade);

            return Color.FromRgb(shade, shade, shade);
        }

        private static Color DensityColor(double density)
        {
            double normalized = Math.Clamp(density / 1.5, 0.0, 1.0);
            byte value = (byte)Math.Clamp(255 - normalized * 255, 0, 255);
            return Color.FromRgb(value, value, value);
        }

        private static Color DensityThreshold(MaterialType material, double density, double intensity)
        {
            if (material == MaterialType.Air || material == MaterialType.Unknown)
                return Colors.White;

            double threshold = Math.Clamp(0.22 + intensity * 0.18, 0.24, 0.82);
            return density >= threshold ? Colors.Black : Color.FromRgb(230, 234, 240);
        }

        private Color EdgeColor(MaterialType[,] materialMap, double[,] densityMap, int x, int y, Color baseColor, double intensity)
        {
            double center = GetMapValue(densityMap, x, y, 0);
            double right = GetMapValue(densityMap, x + 1, y, center);
            double down = GetMapValue(densityMap, x, y + 1, center);
            MaterialType material = GetMapValue(materialMap, x, y, MaterialType.Air);
            MaterialType rightMaterial = GetMapValue(materialMap, x + 1, y, material);
            MaterialType downMaterial = GetMapValue(materialMap, x, y + 1, material);

            double edge = Math.Abs(center - right) + Math.Abs(center - down);
            if (rightMaterial != material)
                edge += 0.45;
            if (downMaterial != material)
                edge += 0.45;

            edge = Math.Clamp(edge * intensity, 0.0, 1.0);
            Color edgeColor = Color.FromRgb(20, 30, 45);
            return edge > 0.18 ? Blend(baseColor, edgeColor, edge) : Blend(baseColor, Colors.White, 0.15);
        }

        private static Color SuspectHighlightColor(Color baseColor, MaterialType material, double density, double intensity)
        {
            bool suspect = material is MaterialType.Organic or MaterialType.HeavyMetal or MaterialType.Electronics && density > 0.55;
            if (!suspect)
                return FocusMaterial(baseColor, false, intensity);

            Color alert = material == MaterialType.Organic
                ? Color.FromRgb(255, 112, 31)
                : Color.FromRgb(230, 32, 58);

            return Blend(baseColor, alert, Math.Clamp(0.35 + density * 0.45, 0.0, 0.85));
        }

        private static Color BoostSaturation(Color input, double amount)
        {
            byte gray = ToGray(input);
            return Color.FromRgb(
                (byte)Math.Clamp(gray + (input.R - gray) * (1.0 + amount), 0, 255),
                (byte)Math.Clamp(gray + (input.G - gray) * (1.0 + amount), 0, 255),
                (byte)Math.Clamp(gray + (input.B - gray) * (1.0 + amount), 0, 255));
        }

        private static byte ToGray(Color input)
        {
            return (byte)Math.Clamp(input.R * 0.299 + input.G * 0.587 + input.B * 0.114, 0, 255);
        }

        private static Color Blend(Color first, Color second, double secondAmount)
        {
            secondAmount = Math.Clamp(secondAmount, 0.0, 1.0);
            double firstAmount = 1.0 - secondAmount;
            return Color.FromRgb(
                (byte)Math.Clamp(first.R * firstAmount + second.R * secondAmount, 0, 255),
                (byte)Math.Clamp(first.G * firstAmount + second.G * secondAmount, 0, 255),
                (byte)Math.Clamp(first.B * firstAmount + second.B * secondAmount, 0, 255));
        }

        private static byte BlendChannel(byte background, byte foreground, double opacity, double darkening)
        {
            double value = background * (1.0 - opacity) + foreground * opacity;
            value *= 1.0 - darkening;
            return (byte)Math.Clamp(value, 0, 255);
        }

        public int GetActiveFiltersCount()
        {
            return _filterPipeline.GetActiveFiltersCount();
        }

        private static T GetMapValue<T>(T[,] map, int x, int y, T fallback)
        {
            if (x < 0 || y < 0 || x >= map.GetLength(0) || y >= map.GetLength(1))
                return fallback;

            return map[x, y];
        }
    }
}
