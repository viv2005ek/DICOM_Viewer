using Dicom;
using Dicom.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfApp1.Services
{
    public class DicomLoaderService
    {
        public (DicomFile file, DicomImage image) LoadSingleFile(string filePath)
        {
            var file = DicomFile.Open(filePath);
            var image = new DicomImage(filePath);
            return (file, image);
        }

        public (List<DicomFile> files, List<DicomImage> images) LoadSeries(string[] filePaths)
        {
            var files = new List<DicomFile>();
            var images = new List<DicomImage>();

            foreach (var file in filePaths.OrderBy(f => f))
            {
                images.Add(new DicomImage(file));
                files.Add(DicomFile.Open(file));
            }

            return (files, images);
        }

        public (double spacingX, double spacingY) ExtractPixelSpacing(DicomFile file)
        {
            try
            {
                if (file.Dataset.TryGetValues<decimal>(DicomTag.PixelSpacing, out decimal[] spacing) && spacing.Length >= 2)
                {
                    return ((double)spacing[0], (double)spacing[1]);
                }
            }
            catch { }
            
            return (1.0, 1.0);
        }

        public Dictionary<string, string> GetMetadata(DicomFile file)
        {
            var metadata = new Dictionary<string, string>();
            var tagsToShow = new[]
            {
                (DicomTag.PatientName, "Patient Name"),
                (DicomTag.PatientID, "Patient ID"),
                (DicomTag.PatientSex, "Sex"),
                (DicomTag.PatientAge, "Age"),
                (DicomTag.PatientBirthDate, "Birth Date"),
                (DicomTag.StudyDate, "Study Date"),
                (DicomTag.StudyDescription, "Study Description"),
                (DicomTag.Modality, "Modality"),
                (DicomTag.SeriesNumber, "Series Number"),
                (DicomTag.InstanceNumber, "Instance Number"),
                (DicomTag.SliceThickness, "Slice Thickness"),
                (DicomTag.PixelSpacing, "Pixel Spacing"),
                (DicomTag.ImagePositionPatient, "Position"),
                (DicomTag.WindowWidth, "Window Width"),
                (DicomTag.WindowCenter, "Window Center")
            };

            foreach (var (tag, name) in tagsToShow)
            {
                try
                {
                    if (file.Dataset.Contains(tag))
                    {
                        string value = file.Dataset.GetValueOrDefault(tag, 0, "N/A").ToString();
                        metadata[name] = value;
                    }
                }
                catch { }
            }

            return metadata;
        }
    }
}