using Microsoft.EntityFrameworkCore;
using SEE_INSADE.Data;
using SEE_INSADE.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SEE_INSADE.Services
{
    public class DetectorService : IDetectorService
    {
        private readonly ApplicationDbContext _context;

        public DetectorService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Detector>> GetDetectorsAsync()
        {
            return await _context.Detectors.ToListAsync();
        }

        public async Task<Detector> GetDetectorByIdAsync(int id)
        {
            var detector = await _context.Detectors.FindAsync(id);
            return detector ?? new Detector { Id = id, Name = "Unknown", Status = "Offline", Efficiency = 0 };
        }

        public async Task UpdateDetectorStatusAsync(int detectorId, string status, double efficiency)
        {
            var detector = await _context.Detectors.FindAsync(detectorId);
            if (detector != null)
            {
                detector.Status = status;
                detector.Efficiency = efficiency;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Detector>> GetDetectorsByStatusAsync(string status)
        {
            return await _context.Detectors
                .Where(d => d.Status == status)
                .ToListAsync();
        }
    }
}