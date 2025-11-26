using SEE_INSADE.Core.Filters;
using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SEE_INSADE.Core.Imaging
{
    public class ImageProcessor
    {
        //private readonly FilterPipeline _filterPipeline;

        public ImageProcessor()
        {
            //_filterPipeline = new FilterPipeline();
        }

        public WriteableBitmap ProcessImage(WriteableBitmap source, MaterialType[,] materialMap, double[,] densityMap)
        {
            // Временно возвращаем исходное изображение
            return source;

            /*
            int width = source.PixelWidth;
            int height = source.PixelHeight;
            
            var result = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            byte[] pixels = new byte[width * height * 4];

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    
                    Color originalColor = GetPixelColor(source, x, y);
                    MaterialType material = materialMap[x, y];
                    double density = densityMap[x, y];
                    
                    Color filteredColor = _filterPipeline.ApplyFilters(originalColor, material, density);
                    
                    pixels[index] = filteredColor.B;
                    pixels[index + 1] = filteredColor.G;
                    pixels[index + 2] = filteredColor.R;
                    pixels[index + 3] = 255;
                }
            });

            result.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            return result;
            */
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
            //_filterPipeline.UpdateFilters(pipeline);
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
                    Color color = GetMaterialColor(materialMap[x, y]);

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
                MaterialType.Organic => Colors.Orange,
                MaterialType.Inorganic => Colors.Blue,
                MaterialType.HeavyMetal => Colors.Red,
                MaterialType.LightMetal => Colors.LightBlue,
                MaterialType.Electronics => Colors.Purple,
                MaterialType.Plastic => Colors.Gray,
                MaterialType.Glass => Colors.Cyan,
                MaterialType.Liquid => Colors.LightBlue,
                _ => Colors.White
            };
        }

        public int GetActiveFiltersCount()
        {
            return 0; // Временно
            //return _filterPipeline.GetActiveFiltersCount();
        }
    }
}