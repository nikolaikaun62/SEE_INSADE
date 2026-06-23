namespace SEE_INSADE.Services.Scanning
{
    public sealed class Nuctech6040DDetectorConnection : INuctech6040DDetectorConnection
    {
        public bool IsConnected { get; private set; }
        public string Status { get; private set; } = "Nuctech 6040D detector driver is not configured";
        public int DetectorCount { get; private set; } = 876;

        public bool TryConnect()
        {
            IsConnected = false;
            Status = "No Nuctech 6040D detector transport configured";
            return false;
        }

        public bool TryReadLine(out NuctechDetectorLine line)
        {
            line = new NuctechDetectorLine();
            Status = "No Nuctech 6040D detector transport configured";
            return false;
        }
    }
}
