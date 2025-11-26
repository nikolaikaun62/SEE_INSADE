using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SEE_INSADE.Utilities
{
    public static class ImageHelper
    {
        public static WriteableBitmap CreateBitmap(int width, int height)
        {
            return new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
        }

        public static Color GetPixelColor(WriteableBitmap bitmap, int x, int y)
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

        public static void SetPixelColor(WriteableBitmap bitmap, int x, int y, Color color)
        {
            try
            {
                byte[] pixel = { color.B, color.G, color.R, 255 };
                bitmap.WritePixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0);
            }
            catch
            {
                // Handle error
            }
        }
    }
}