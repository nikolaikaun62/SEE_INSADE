using System;

namespace SEE_INSADE.Core.Emulators
{
    public class ScannerEmulator
    {
        private Random _random = new Random();

        public ScannerData GenerateScanData(int width, int height)
        {
            var data = new ScannerData
            {
                MaterialMap = new MaterialType[width, height],
                DensityMap = new double[width, height],
                Timestamp = DateTime.Now
            };

            // Generate random test data
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    data.MaterialMap[x, y] = GetRandomMaterial();
                    data.DensityMap[x, y] = _random.NextDouble();
                }
            }

            return data;
        }

        private MaterialType GetRandomMaterial()
        {
            var materials = new[] {
                MaterialType.Air,
                MaterialType.Organic,
                MaterialType.Inorganic,
                MaterialType.Plastic,
                MaterialType.Glass
            };

            return materials[_random.Next(materials.Length)];
        }
    }

    public class ScannerData
    {
        public MaterialType[,] MaterialMap { get; set; } = null!;
        public double[,] DensityMap { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}