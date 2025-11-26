using SEE_INSADE.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SEE_INSADE.Services
{
    public interface IDetectorService
    {
        Task<List<Detector>> GetDetectorsAsync();
        Task<Detector> GetDetectorByIdAsync(int id);
        Task UpdateDetectorStatusAsync(int detectorId, string status, double efficiency);
        Task<List<Detector>> GetDetectorsByStatusAsync(string status);
    }
}