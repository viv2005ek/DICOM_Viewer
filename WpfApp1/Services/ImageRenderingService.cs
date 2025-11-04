using Dicom.Imaging;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp1.Services
{
    public class ImageRenderingService
    {
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public BitmapSource RenderDicomImage(DicomImage dicomImage, int windowWidth, int windowLevel)
        {
            dicomImage.WindowWidth = windowWidth;
            dicomImage.WindowCenter = windowLevel;
            var rendered = dicomImage.RenderImage();
            
            using (var bmp = rendered.AsClonedBitmap())
            {
                return BitmapToImageSource(bmp);
            }
        }

        private BitmapSource BitmapToImageSource(System.Drawing.Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally 
            { 
                DeleteObject(hBitmap); 
            }
        }
    }
}