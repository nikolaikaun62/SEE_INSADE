using SEE_INSADE.Services.Scanning;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace SEE_INSADE.Services.Export
{
    public class DataExporter
    {
        public void ExportScanData(ScanData scanData, string filePath)
        {
            var exportData = new
            {
                Timestamp = System.DateTime.Now,
                Width = scanData.MaterialMap.GetLength(0),
                Height = scanData.MaterialMap.GetLength(1),
                ObjectCount = scanData.ObjectCount
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public void ExportImage(WriteableBitmap image, string filePath)
        {
            // Simple image export (would need proper implementation for different formats)
            using var fileStream = new FileStream(filePath, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(fileStream);
        }
    }
}