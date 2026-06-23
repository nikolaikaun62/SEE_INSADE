using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SEE_INSADE.Services
{
    public class ScanEmulationService : IScanEmulationService
    {
        private readonly Random _random = new();

        public async Task<ScanResult> PerformScanAsync(ScanParameters parameters, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            if (!ValidateScanParameters(parameters))
                throw new ArgumentException("Invalid scan parameters");

            var result = new ScanResult
            {
                ScanId = Guid.NewGuid().ToString(),
                StartTime = DateTime.Now,
                Parameters = parameters
            };

            for (int i = 0; i <= 100; i += 10)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Status = "Cancelled";
                    result.EndTime = DateTime.Now;
                    return result;
                }

                await Task.Delay(200, cancellationToken);

                var scanProgress = new ScanProgress
                {
                    Percentage = i,
                    CurrentStage = GetStageForPercentage(i),
                    DetectedObjects = i > 50 ? GenerateDetectedObjects(i) : new List<DetectedObject>()
                };

                progress.Report(scanProgress);
            }

            result.Status = "Completed";
            result.EndTime = DateTime.Now;
            result.DetectedObjects = GenerateFinalDetectedObjects();

            return result;
        }

        public bool ValidateScanParameters(ScanParameters parameters)
        {
            return parameters.EnergyLevel >= 50 && parameters.EnergyLevel <= 200 &&
                   parameters.ScanSpeed >= 1 && parameters.ScanSpeed <= 10 &&
                   parameters.Resolution >= 0.1 && parameters.Resolution <= 2.0;
        }

        private string GetStageForPercentage(int percentage)
        {
            return percentage switch
            {
                < 20 => "System Initialization",
                < 40 => "Detector Calibration",
                < 60 => "Object Scanning",
                < 80 => "Data Analysis",
                _ => "Report Generation"
            };
        }

        private List<DetectedObject> GenerateDetectedObjects(int progress)
        {
            var objects = new List<DetectedObject>();
            var count = _random.Next(1, 5);

            for (int i = 0; i < count; i++)
            {
                objects.Add(new DetectedObject
                {
                    Id = i + 1,
                    MaterialType = GetRandomMaterial(),
                    Density = _random.NextDouble() * 5 + 1,
                    PositionX = _random.NextDouble() * 100,
                    PositionY = _random.NextDouble() * 100
                });
            }

            return objects;
        }

        private List<DetectedObject> GenerateFinalDetectedObjects()
        {
            return GenerateDetectedObjects(100);
        }

        private string GetRandomMaterial()
        {
            var materials = new[] { "Metal", "Plastic", "Organic", "Glass", "Ceramic" };
            return materials[_random.Next(materials.Length)];
        }
    }
}