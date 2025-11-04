    using Dicom;
using System;
using System.Windows;

namespace WpfApp1.Services
{
    public class HUCalculatorService
    {
        public string CalculateHU(DicomFile file, Point mousePos)
        {
            try
            {
                if (file == null) return "HU: -";

                string modality = file.Dataset.GetSingleValueOrDefault(DicomTag.Modality, "");
                if (modality != "CT")
                {
                    return "HU: N/A (Not CT)";
                }

                int x = (int)mousePos.X;
                int y = (int)mousePos.Y;

                if (file.Dataset.TryGetValues<short>(DicomTag.PixelData, out short[] pixelData))
                {
                    int width = file.Dataset.GetSingleValue<ushort>(DicomTag.Columns);
                    int height = file.Dataset.GetSingleValue<ushort>(DicomTag.Rows);

                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        int index = y * width + x;
                        if (index < pixelData.Length)
                        {
                            short pixelValue = pixelData[index];
                            double rescaleSlope = file.Dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
                            double rescaleIntercept = file.Dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);
                            double huValue = pixelValue * rescaleSlope + rescaleIntercept;
                            return $"HU: {huValue:F0}";
                        }
                    }
                }
            }
            catch { }

            return "HU: -";
        }
    }
}