using Dicom;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public class ExportService
    {
        public void ExportSnapshot(Canvas canvas, string filePath)
        {
            var rtb = new RenderTargetBitmap(
                (int)canvas.ActualWidth,
                (int)canvas.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            rtb.Render(canvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (var fs = File.OpenWrite(filePath))
            {
                encoder.Save(fs);
            }
        }

        public void ExportMeasurementsAsJson(DicomFile dicomFile, List<MeasurementData> measurements, string filePath)
        {
            var report = new
            {
                PatientInfo = dicomFile != null ? new
                {
                    Name = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientName, "N/A"),
                    ID = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "N/A"),
                    StudyDate = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "N/A")
                } : null,
                Measurements = measurements,
                ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            File.WriteAllText(filePath, JsonConvert.SerializeObject(report, Formatting.Indented));
        }

        public void ExportMeasurementsAsText(List<MeasurementData> measurements, string filePath)
        {
            var text = $"DICOM Measurement Report\nGenerated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\nMeasurements:\n" +
                       string.Join("\n", measurements.Select(m => $"- {m.Type}: {m.Value:F2} {m.Unit}"));
            File.WriteAllText(filePath, text);
        }
    }
}