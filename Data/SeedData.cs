using SEE_INSADE.Models;
using System.Linq;

namespace SEE_INSADE.Data
{
    public static class SeedData
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.EnsureCreated();

            // Проверяем, есть ли уже детекторы в базе
            if (context.Detectors.Any())
            {
                return; // База уже была заполнена
            }

            var detectors = new[]
            {
                new Detector { Name = "Детектор #1-XRay", Status = "Работает", Efficiency = 94.5, Location = "Зона A" },
                new Detector { Name = "Детектор #2-Gamma", Status = "Работает", Efficiency = 87.2, Location = "Зона B" },
                new Detector { Name = "Детектор #3-Neutron", Status = "Обслуживание", Efficiency = 65.8, Location = "Зона C" },
                new Detector { Name = "Детектор #4-XRay", Status = "Неисправен", Efficiency = 23.1, Location = "Зона A" },
                new Detector { Name = "Детектор #5-Gamma", Status = "Работает", Efficiency = 91.7, Location = "Зона D" },
                new Detector { Name = "Детектор #6-XRay", Status = "Выключен", Efficiency = 100.0, Location = "Зона B" },
                new Detector { Name = "Детектор #7-Neutron", Status = "Работает", Efficiency = 88.9, Location = "Зона C" },
                new Detector { Name = "Детектор #8-Gamma", Status = "Обслуживание", Efficiency = 72.4, Location = "Зона D" }
            };

            context.Detectors.AddRange(detectors);
            context.SaveChanges();
        }
    }
}