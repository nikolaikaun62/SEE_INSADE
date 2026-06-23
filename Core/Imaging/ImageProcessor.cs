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
