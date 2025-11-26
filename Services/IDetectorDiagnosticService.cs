using SEE_INSADE.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SEE_INSADE.Services
{
    public interface IDetectorDiagnosticService
    {
        Task<DetectorDiagnosticResult> RunDiagnosticAsync(int detectorId);
        Task<DetectorCalibrationResult> CalibrateDetectorAsync(int detectorId);
        Task<List<DetectorMetric>> GetDetectorMetricsAsync(int detectorId, int hoursBack = 24);
        DetectorHealthStatus GetHealthStatus(EnhancedDetector detector);
    }

    public class DetectorDiagnosticResult
    {
        public bool IsSuccessful { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<DiagnosticTest> Tests { get; set; } = new List<DiagnosticTest>();
        public Dictionary<string, double> Metrics { get; set; } = new Dictionary<string, double>();
        public string Recommendations { get; set; } = string.Empty;
    }

    public class DiagnosticTest
    {
        public string Name { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string Message { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    public class DetectorCalibrationResult
    {
        public bool IsSuccessful { get; set; }
        public double OldEfficiency { get; set; }
        public double NewEfficiency { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class DetectorMetric
    {
        public System.DateTime Timestamp { get; set; }
        public double Efficiency { get; set; }
        public double Temperature { get; set; }
        public double Voltage { get; set; }
        public int PhotonCount { get; set; }
    }

    public class DetectorHealthStatus
    {
        public string Status { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Score { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
    }
}