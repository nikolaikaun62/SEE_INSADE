using System;
using System.Collections.Generic;

namespace SEE_INSADE.Core.Emulators
{
    public class ScannerEmulator
    {
        private readonly Random _random = new Random();
        private readonly List<MaterialProfile> _materials;

        public ScannerEmulator()
        {
            _materials = CreateMaterials();
        }

        public ScannerData GenerateScanData(int width, int height)
        {
            int objectCount = _random.Next(10, 22);
            return GenerateScanData(width, height, objectCount);
        }

        public ScannerData GenerateScanData(int width, int height, int objectCount)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));

            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));

            if (objectCount < 0)
                throw new ArgumentOutOfRangeException(nameof(objectCount));

            var data = new ScannerData
            {
                MaterialMap = new MaterialType[width, height],
                DensityMap = new double[width, height],
                DetailedMaterialMap = new string[width, height],
                PhysicalDensityMap = new double[width, height],
                EffectiveZMap = new double[width, height],
                ObjectIdMap = new int[width, height],
                Timestamp = DateTime.Now
            };

            FillBackground(data, width, height);

            var objects = CreateRandomObjects(width, height, objectCount);

            foreach (var scanObject in objects)
            {
                DrawObject(data, scanObject, width, height);
            }

            AddSensorNoise(data, width, height);

            return data;
        }

        private void FillBackground(ScannerData data, int width, int height)
        {
            var air = GetMaterial("Air");

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    data.MaterialMap[x, y] = MaterialType.Air;
                    data.DensityMap[x, y] = 0.0;
                    data.DetailedMaterialMap[x, y] = air.Name;
                    data.PhysicalDensityMap[x, y] = air.GetRandomDensity(_random);
                    data.EffectiveZMap[x, y] = air.EffectiveZ;
                    data.ObjectIdMap[x, y] = 0;
                }
            }
        }

        private List<ScanObject> CreateRandomObjects(int imageWidth, int imageHeight, int count)
        {
            var result = new List<ScanObject>();

            for (int i = 1; i <= count; i++)
            {
                result.Add(CreateRandomObject(i, imageWidth, imageHeight));
            }

            return result;
        }

        private ScanObject CreateRandomObject(int id, int imageWidth, int imageHeight)
        {
            var scanObject = new ScanObject
            {
                Id = id,
                Shape = (ShapeKind)_random.Next(0, 3),
                CenterX = RandomRange(imageWidth * 0.08, imageWidth * 0.92),
                CenterY = RandomRange(imageHeight * 0.08, imageHeight * 0.92),
                Width = RandomRange(imageWidth * 0.06, imageWidth * 0.25),
                Height = RandomRange(imageHeight * 0.05, imageHeight * 0.22),
                RotationRad = RandomRange(0, Math.PI)
            };

            int scenario = _random.Next(0, 10);

            switch (scenario)
            {
                case 0:
                    CreateSugarOrSaltPacket(scanObject);
                    break;

                case 1:
                    CreateMetalObject(scanObject);
                    break;

                case 2:
                    CreateElectronics(scanObject);
                    break;

                case 3:
                    CreateGlassBottle(scanObject);
                    break;

                case 4:
                    CreateCable(scanObject);
                    break;

                case 5:
                    CreateOrganicObject(scanObject);
                    break;

                case 6:
                    CreateCeramicObject(scanObject);
                    break;

                case 7:
                    CreateBookOrPaper(scanObject);
                    break;

                case 8:
                    CreateBattery(scanObject);
                    break;

                default:
                    CreateMixedContainer(scanObject);
                    break;
            }

            return scanObject;
        }

        private void CreateSugarOrSaltPacket(ScanObject scanObject)
        {
            scanObject.Shape = ShapeKind.Rectangle;

            string content = _random.NextDouble() < 0.5 ? "Sugar" : "Salt";

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.00,
                ToRadius = 0.82,
                Components = new List<MaterialComponent>
                {
                    Component(content, 1.0)
                }
            });

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.82,
                ToRadius = 1.00,
                Components = new List<MaterialComponent>
                {
                    Component("Plastic", 1.0)
                }
            });
        }

        private void CreateMetalObject(ScanObject scanObject)
        {
            scanObject.Shape = _random.NextDouble() < 0.5 ? ShapeKind.Rectangle : ShapeKind.Capsule;
            scanObject.Width *= 0.75;
            scanObject.Height *= 0.45;

            string metal = GetRandomFrom("Aluminum", "Steel", "Copper", "Lead");

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.00,
                ToRadius = 1.00,
                Components = new List<MaterialComponent>
                {
                    Component(metal, 1.0)
                }
            });
        }

        private void CreateElectronics(ScanObject scanObject)
        {
            scanObject.Shape = ShapeKind.Rectangle;

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.00,
                ToRadius = 0.70,
                Components = new List<MaterialComponent>
                {
                    Component("Plastic", 0.35),
                    Component("Copper", 0.25),
                    Component("Steel", 0.15),
                    Component("Lithium Battery", 0.25)
                }
            });

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.70,
                ToRadius = 1.00,
                Components = new List<MaterialComponent>
                {
                    Component("Plastic", 1.0)
                }
            });
        }

        private void CreateGlassBottle(ScanObject scanObject)
        {
            scanObject.Shape = ShapeKind.Capsule;
            scanObject.Width *= 0.70;
            scanObject.Height *= 1.35;

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.00,
                ToRadius = 0.72,
                Components = new List<MaterialComponent>
                {
                    Component("Water", 0.70),
                    Component("Sugar", 0.30)
                }
            });

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.72,
                ToRadius = 1.00,
                Components = new List<MaterialComponent>
                {
                    Component("Glass", 1.0)
                }
            });
        }

        private void CreateCable(ScanObject scanObject)
        {
            scanObject.Shape = ShapeKind.Capsule;
            scanObject.Width *= 1.60;
            scanObject.Height *= 0.25;

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.00,
                ToRadius = 0.42,
                Components = new List<MaterialComponent>
                {
                    Component("Copper", 1.0)
                }
            });

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.42,
                ToRadius = 1.00,
                Components = new List<MaterialComponent>
                {
                    Component("Rubber", 1.0)
                }
            });
        }

        private void CreateOrganicObject(ScanObject scanObject)
        {
            scanObject.Shape = ShapeKind.Ellipse;

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.00,
                ToRadius = 1.00,
                Components = new List<MaterialComponent>
                {
                    Component("Organic", 0.55),
                    Component("Water", 0.25),
                    Component("Sugar", 0.20)
                }
            });
        }

        private void CreateCeramicObject(ScanObject scanObject)
        {
            scanObject.Shape = ShapeKind.Ellipse;

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.00,
                ToRadius = 1.00,
                Components = new List<MaterialComponent>
                {
                    Component("Ceramic", 0.75),
                    Component("Glass", 0.15),
                    Component("Salt", 0.10)
                }
            });
        }

        private void CreateBookOrPaper(ScanObject scanObject)
        {
            scanObject.Shape = ShapeKind.Rectangle;
            scanObject.Width *= 1.25;
            scanObject.Height *= 0.65;

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.00,
                ToRadius = 1.00,
                Components = new List<MaterialComponent>
                {
                    Component("Paper", 0.85),
                    Component("Plastic", 0.15)
                }
            });
        }

        private void CreateBattery(ScanObject scanObject)
        {
            scanObject.Shape = ShapeKind.Capsule;
            scanObject.Width *= 0.80;
            scanObject.Height *= 0.38;

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.00,
                ToRadius = 0.72,
                Components = new List<MaterialComponent>
                {
                    Component("Lithium Battery", 0.75),
                    Component("Copper", 0.15),
                    Component("Plastic", 0.10)
                }
            });

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.72,
                ToRadius = 1.00,
                Components = new List<MaterialComponent>
                {
                    Component("Aluminum", 1.0)
                }
            });
        }

        private void CreateMixedContainer(ScanObject scanObject)
        {
            scanObject.Shape = ShapeKind.Capsule;

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.00,
                ToRadius = 0.75,
                Components = new List<MaterialComponent>
                {
                    Component("Water", 0.45),
                    Component("Organic", 0.35),
                    Component("Salt", 0.20)
                }
            });

            scanObject.Layers.Add(new MaterialLayer
            {
                FromRadius = 0.75,
                ToRadius = 1.00,
                Components = new List<MaterialComponent>
                {
                    Component("Aluminum", 1.0)
                }
            });
        }

        private void DrawObject(ScannerData data, ScanObject scanObject, int imageWidth, int imageHeight)
        {
            double radius = Math.Max(scanObject.Width, scanObject.Height);

            int minX = ClampToInt(scanObject.CenterX - radius, 0, imageWidth - 1);
            int maxX = ClampToInt(scanObject.CenterX + radius, 0, imageWidth - 1);
            int minY = ClampToInt(scanObject.CenterY - radius, 0, imageHeight - 1);
            int maxY = ClampToInt(scanObject.CenterY + radius, 0, imageHeight - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!scanObject.Contains(x, y))
                        continue;

                    double normalizedRadius = scanObject.GetNormalizedRadius(x, y);
                    MaterialLayer? layer = scanObject.GetLayer(normalizedRadius);

                    if (layer == null)
                        continue;

                    MaterialSample sample = SampleLayer(layer, normalizedRadius, scanObject.Shape);

                    // Если объекты пересекаются, сверху отображаем более плотный.
                    if (sample.ImageDensity >= data.DensityMap[x, y])
                    {
                        data.MaterialMap[x, y] = sample.Category;
                        data.DensityMap[x, y] = sample.ImageDensity;
                        data.DetailedMaterialMap[x, y] = sample.Name;
                        data.PhysicalDensityMap[x, y] = sample.PhysicalDensity;
                        data.EffectiveZMap[x, y] = sample.EffectiveZ;
                        data.ObjectIdMap[x, y] = scanObject.Id;
                    }
                }
            }
        }

        private MaterialSample SampleLayer(MaterialLayer layer, double normalizedRadius, ShapeKind shape)
        {
            double totalWeight = 0.0;

            foreach (var component in layer.Components)
            {
                totalWeight += component.Weight;
            }

            double physicalDensity = 0.0;
            double effectiveZ = 0.0;
            double absorption = 0.0;

            var dominantMaterial = layer.Components[0].Material;
            double dominantWeight = layer.Components[0].Weight;

            foreach (var component in layer.Components)
            {
                double weight = component.Weight / totalWeight;
                MaterialProfile material = component.Material;

                physicalDensity += material.GetRandomDensity(_random) * weight;
                effectiveZ += material.EffectiveZ * weight;
                absorption += material.AbsorptionCoefficient * weight;

                if (component.Weight > dominantWeight)
                {
                    dominantMaterial = component.Material;
                    dominantWeight = component.Weight;
                }
            }

            double thicknessFactor = GetThicknessFactor(normalizedRadius, shape);
            double noise = 1.0 + NextGaussian(0.0, 0.045);

            physicalDensity = Math.Max(0.0, physicalDensity * noise);

            double imageDensity = physicalDensity * absorption * thicknessFactor / 18.0;
            imageDensity = Clamp(imageDensity, 0.0, 1.0);

            string name;

            if (layer.Components.Count == 1)
            {
                name = dominantMaterial.Name;
            }
            else
            {
                name = "Mixed: " + dominantMaterial.Name;
            }

            return new MaterialSample
            {
                Name = name,
                Category = ClassifyMaterial(dominantMaterial, effectiveZ, physicalDensity),
                PhysicalDensity = physicalDensity,
                EffectiveZ = effectiveZ,
                ImageDensity = imageDensity
            };
        }

        private MaterialType ClassifyMaterial(MaterialProfile dominantMaterial, double effectiveZ, double density)
        {
            if (dominantMaterial.Category == MaterialType.Plastic)
                return MaterialType.Plastic;

            if (dominantMaterial.Category == MaterialType.Glass)
                return MaterialType.Glass;

            if (dominantMaterial.Category == MaterialType.Organic)
                return MaterialType.Organic;

            if (dominantMaterial.Category == MaterialType.Air)
                return MaterialType.Air;

            // Так как в твоём enum пока нет Metal,
            // металлы и соли относятся к Inorganic.
            if (effectiveZ >= 10.0 || density >= 1.8)
                return MaterialType.Inorganic;

            return dominantMaterial.Category;
        }

        private double GetThicknessFactor(double normalizedRadius, ShapeKind shape)
        {
            normalizedRadius = Clamp(normalizedRadius, 0.0, 1.0);

            if (shape == ShapeKind.Rectangle)
                return RandomRange(0.85, 1.05);

            double roundedThickness = Math.Sqrt(Math.Max(0.0, 1.0 - normalizedRadius * normalizedRadius));

            return 0.25 + roundedThickness * 0.85;
        }

        private void AddSensorNoise(ScannerData data, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double noise = NextGaussian(0.0, 0.010);
                    data.DensityMap[x, y] = Clamp(data.DensityMap[x, y] + noise, 0.0, 1.0);
                }
            }
        }

        private List<MaterialProfile> CreateMaterials()
        {
            return new List<MaterialProfile>
            {
                Material("Air", MaterialType.Air, 0.001, 0.002, 7.3, 0.02),

                Material("Organic", MaterialType.Organic, 0.70, 1.30, 6.5, 0.85),
                Material("Water", MaterialType.Organic, 0.95, 1.05, 7.4, 0.95),
                Material("Sugar", MaterialType.Organic, 1.45, 1.65, 6.8, 1.10),
                Material("Paper", MaterialType.Organic, 0.60, 1.20, 6.6, 0.75),
                Material("Rubber", MaterialType.Organic, 0.90, 1.30, 6.8, 0.90),

                Material("Plastic", MaterialType.Plastic, 0.85, 1.45, 6.7, 0.88),

                Material("Salt", MaterialType.Inorganic, 1.90, 2.25, 14.0, 1.70),
                Material("Ceramic", MaterialType.Inorganic, 2.20, 3.80, 12.5, 1.85),

                Material("Glass", MaterialType.Glass, 2.20, 2.80, 11.5, 1.65),

                Material("Aluminum", MaterialType.Inorganic, 2.60, 2.85, 13.0, 2.40),
                Material("Steel", MaterialType.Inorganic, 7.40, 8.10, 26.0, 5.50),
                Material("Copper", MaterialType.Inorganic, 8.70, 9.10, 29.0, 6.20),
                Material("Lead", MaterialType.Inorganic, 10.80, 11.40, 82.0, 9.50),

                Material("Lithium Battery", MaterialType.Inorganic, 1.80, 3.20, 11.0, 2.20)
            };
        }

        private MaterialProfile Material(
            string name,
            MaterialType category,
            double minDensity,
            double maxDensity,
            double effectiveZ,
            double absorptionCoefficient)
        {
            return new MaterialProfile
            {
                Name = name,
                Category = category,
                MinDensity = minDensity,
                MaxDensity = maxDensity,
                EffectiveZ = effectiveZ,
                AbsorptionCoefficient = absorptionCoefficient
            };
        }

        private MaterialComponent Component(string materialName, double weight)
        {
            return new MaterialComponent
            {
                Material = GetMaterial(materialName),
                Weight = weight
            };
        }

        private MaterialProfile GetMaterial(string name)
        {
            foreach (var material in _materials)
            {
                if (material.Name == name)
                    return material;
            }

            throw new InvalidOperationException("Material not found: " + name);
        }

        private string GetRandomFrom(params string[] values)
        {
            return values[_random.Next(values.Length)];
        }

        private double RandomRange(double min, double max)
        {
            return min + _random.NextDouble() * (max - min);
        }

        private double NextGaussian(double mean, double standardDeviation)
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();

            double randStdNormal =
                Math.Sqrt(-2.0 * Math.Log(u1)) *
                Math.Sin(2.0 * Math.PI * u2);

            return mean + standardDeviation * randStdNormal;
        }

        private double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        private int ClampToInt(double value, int min, int max)
        {
            int intValue = (int)Math.Round(value);

            if (intValue < min)
                return min;

            if (intValue > max)
                return max;

            return intValue;
        }
    }

    public class ScannerData
    {
        public MaterialType[,] MaterialMap { get; set; } = null!;

        // Нормализованная плотность для отображения: 0.0 - 1.0.
        public double[,] DensityMap { get; set; } = null!;

        // Детальный материал: Steel, Copper, Salt, Sugar, Battery и т.д.
        public string[,] DetailedMaterialMap { get; set; } = null!;

        // Условная физическая плотность, примерно г/см³.
        public double[,] PhysicalDensityMap { get; set; } = null!;

        // Условный эффективный атомный номер.
        public double[,] EffectiveZMap { get; set; } = null!;

        // ID предмета. 0 — фон.
        public int[,] ObjectIdMap { get; set; } = null!;

        public DateTime Timestamp { get; set; }
    }

    public enum ShapeKind
    {
        Rectangle,
        Ellipse,
        Capsule
    }

    public class ScanObject
    {
        public int Id { get; set; }

        public ShapeKind Shape { get; set; }

        public double CenterX { get; set; }

        public double CenterY { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double RotationRad { get; set; }

        public List<MaterialLayer> Layers { get; set; } = new List<MaterialLayer>();

        public bool Contains(double x, double y)
        {
            ToLocal(x, y, out double localX, out double localY);

            double halfW = Width / 2.0;
            double halfH = Height / 2.0;

            switch (Shape)
            {
                case ShapeKind.Rectangle:
                    return Math.Abs(localX) <= halfW && Math.Abs(localY) <= halfH;

                case ShapeKind.Ellipse:
                    return Math.Pow(localX / halfW, 2) + Math.Pow(localY / halfH, 2) <= 1.0;

                case ShapeKind.Capsule:
                    return ContainsCapsule(localX, localY);

                default:
                    return false;
            }
        }

        public double GetNormalizedRadius(double x, double y)
        {
            ToLocal(x, y, out double localX, out double localY);

            double halfW = Width / 2.0;
            double halfH = Height / 2.0;

            switch (Shape)
            {
                case ShapeKind.Rectangle:
                    return Math.Max(Math.Abs(localX) / halfW, Math.Abs(localY) / halfH);

                case ShapeKind.Ellipse:
                    return Math.Sqrt(Math.Pow(localX / halfW, 2) + Math.Pow(localY / halfH, 2));

                case ShapeKind.Capsule:
                    return GetCapsuleRadius(localX, localY);

                default:
                    return 1.0;
            }
        }

        public MaterialLayer? GetLayer(double normalizedRadius)
        {
            foreach (var layer in Layers)
            {
                if (normalizedRadius >= layer.FromRadius && normalizedRadius <= layer.ToRadius)
                    return layer;
            }

            return null;
        }

        private void ToLocal(double x, double y, out double localX, out double localY)
        {
            double dx = x - CenterX;
            double dy = y - CenterY;

            double cos = Math.Cos(-RotationRad);
            double sin = Math.Sin(-RotationRad);

            localX = dx * cos - dy * sin;
            localY = dx * sin + dy * cos;
        }

        private bool ContainsCapsule(double localX, double localY)
        {
            double radius = Height / 2.0;

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

    public class MaterialLayer
    {
        public double FromRadius { get; set; }

        public double ToRadius { get; set; }

        public List<MaterialComponent> Components { get; set; } = new List<MaterialComponent>();
    }

    public class MaterialComponent
    {
        public MaterialProfile Material { get; set; } = null!;

        public double Weight { get; set; }
    }

    public class MaterialProfile
    {
        public string Name { get; set; } = string.Empty;

        public MaterialType Category { get; set; }

        public double MinDensity { get; set; }

        public double MaxDensity { get; set; }

        public double EffectiveZ { get; set; }

        public double AbsorptionCoefficient { get; set; }

        public double GetRandomDensity(Random random)
        {
            return MinDensity + random.NextDouble() * (MaxDensity - MinDensity);
        }
    }

    public class MaterialSample
    {
        public string Name { get; set; } = string.Empty;

        public MaterialType Category { get; set; }

        public double PhysicalDensity { get; set; }

        public double EffectiveZ { get; set; }

        public double ImageDensity { get; set; }
    }
}