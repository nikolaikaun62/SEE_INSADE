using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SEE_INSADE.Services
{
    public interface IScanEmulationService
    {
        Task<ScanResult> PerformScanAsync(ScanParameters parameters, IProgress<ScanProgress> progress, CancellationToken cancellationToken);
        bool ValidateScanParameters(ScanParameters parameters);
    }

    public class ScanParameters
    {
        public double EnergyLevel { get; set; } = 100;
        public int ScanSpeed { get; set; } = 5;
        public double Resolution { get; set; } = 1.0;
        public string ScanMode { get; set; } = "Standard";
    }

    public class ScanResult
    {
        public string ScanId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public ScanParameters Parameters { get; set; } = new ScanParameters();
        public List<DetectedObject> DetectedObjects { get; set; } = new List<DetectedObject>();
    }

    public class ScanProgress
    {
        public int Percentage { get; set; }
        public string CurrentStage { get; set; } = string.Empty;
        public List<DetectedObject> DetectedObjects { get; set; } = new List<DetectedObject>();
    }

    public class DetectedObject
    {
        public int Id { get; set; }
        public string MaterialType { get; set; } = string.Empty;
        public double Density { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
    }
}