using Dicom;
using Dicom.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private DicomImage? dicomImage;
        private List<DicomImage> dicomSeries = new List<DicomImage>();
        private DispatcherTimer cineTimer = new DispatcherTimer();
        private List<Line> distanceLines = new List<Line>();
        private int currentSlice = 0;

        private double scale = 1.0;
        private Point origin;
        private Point start;

        public MainWindow()
        {
            InitializeComponent();
            cineTimer.Interval = TimeSpan.FromMilliseconds(200);
            cineTimer.Tick += CineTimer_Tick;
        }

        #region DICOM Loading
        private void LoadDicom_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "DICOM Files (*.dcm)|*.dcm" };
            if (dlg.ShowDialog() == true)
            {
                dicomImage = new DicomImage(dlg.FileName);
                dicomSeries.Clear();
                dicomSeries.Add(dicomImage);
                SliceSlider.Maximum = dicomSeries.Count - 1;
                currentSlice = 0;
                UpdateImage();
                ShowMetadata(DicomFile.Open(dlg.FileName));
            }
        }

        private void LoadSeries_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "DICOM Files (*.dcm)|*.dcm", Multiselect = true };
            if (dlg.ShowDialog() == true)
            {
                dicomSeries.Clear();
                foreach (var file in dlg.FileNames)
                    dicomSeries.Add(new DicomImage(file));

                SliceSlider.Maximum = dicomSeries.Count - 1;
                currentSlice = 0;
                dicomImage = dicomSeries[currentSlice];
                UpdateImage();
            }
        }
        #endregion

        #region Image Rendering
        private void UpdateImage()
        {
            if (dicomImage == null) return;
            dicomImage.WindowWidth = (int)WindowSlider.Value;
            dicomImage.WindowCenter = (int)LevelSlider.Value;

            var rendered = dicomImage.RenderImage();
            using (var bmp = rendered.AsClonedBitmap())
            {
                DicomImage.Source = BitmapToImageSource(bmp);
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
            finally { DeleteObject(hBitmap); }
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        #endregion

        #region Window/Level & Slice
        private void WindowLevelChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateImage();

        private void SliceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (dicomSeries.Count == 0) return;
            currentSlice = (int)SliceSlider.Value;
            dicomImage = dicomSeries[currentSlice];
            UpdateImage();
        }
        #endregion

        #region Cine Playback
        private void PlayCine_Click(object sender, RoutedEventArgs e) => cineTimer.Start();

        private void StopCine_Click(object sender, RoutedEventArgs e) => cineTimer.Stop();

        private void CineTimer_Tick(object? sender, EventArgs e)
        {
            if (dicomSeries.Count == 0) return;
            currentSlice = (currentSlice + 1) % dicomSeries.Count;
            SliceSlider.Value = currentSlice;
        }
        #endregion

        #region Metadata
        private void ShowMetadata(DicomFile file)
        {
            MetadataTree.Items.Clear();

            DicomTag[] tagsToShow = new DicomTag[]
            {
        DicomTag.PatientName,
        DicomTag.PatientID,
        DicomTag.PatientSex,
        DicomTag.PatientAge,
        DicomTag.StudyDate,
        DicomTag.StudyDescription,
        DicomTag.Modality,
        DicomTag.SeriesNumber,
        DicomTag.InstanceNumber
            };

            foreach (var tag in tagsToShow)
            {
                if (file.Dataset.TryGetString(tag, out string value))
                {
                    TreeViewItem node = new TreeViewItem { Header = $"{tag.DictionaryEntry.Name}: {value}" };
                    MetadataTree.Items.Add(node);
                }
            }
        }

        #endregion

        #region Visibility
        private void VisibilityChanged(object sender, RoutedEventArgs e)
        {
            if (OverlayCanvas == null || ChkShowAnnotations == null || ChkShowROI == null || ChkShowSlice == null)
                return;

            OverlayCanvas.Visibility = (ChkShowAnnotations.IsChecked == true || ChkShowROI.IsChecked == true)
                ? Visibility.Visible
                : Visibility.Hidden;

            DicomImage.Visibility = (ChkShowSlice.IsChecked == true)
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        #endregion

        #region Plane Selector
        private void PlaneSelector_Changed(object sender, SelectionChangedEventArgs e) { }
        #endregion

        #region Annotations / ROI
        private void AddAnnotation_Click(object sender, RoutedEventArgs e)
        {
            Line line = new Line
            {
                X1 = 50,
                Y1 = 50,
                X2 = 150,
                Y2 = 50,
                Stroke = Brushes.Red,
                StrokeThickness = 2
            };
            OverlayCanvas.Children.Add(line);
            distanceLines.Add(line);
        }

        private void AddROI_Click(object sender, RoutedEventArgs e)
        {
            Rectangle rect = new Rectangle
            {
                Width = 100,
                Height = 50,
                Stroke = Brushes.Green,
                StrokeThickness = 2
            };
            Canvas.SetLeft(rect, 100);
            Canvas.SetTop(rect, 100);
            OverlayCanvas.Children.Add(rect);
        }
        #endregion

        #region Zoom & Pan
        private void ImageScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) scale *= 1.1;
            else scale /= 1.1;

            OverlayCanvas.RenderTransform = new ScaleTransform(scale, scale);
        }

        private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OverlayCanvas.CaptureMouse();
            start = e.GetPosition(this);
            origin = new Point(Canvas.GetLeft(OverlayCanvas), Canvas.GetTop(OverlayCanvas));
        }

        private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OverlayCanvas.ReleaseMouseCapture();
        }

        private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!OverlayCanvas.IsMouseCaptured) return;
            Point p = e.GetPosition(this);
            TranslateTransform tt = new TranslateTransform(p.X - start.X, p.Y - start.Y);
            OverlayCanvas.RenderTransform = new TransformGroup()
            {
                Children = new TransformCollection() { new ScaleTransform(scale, scale), tt }
            };
        }
        #endregion

        #region Export
        private void ExportSnapshot_Click(object sender, RoutedEventArgs e)
        {
            RenderTargetBitmap rtb = new RenderTargetBitmap(
                (int)OverlayCanvas.ActualWidth, (int)OverlayCanvas.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(OverlayCanvas);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            SaveFileDialog dlg = new SaveFileDialog { Filter = "PNG Image|*.png" };
            if (dlg.ShowDialog() == true)
            {
                using (var fs = File.OpenWrite(dlg.FileName))
                    encoder.Save(fs);
            }
        }

        private void ExportMeasurements_Click(object sender, RoutedEventArgs e)
        {
            var measurements = new { distances = distanceLines.Count };
            SaveFileDialog dlg = new SaveFileDialog { Filter = "JSON File|*.json" };
            if (dlg.ShowDialog() == true)
                File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(measurements, Formatting.Indented));
        }
        #endregion
    }
}
