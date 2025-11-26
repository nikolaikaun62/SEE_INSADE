using SEE_INSADE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SEE_INSADE.Services
{
    public class DetectorDiagnosticService : IDetectorDiagnosticService
    {
        private readonly Random _random = new();

        public async Task<DetectorDiagnosticResult> RunDiagnosticAsync(int detectorId)
        {
            await Task.Delay(2000);

            var result = new DetectorDiagnosticResult();

            var voltageTest = new DiagnosticTest
            {
                Name = "Voltage Check",
                Value = _random.NextDouble() * 0.8 + 4.2,
                Passed = true
            };
            voltageTest.Passed = voltageTest.Value >= 4.5;
            voltageTest.Message = voltageTest.Passed ? "Voltage normal" : "Low voltage";
            result.Tests.Add(voltageTest);

            var tempTest = new DiagnosticTest
            {
                Name = "Temperature Check",
                Value = _random.NextDouble() * 40 + 20,
                Passed = true
            };
            tempTest.Passed = tempTest.Value <= 50;
            tempTest.Message = tempTest.Passed ? "Temperature normal" : "Detector overheating";
            result.Tests.Add(tempTest);

            var efficiencyTest = new DiagnosticTest
            {
                Name = "Efficiency Check",
                Value = _random.NextDouble() * 40 + 60,
                Passed = true
            };
            efficiencyTest.Passed = efficiencyTest.Value >= 75;
            efficiencyTest.Message = efficiencyTest.Passed ? "Efficiency normal" : "Low efficiency";
            result.Tests.Add(efficiencyTest);

            var calibrationTest = new DiagnosticTest
            {
                Name = "Calibration Check",
                Value = _random.NextDouble() * 20 + 80,
                Passed = true
            };
            calibrationTest.Passed = calibrationTest.Value >= 85;
            calibrationTest.Message = calibrationTest.Passed ? "Calibration normal" : "Calibration required";
            result.Tests.Add(calibrationTest);

            result.Metrics.Add("Voltage", voltageTest.Value);
            result.Metrics.Add("Temperature", tempTest.Value);
            result.Metrics.Add("Efficiency", efficiencyTest.Value);
            result.Metrics.Add("Calibration Accuracy", calibrationTest.Value);

            result.IsSuccessful = result.Tests.All(t => t.Passed);
            result.Status = result.IsSuccessful ? "Diagnostic passed" : "Issues detected";

            var recommendations = new List<string>();
            if (!voltageTest.Passed) recommendations.Add("Check power supply");
            if (!tempTest.Passed) recommendations.Add("Ensure detector cooling");
            if (!efficiencyTest.Passed) recommendations.Add("Perform calibration");
            if (!calibrationTest.Passed) recommendations.Add("Perform precise calibration");

            result.Recommendations = string.Join("; ", recommendations);

            return result;
        }

        public async Task<DetectorCalibrationResult> CalibrateDetectorAsync(int detectorId)
        {
            await Task.Delay(3000);

            var oldEfficiency = _random.NextDouble() * 30 + 60;
            var improvement = _random.NextDouble() * 15 + 5;

            return new DetectorCalibrationResult
            {
                IsSuccessful = true,
                OldEfficiency = Math.Round(oldEfficiency, 1),
                NewEfficiency = Math.Round(oldEfficiency + improvement, 1),
                Message = "Calibration completed successfully"
            };
        }

        public async Task<List<DetectorMetric>> GetDetectorMetricsAsync(int detectorId, int hoursBack = 24)
        {
            var metrics = new List<DetectorMetric>();
            var now = DateTime.Now;
            var random = new Random(detectorId);

            for (int i = hoursBack; i >= 0; i--)
            {
                var timestamp = now.AddHours(-i);
                metrics.Add(new DetectorMetric
                {
                    Timestamp = timestamp,
                    Efficiency = 80 + random.NextDouble() * 15,
                    Temperature = 25 + random.NextDouble() * 20,
                    Voltage = 4.5 + random.NextDouble() * 0.4,
                    PhotonCount = random.Next(5000, 15000)
                });
            }

            return await Task.FromResult(metrics);
        }

        public DetectorHealthStatus GetHealthStatus(EnhancedDetector detector)
        {
            var status = new DetectorHealthStatus();
            var issues = new List<string>();
            var recommendations = new List<string>();
            var score = 100;

            if (detector.Efficiency < 70)
            {
                issues.Add("Low efficiency");
                recommendations.Add("Calibration required");
                score -= 30;
            }
            else if (detector.Efficiency < 85)
            {
                issues.Add("Reduced efficiency");
                recommendations.Add("Preventive maintenance recommended");
                score -= 15;
            }

            if (detector.Temperature > 55)
            {
                issues.Add("Critical temperature");
                recommendations.Add("Immediate cooling required");
                score -= 40;
            }
            else if (detector.Temperature > 45)
            {
                issues.Add("High temperature");
                recommendations.Add("Check cooling system");
                score -= 20;
            }

            if (detector.Voltage < 4.3)
            {
                issues.Add("Low voltage");
                recommendations.Add("Check power supply");
                score -= 25;
            }

            var daysToMaintenance = (detector.NextMaintenance - DateTime.Now).TotalDays;
            if (daysToMaintenance < 7)
            {
                issues.Add("Scheduled maintenance required");
                recommendations.Add("Plan maintenance");
                score -= 10;
            }

            status.Score = Math.Max(0, score);
            status.Issues = issues;
            status.Recommendations = recommendations;

            if (score >= 90)
            {
                status.Status = "Excellent";
                status.Color = "#4CAF50";
            }
            else if (score >= 70)
            {
                status.Status = "Good";
                status.Color = "#8BC34A";
            }
            else if (score >= 50)
            {
                status.Status = "Satisfactory";
                status.Color = "#FFC107";
            }
            else
            {
                status.Status = "Requires Attention";
                status.Color = "#F44336";
            }

            return status;
        }
    }
}