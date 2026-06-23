using System;

namespace SEE_INSADE.Services.Scanning
{
    public interface INuctech6040DDetectorConnection
    {
        bool IsConnected { get; }
        string Status { get; }
        int DetectorCount { get; }
        bool TryConnect();
        bool TryReadLine(out NuctechDetectorLine line);
    }

    public sealed class NuctechDetectorLine
    {
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public double[] LowEnergy { get; init; } = Array.Empty<double>();
        public double[] HighEnergy { get; init; } = Array.Empty<double>();
    }
}
