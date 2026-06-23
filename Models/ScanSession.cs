using System;

namespace SEE_INSADE.Models
{
    public class ScanSession
    {
        public int Id { get; set; }
        public string ScanId { get; set; } = string.Empty;
        public int DetectorId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = "Running";
        public string Parameters { get; set; } = string.Empty;
        public string Results { get; set; } = string.Empty;
    }
}