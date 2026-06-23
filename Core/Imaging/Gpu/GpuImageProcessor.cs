using ComputeSharp;
using SEE_INSADE.Core.Imaging;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SEE_INSADE.Core.Imaging.Gpu
{
    public sealed class GpuImageProcessor
    {
        private readonly GraphicsDevice? _device;
        private string _lastError = string.Empty;

        public GpuImageProcessor()
        {
            try
            {
                _device = GraphicsDevice.GetDefault();
                IsAvailable = true;
                StatusText = "GPU ready: ComputeSharp DX12";
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                _lastError = ex.Message;
                StatusText = "GPU unavailable - CPU fallback";
            }
        }

        public bool IsAvailable { get; }
        public string StatusText { get; private set; }

        public bool TryCreateOperatorFilterView(
            MaterialType[,] materialMap,
            double[,] densityMap,
            int width,
            int height,
            OperatorFilterSettings settings,
            out WriteableBitmap? bitmap)
        {
            bitmap = null;

            if (_device == null || !IsAvailable)
                return false;

            if (width <= 0 || height <= 0)
                return false;

            try
            {
                int pixelCount = checked(width * height);
                int[] materials = new int[pixelCount];
                float[] densities = new float[pixelCount];
                int[] output = new int[pixelCount];

                Parallel.For(0, height, y =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        materials[index] = ToGpuMaterialCode(GetMapValue(materialMap, x, y, MaterialType.Air));
                        densities[index] = (float)Math.Clamp(GetMapValue(densityMap, x, y, 0.0), 0.0, 2.35);
                    }
                });

                using ReadOnlyBuffer<int> materialBuffer = _device.AllocateReadOnlyBuffer(materials);
                using ReadOnlyBuffer<float> densityBuffer = _device.AllocateReadOnlyBuffer(densities);
                using ReadWriteBuffer<int> outputBuffer = _device.AllocateReadWriteBuffer(output);

                _device.For(pixelCount, new GpuOperatorFilterKernel(
                    materialBuffer,
                    densityBuffer,
                    outputBuffer,
                    width,
                    height,
                    (int)settings.Mode,
                    (float)Math.Clamp(settings.Strength, 0.1, 3.0),
                    settings.BrightnessEnabled ? 1 : 0,
                    (float)Math.Clamp(settings.Brightness, 0.1, 3.0),
                    settings.ContrastEnabled ? 1 : 0,
                    (float)Math.Clamp(settings.Contrast, 0.1, 3.0),
                    settings.MaterialEnhancementEnabled ? 1 : 0,
                    settings.EdgeDetectionEnabled ? 1 : 0,
                    settings.NoiseReductionEnabled ? 1 : 0));

                outputBuffer.CopyTo(output);

                byte[] pixels = new byte[pixelCount * 4];
                Parallel.For(0, pixelCount, index =>
                {
                    int packed = output[index];
                    int pixelOffset = index * 4;
                    pixels[pixelOffset] = (byte)(packed & 0xFF);
                    pixels[pixelOffset + 1] = (byte)((packed >> 8) & 0xFF);
                    pixels[pixelOffset + 2] = (byte)((packed >> 16) & 0xFF);
                    pixels[pixelOffset + 3] = 255;
                });

                var result = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
                result.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
                bitmap = result;

                StatusText = "GPU active: ComputeSharp DX12";
                return true;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                StatusText = "GPU failed - CPU fallback";
                System.Diagnostics.Debug.WriteLine($"GPU image processing failed: {_lastError}");
                return false;
            }
        }

        private static int ToGpuMaterialCode(MaterialType material)
        {
            return material switch
            {
                MaterialType.Air => 0,
                MaterialType.Organic => 1,
                MaterialType.Sugar => 2,
                MaterialType.Plastic => 3,
                MaterialType.Liquid => 4,
                MaterialType.Inorganic => 5,
                MaterialType.Salt => 6,
                MaterialType.Glass => 7,
                MaterialType.Mixed => 8,
                MaterialType.Aluminum => 9,
                MaterialType.LightMetal => 10,
                MaterialType.Iron => 11,
                MaterialType.Electronics => 12,
                MaterialType.HeavyMetal => 13,
                MaterialType.Gold => 14,
                MaterialType.Lead => 15,
                _ => 0
            };
        }

        private static T GetMapValue<T>(T[,] map, int x, int y, T fallback) where T : notnull
        {
            if (x < 0 || y < 0 || x >= map.GetLength(0) || y >= map.GetLength(1))
                return fallback;

            return map[x, y];
        }
    }
}
