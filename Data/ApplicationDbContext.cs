using Microsoft.EntityFrameworkCore;
using SEE_INSADE.Models;
using SEE_INSADE.Services;

namespace SEE_INSADE.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Detector> Detectors { get; set; }
        public DbSet<ScanSession> ScanSessions { get; set; }
        public DbSet<DetectorMetric> DetectorMetrics { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=see_insade.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Конфигурация для Detector
            modelBuilder.Entity<Detector>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Efficiency).HasDefaultValue(100.0);
            });

            // Конфигурация для ScanSession
            modelBuilder.Entity<ScanSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ScanId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            });
        }
    }
}