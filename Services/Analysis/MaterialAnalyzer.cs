using System;

namespace SEE_INSADE.Services.Analysis
{
    public class MaterialAnalyzer
    {
        public AnalysisResult AnalyzeMaterial(MaterialType[,] materialMap, double[,] densityMap)
        {
            int width = materialMap.GetLength(0);
            int height = materialMap.GetLength(1);

            var result = new AnalysisResult();

            // Count materials
            var materialCounts = new System.Collections.Generic.Dictionary<MaterialType, int>();
            double totalDensity = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var material = materialMap[x, y];
                    if (materialCounts.ContainsKey(material))
                        materialCounts[material]++;
                    else
                        materialCounts[material] = 1;

                    totalDensity += densityMap[x, y];
                }
            }

            result.MaterialDistribution = materialCounts;
            result.AverageDensity = totalDensity / (width * height);
            result.Timestamp = DateTime.Now;

            return result;
        }
    }

    public class AnalysisResult
    {
        public System.Collections.Generic.Dictionary<MaterialType, int> MaterialDistribution { get; set; } = null!;
        public double AverageDensity { get; set; }
        public DateTime Timestamp { get; set; }
    }
}