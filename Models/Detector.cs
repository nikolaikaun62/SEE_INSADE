using System;
using System.ComponentModel.DataAnnotations;

namespace SEE_INSADE.Models
{
    public class Detector
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Выключен";

        [Range(0, 100)]
        public double Efficiency { get; set; } = 100.0;

        public string Location { get; set; } = string.Empty;
        public DateTime LastMaintenance { get; set; } = DateTime.Now;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}