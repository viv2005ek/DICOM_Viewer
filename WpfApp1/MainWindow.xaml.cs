#nullable disable
using Dicom;
using Dicom.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private DicomFile currentDicomFile;
        private DicomImage dicomImage;
        private List<DicomFile> dicomSeriesFiles = new List<DicomFile>();
        private List<DicomImage> dicomSeries = new List<DicomImage>();
        private DispatcherTimer cineTimer = new DispatcherTimer();
        private int currentSlice = 0;

        private double scale = 1.0;
        private double rotation = 0.0;
        private bool isFlippedHorizontal = false;
        private bool isFlippedVertical = false;
        private Point panOffset = new Point(0, 0);
        private bool isPanning = false;
        private Point lastPanPoint;

        private enum MeasurementTool { None, Distance, Angle, Area }
        private MeasurementTool activeTool = MeasurementTool.None;
        private List<Point> measurementPoints = new List<Point>();
        private List<UIElement> measurementElements = new List<UIElement>();
        private List<MeasurementData> measurements = new List<MeasurementData>();

        private enum AnnotationTool { None, Freehand, Arrow, Text }
        private AnnotationTool activeAnnotation = AnnotationTool.None;
        private List<Point> freehandPoints = new List<Point>();
        private Polyline currentPolyline;
        private List<UIElement> annotationElements = new List<UIElement>();

        private double pixelSpacingX = 1.0;
        private double pixelSpacingY = 1.0;

        public MainWindow()
        {
            InitializeComponent();
            cineTimer.Interval = TimeSpan.FromMilliseconds(200);
            cineTimer.Tick += CineTimer_Tick;
            OverlayCanvas.Width = 512;
            OverlayCanvas.Height = 512;
        }

        #region DICOM Loading
        private void LoadDicom_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "DICOM Files (*.dcm)|*.dcm|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    currentDicomFile = DicomFile.Open(dlg.FileName);
                    dicomImage = new DicomImage(dlg.FileName);
                    dicomSeries.Clear();
                    dicomSeriesFiles.Clear();
                    dicomSeries.Add(dicomImage);
                    dicomSeriesFiles.Add(currentDicomFile);
                    SliceSlider.Maximum = Math.Max(0, dicomSeries.Count - 1);
                    currentSlice = 0;
                    ExtractPixelSpacing(currentDicomFile);
                    UpdateImage();
                    ShowMetadata(currentDicomFile);
                    ResetTransform_Click(null, null);
                    StatusText.Text = $"Loaded: {System.IO.Path.GetFileName(dlg.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading DICOM: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadSeries_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "DICOM Files (*.dcm)|*.dcm|All Files (*.*)|*.*", Multiselect = true };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    dicomSeries.Clear();
                    dicomSeriesFiles.Clear();
                    foreach (var file in dlg.FileNames.OrderBy(f => f))
                    {
                        dicomSeries.Add(new DicomImage(file));
                        dicomSeriesFiles.Add(DicomFile.Open(file));
                    }
                    SliceSlider.Maximum = Math.Max(0, dicomSeries.Count - 1);
                    currentSlice = 0;
                    dicomImage = dicomSeries[currentSlice];
                    currentDicomFile = dicomSeriesFiles[currentSlice];
                    ExtractPixelSpacing(currentDicomFile);
                    UpdateImage();
                    ShowMetadata(currentDicomFile);
                    ResetTransform_Click(null, null);
                    StatusText.Text = $"Loaded series: {dicomSeries.Count} images";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading series: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExtractPixelSpacing(DicomFile file)
        {
            try
            {
                if (file.Dataset.TryGetValues<decimal>(DicomTag.PixelSpacing, out decimal[] spacing) && spacing.Length >= 2)
                {
                    pixelSpacingX = (double)spacing[0];
                    pixelSpacingY = (double)spacing[1];
                }
                else
                {
                    pixelSpacingX = 1.0;
                    pixelSpacingY = 1.0;
                }
            }
            catch
            {
                pixelSpacingX = 1.0;
                pixelSpacingY = 1.0;
            }
        }
        #endregion

        #region Image Rendering
        private void UpdateImage()
        {
            if (dicomImage == null) return;
            try
            {
                dicomImage.WindowWidth = (int)WindowSlider.Value;
                dicomImage.WindowCenter = (int)LevelSlider.Value;
                var rendered = dicomImage.RenderImage();
                using (var bmp = rendered.AsClonedBitmap())
                {
                    BitmapSource source = BitmapToImageSource(bmp);
                    DicomImage.Source = source;
                    OverlayCanvas.Width = source.PixelWidth;
                    OverlayCanvas.Height = source.PixelHeight;
                }
                ApplyTransforms();
                UpdateWindowLevelDisplay();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error rendering: {ex.Message}";
            }
        }

        private void ApplyTransforms()
        {
            TransformGroup group = new TransformGroup();
            group.Children.Add(new ScaleTransform(
                scale * (isFlippedHorizontal ? -1 : 1),
                scale * (isFlippedVertical ? -1 : 1),
                OverlayCanvas.Width / 2,
                OverlayCanvas.Height / 2));
            group.Children.Add(new RotateTransform(rotation, OverlayCanvas.Width / 2, OverlayCanvas.Height / 2));
            group.Children.Add(new TranslateTransform(panOffset.X, panOffset.Y));
            OverlayCanvas.RenderTransform = group;
            ZoomLabel.Text = $"Zoom: {(scale * 100):F0}%";
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

        #region Window/Level Controls
        private void WindowLevelChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WindowValue != null && LevelValue != null)
            {
                WindowValue.Text = ((int)WindowSlider.Value).ToString();
                LevelValue.Text = ((int)LevelSlider.Value).ToString();
            }
            UpdateImage();
        }

        private void WindowValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(WindowValue.Text, out int value))
                WindowSlider.Value = Math.Max(WindowSlider.Minimum, Math.Min(WindowSlider.Maximum, value));
        }

        private void LevelValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(LevelValue.Text, out int value))
                LevelSlider.Value = Math.Max(LevelSlider.Minimum, Math.Min(LevelSlider.Maximum, value));
        }

        private void UpdateWindowLevelDisplay()
        {
            WindowValue.Text = ((int)WindowSlider.Value).ToString();
            LevelValue.Text = ((int)LevelSlider.Value).ToString();
        }

        private void PresetAbdomen_Click(object sender, RoutedEventArgs e) => SetWindowLevel(400, 40);
        private void PresetLung_Click(object sender, RoutedEventArgs e) => SetWindowLevel(1500, -600);
        private void PresetBrain_Click(object sender, RoutedEventArgs e) => SetWindowLevel(80, 40);
        private void PresetBone_Click(object sender, RoutedEventArgs e) => SetWindowLevel(2000, 300);
        private void PresetMediastinum_Click(object sender, RoutedEventArgs e) => SetWindowLevel(350, 50);
        private void PresetLiver_Click(object sender, RoutedEventArgs e) => SetWindowLevel(150, 30);

        private void SetWindowLevel(int window, int level)
        {
            WindowSlider.Value = window;
            LevelSlider.Value = level;
        }
        #endregion

        #region Slice Navigation
        private void SliceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (dicomSeries.Count == 0) return;
            currentSlice = (int)SliceSlider.Value;
            if (currentSlice >= 0 && currentSlice < dicomSeries.Count)
            {
                dicomImage = dicomSeries[currentSlice];
                currentDicomFile = dicomSeriesFiles[currentSlice];
                UpdateImage();
                SliceInfo.Text = $"{currentSlice + 1}/{dicomSeries.Count}";
            }
        }
        #endregion

        #region Cine Playback
        private void PlayCine_Click(object sender, RoutedEventArgs e)
        {
            if (dicomSeries.Count > 1)
            {
                cineTimer.Start();
                StatusText.Text = "Playing cine...";
            }
        }

        private void StopCine_Click(object sender, RoutedEventArgs e)
        {
            cineTimer.Stop();
            StatusText.Text = "Cine stopped";
        }

        private void CineTimer_Tick(object sender, EventArgs e)
        {
            if (dicomSeries.Count == 0) return;
            currentSlice = (currentSlice + 1) % dicomSeries.Count;
            SliceSlider.Value = currentSlice;
        }
        #endregion

        #region Transform Tools
        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            rotation = (rotation + 90) % 360;
            ApplyTransforms();
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            rotation = (rotation - 90) % 360;
            if (rotation < 0) rotation += 360;
            ApplyTransforms();
        }

        private void FlipHorizontal_Click(object sender, RoutedEventArgs e)
        {
            isFlippedHorizontal = !isFlippedHorizontal;
            ApplyTransforms();
        }

        private void FlipVertical_Click(object sender, RoutedEventArgs e)
        {
            isFlippedVertical = !isFlippedVertical;
            ApplyTransforms();
        }

        private void ResetTransform_Click(object sender, RoutedEventArgs e)
        {
            scale = 1.0;
            rotation = 0.0;
            isFlippedHorizontal = false;
            isFlippedVertical = false;
            panOffset = new Point(0, 0);
            ApplyTransforms();
        }
        #endregion

        #region Zoom Controls
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            scale *= 1.2;
            ApplyTransforms();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            scale /= 1.2;
            ApplyTransforms();
        }

        private void Zoom100_Click(object sender, RoutedEventArgs e)
        {
            scale = 1.0;
            panOffset = new Point(0, 0);
            ApplyTransforms();
        }

        private void FitToScreen_Click(object sender, RoutedEventArgs e)
        {
            if (DicomImage.Source != null)
            {
                double scaleX = ImageScrollViewer.ActualWidth / OverlayCanvas.Width;
                double scaleY = ImageScrollViewer.ActualHeight / OverlayCanvas.Height;
                scale = Math.Min(scaleX, scaleY) * 0.95;
                panOffset = new Point(0, 0);
                ApplyTransforms();
            }
        }

        private void ImageScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                Point mousePos = e.GetPosition(OverlayCanvas);
                double oldScale = scale;
                if (e.Delta > 0) scale *= 1.1;
                else scale /= 1.1;
                double scaleChange = scale / oldScale;
                panOffset.X = mousePos.X - (mousePos.X - panOffset.X) * scaleChange;
                panOffset.Y = mousePos.Y - (mousePos.Y - panOffset.Y) * scaleChange;
                ApplyTransforms();
                e.Handled = true;
            }
            else
            {
                if (dicomSeries.Count > 1)
                {
                    if (e.Delta > 0 && currentSlice > 0)
                        SliceSlider.Value = currentSlice - 1;
                    else if (e.Delta < 0 && currentSlice < dicomSeries.Count - 1)
                        SliceSlider.Value = currentSlice + 1;
                    e.Handled = true;
                }
            }
        }
        #endregion

        #region Pan Controls
        private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (activeTool == MeasurementTool.None && activeAnnotation == AnnotationTool.None)
            {
                isPanning = true;
                lastPanPoint = e.GetPosition(this);
                OverlayCanvas.CaptureMouse();
                Cursor = Cursors.Hand;
            }
            else if (activeTool != MeasurementTool.None)
            {
                HandleMeasurementClick(e);
            }
            else if (activeAnnotation != AnnotationTool.None)
            {
                HandleAnnotationClick(e);
            }
        }

        private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isPanning)
            {
                isPanning = false;
                OverlayCanvas.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
            }
            else if (activeAnnotation == AnnotationTool.Freehand && currentPolyline != null)
            {
                currentPolyline = null;
                freehandPoints.Clear();
            }
        }

        private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(OverlayCanvas);
            MousePosText.Text = $"Position: ({(int)mousePos.X}, {(int)mousePos.Y})";

            if (ChkShowHU.IsChecked == true && currentDicomFile != null)
            {
                UpdateHUValue(mousePos);
            }

            if (isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(this);
                Vector delta = currentPoint - lastPanPoint;
                panOffset.X += delta.X;
                panOffset.Y += delta.Y;
                lastPanPoint = currentPoint;
                ApplyTransforms();
            }
            else if (activeAnnotation == AnnotationTool.Freehand && e.LeftButton == MouseButtonState.Pressed && currentPolyline != null)
            {
                freehandPoints.Add(mousePos);
                currentPolyline.Points.Add(mousePos);
            }
        }

        private void OverlayCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (activeTool == MeasurementTool.Area && measurementPoints.Count >= 3)
            {
                CompleteAreaMeasurement();
            }
            else
            {
                DeactivateAllTools();
            }
        }

        private void UpdateHUValue(Point mousePos)
        {
            try
            {
                if (dicomImage == null || currentDicomFile == null) return;
                string modality = currentDicomFile.Dataset.GetSingleValueOrDefault(DicomTag.Modality, "");
                if (modality != "CT")
                {
                    HUValueText.Text = "HU: N/A (Not CT)";
                    return;
                }
                int x = (int)mousePos.X;
                int y = (int)mousePos.Y;
                if (currentDicomFile.Dataset.TryGetValues<short>(DicomTag.PixelData, out short[] pixelData))
                {
                    int width = currentDicomFile.Dataset.GetSingleValue<ushort>(DicomTag.Columns);
                    int height = currentDicomFile.Dataset.GetSingleValue<ushort>(DicomTag.Rows);
                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        int index = y * width + x;
                        if (index < pixelData.Length)
                        {
                            short pixelValue = pixelData[index];
                            double rescaleSlope = currentDicomFile.Dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
                            double rescaleIntercept = currentDicomFile.Dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);
                            double huValue = pixelValue * rescaleSlope + rescaleIntercept;
                            HUValueText.Text = $"HU: {huValue:F0}";
                        }
                    }
                }
            }
            catch
            {
                HUValueText.Text = "HU: -";
            }
        }
        #endregion

        #region Measurement Tools
        private void MeasureDistance_Click(object sender, RoutedEventArgs e)
        {
            DeactivateAllTools();
            activeTool = MeasurementTool.Distance;
            measurementPoints.Clear();
            ActiveToolText.Text = "Active Tool: Distance (Click 2 points)";
            BtnDistance.Background = Brushes.LightBlue;
            Cursor = Cursors.Cross;
        }

        private void MeasureAngle_Click(object sender, RoutedEventArgs e)
        {
            DeactivateAllTools();
            activeTool = MeasurementTool.Angle;
            measurementPoints.Clear();
            ActiveToolText.Text = "Active Tool: Angle (Click 3 points)";
            BtnAngle.Background = Brushes.LightBlue;
            Cursor = Cursors.Cross;
        }

        private void MeasureArea_Click(object sender, RoutedEventArgs e)
        {
            DeactivateAllTools();
            activeTool = MeasurementTool.Area;
            measurementPoints.Clear();
            ActiveToolText.Text = "Active Tool: Area/ROI (Click points, right-click to finish)";
            BtnArea.Background = Brushes.LightBlue;
            Cursor = Cursors.Cross;
        }

        private void HandleMeasurementClick(MouseButtonEventArgs e)
        {
            Point clickPos = e.GetPosition(OverlayCanvas);
            measurementPoints.Add(clickPos);
            Ellipse marker = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = Brushes.Yellow,
                Stroke = Brushes.Red,
                StrokeThickness = 1
            };
            Canvas.SetLeft(marker, clickPos.X - 3);
            Canvas.SetTop(marker, clickPos.Y - 3);
            OverlayCanvas.Children.Add(marker);
            measurementElements.Add(marker);

            switch (activeTool)
            {
                case MeasurementTool.Distance:
                    if (measurementPoints.Count == 2) CompletDistanceMeasurement();
                    break;
                case MeasurementTool.Angle:
                    if (measurementPoints.Count == 3) CompleteAngleMeasurement();
                    break;
                case MeasurementTool.Area:
                    if (measurementPoints.Count > 2) DrawPolygonPreview();
                    break;
            }
        }

        private void CompletDistanceMeasurement()
        {
            Point p1 = measurementPoints[0];
            Point p2 = measurementPoints[1];
            Line line = new Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = Brushes.Cyan,
                StrokeThickness = 2
            };
            OverlayCanvas.Children.Add(line);
            measurementElements.Add(line);
            double dx = (p2.X - p1.X) * pixelSpacingX;
            double dy = (p2.Y - p1.Y) * pixelSpacingY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            TextBlock label = new TextBlock
            {
                Text = $"{distance:F2} mm",
                Foreground = Brushes.Yellow,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(3),
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, (p1.X + p2.X) / 2);
            Canvas.SetTop(label, (p1.Y + p2.Y) / 2 - 20);
            OverlayCanvas.Children.Add(label);
            measurementElements.Add(label);
            measurements.Add(new MeasurementData { Type = "Distance", Value = distance, Unit = "mm" });
            DeactivateAllTools();
            StatusText.Text = $"Distance: {distance:F2} mm";
        }

        private void CompleteAngleMeasurement()
        {
            Point p1 = measurementPoints[0];
            Point p2 = measurementPoints[1];
            Point p3 = measurementPoints[2];
            Line line1 = new Line { X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y, Stroke = Brushes.Cyan, StrokeThickness = 2 };
            Line line2 = new Line { X1 = p2.X, Y1 = p2.Y, X2 = p3.X, Y2 = p3.Y, Stroke = Brushes.Cyan, StrokeThickness = 2 };
            OverlayCanvas.Children.Add(line1);
            OverlayCanvas.Children.Add(line2);
            measurementElements.Add(line1);
            measurementElements.Add(line2);
            Vector v1 = new Vector(p1.X - p2.X, p1.Y - p2.Y);
            Vector v2 = new Vector(p3.X - p2.X, p3.Y - p2.Y);
            double angle = Vector.AngleBetween(v1, v2);
            if (angle < 0) angle += 360;
            TextBlock label = new TextBlock
            {
                Text = $"{angle:F1}°",
                Foreground = Brushes.Yellow,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(3),
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, p2.X + 10);
            Canvas.SetTop(label, p2.Y - 20);
            OverlayCanvas.Children.Add(label);
            measurementElements.Add(label);
            measurements.Add(new MeasurementData { Type = "Angle", Value = angle, Unit = "degrees" });
            DeactivateAllTools();
            StatusText.Text = $"Angle: {angle:F1}°";
        }

        private void DrawPolygonPreview()
        {
            var oldPreview = measurementElements.OfType<Polygon>().FirstOrDefault();
            if (oldPreview != null)
            {
                OverlayCanvas.Children.Remove(oldPreview);
                measurementElements.Remove(oldPreview);
            }
            Polygon polygon = new Polygon
            {
                Stroke = Brushes.Cyan,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(50, 0, 255, 255))
            };
            foreach (var pt in measurementPoints)
            {
                polygon.Points.Add(pt);
            }
            OverlayCanvas.Children.Add(polygon);
            measurementElements.Add(polygon);
        }

        private void CompleteAreaMeasurement()
        {
            if (measurementPoints.Count < 3) return;
            double area = 0;
            for (int i = 0; i < measurementPoints.Count; i++)
            {
                Point p1 = measurementPoints[i];
                Point p2 = measurementPoints[(i + 1) % measurementPoints.Count];
                area += (p1.X * p2.Y - p2.X * p1.Y);
            }
            area = Math.Abs(area / 2.0) * pixelSpacingX * pixelSpacingY;
            double cx = measurementPoints.Average(p => p.X);
            double cy = measurementPoints.Average(p => p.Y);
            TextBlock label = new TextBlock
            {
                Text = $"{area:F2} mm²",
                Foreground = Brushes.Yellow,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(3),
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, cx);
            Canvas.SetTop(label, cy - 20);
            OverlayCanvas.Children.Add(label);
            measurementElements.Add(label);
            measurements.Add(new MeasurementData { Type = "Area", Value = area, Unit = "mm²" });
            DeactivateAllTools();
            StatusText.Text = $"Area: {area:F2} mm²";
        }

        private void ClearMeasurements_Click(object sender, RoutedEventArgs e)
        {
            foreach (var element in measurementElements)
            {
                OverlayCanvas.Children.Remove(element);
            }
            measurementElements.Clear();
            measurements.Clear();
            measurementPoints.Clear();
            StatusText.Text = "Measurements cleared";
        }
        #endregion

        #region Annotation Tools
        private void Freehand_Click(object sender, RoutedEventArgs e)
        {
            DeactivateAllTools();
            activeAnnotation = AnnotationTool.Freehand;
            ActiveToolText.Text = "Active Tool: Freehand (Click and drag)";
            BtnFreehand.Background = Brushes.LightGreen;
            Cursor = Cursors.Pen;
        }

        private void Arrow_Click(object sender, RoutedEventArgs e)
        {
            DeactivateAllTools();
            activeAnnotation = AnnotationTool.Arrow;
            measurementPoints.Clear();
            ActiveToolText.Text = "Active Tool: Arrow (Click 2 points)";
            BtnArrow.Background = Brushes.LightGreen;
            Cursor = Cursors.Cross;
        }

        private void TextAnnotation_Click(object sender, RoutedEventArgs e)
        {
            DeactivateAllTools();
            activeAnnotation = AnnotationTool.Text;
            ActiveToolText.Text = "Active Tool: Text (Click to place)";
            BtnText.Background = Brushes.LightGreen;
            Cursor = Cursors.IBeam;
        }

        private void HandleAnnotationClick(MouseButtonEventArgs e)
        {
            Point clickPos = e.GetPosition(OverlayCanvas);
            switch (activeAnnotation)
            {
                case AnnotationTool.Freehand:
                    if (currentPolyline == null)
                    {
                        currentPolyline = new Polyline { Stroke = Brushes.Red, StrokeThickness = 2 };
                        freehandPoints.Clear();
                        freehandPoints.Add(clickPos);
                        currentPolyline.Points.Add(clickPos);
                        OverlayCanvas.Children.Add(currentPolyline);
                        annotationElements.Add(currentPolyline);
                    }
                    break;
                case AnnotationTool.Arrow:
                    measurementPoints.Add(clickPos);
                    if (measurementPoints.Count == 2)
                    {
                        DrawArrow(measurementPoints[0], measurementPoints[1]);
                        DeactivateAllTools();
                    }
                    break;
                case AnnotationTool.Text:
                    string text = Microsoft.VisualBasic.Interaction.InputBox("Enter annotation text:", "Text Annotation", "");
                    if (!string.IsNullOrEmpty(text))
                    {
                        TextBlock tb = new TextBlock
                        {
                            Text = text,
                            Foreground = Brushes.Yellow,
                            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                            Padding = new Thickness(5),
                            FontSize = 14,
                            FontWeight = FontWeights.Bold
                        };
                        Canvas.SetLeft(tb, clickPos.X);
                        Canvas.SetTop(tb, clickPos.Y);
                        OverlayCanvas.Children.Add(tb);
                        annotationElements.Add(tb);
                    }
                    DeactivateAllTools();
                    break;
            }
        }

        private void DrawArrow(Point start, Point end)
        {
            Line line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = Brushes.Orange,
                StrokeThickness = 3
            };
            OverlayCanvas.Children.Add(line);
            annotationElements.Add(line);
            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            double arrowLength = 15;
            double arrowAngle = Math.PI / 6;
            Point p1 = new Point(
                end.X - arrowLength * Math.Cos(angle - arrowAngle),
                end.Y - arrowLength * Math.Sin(angle - arrowAngle));
            Point p2 = new Point(
                end.X - arrowLength * Math.Cos(angle + arrowAngle),
                end.Y - arrowLength * Math.Sin(angle + arrowAngle));
            Polygon arrowHead = new Polygon
            {
                Fill = Brushes.Orange,
                Stroke = Brushes.Orange,
                StrokeThickness = 2
            };
            arrowHead.Points.Add(end);
            arrowHead.Points.Add(p1);
            arrowHead.Points.Add(p2);
            OverlayCanvas.Children.Add(arrowHead);
            annotationElements.Add(arrowHead);
        }

        private void ClearAnnotations_Click(object sender, RoutedEventArgs e)
        {
            foreach (var element in annotationElements)
            {
                OverlayCanvas.Children.Remove(element);
            }
            annotationElements.Clear();
            StatusText.Text = "Annotations cleared";
        }

        private void DeactivateAllTools()
        {
            activeTool = MeasurementTool.None;
            activeAnnotation = AnnotationTool.None;
            measurementPoints.Clear();
            freehandPoints.Clear();
            currentPolyline = null;
            ActiveToolText.Text = "Active Tool: None";
            Cursor = Cursors.Arrow;
            BtnDistance.Background = SystemColors.ControlBrush;
            BtnAngle.Background = SystemColors.ControlBrush;
            BtnArea.Background = SystemColors.ControlBrush;
            BtnFreehand.Background = SystemColors.ControlBrush;
            BtnArrow.Background = SystemColors.ControlBrush;
            BtnText.Background = SystemColors.ControlBrush;
        }
        #endregion

        #region Keyboard Shortcuts
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Add:
                case Key.OemPlus:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        ZoomIn_Click(null, null);
                    break;
                case Key.Subtract:
                case Key.OemMinus:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        ZoomOut_Click(null, null);
                    break;
                case Key.R:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        ResetTransform_Click(null, null);
                    break;
                case Key.Up:
                    if (currentSlice > 0)
                        SliceSlider.Value = currentSlice - 1;
                    break;
                case Key.Down:
                    if (currentSlice < dicomSeries.Count - 1)
                        SliceSlider.Value = currentSlice + 1;
                    break;
                case Key.Escape:
                    DeactivateAllTools();
                    break;
                case Key.Space:
                    if (activeTool == MeasurementTool.Area && measurementPoints.Count >= 3)
                    {
                        CompleteAreaMeasurement();
                    }
                    break;
            }
        }
        #endregion

        #region Metadata
        private void ShowMetadata(DicomFile file)
        {
            MetadataTree.Items.Clear();
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
                        TreeViewItem node = new TreeViewItem { Header = $"{name}: {value}" };
                        MetadataTree.Items.Add(node);
                    }
                }
                catch { }
            }
        }
        #endregion

        #region Export
        private void ExportSnapshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)OverlayCanvas.ActualWidth,
                    (int)OverlayCanvas.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);
                rtb.Render(OverlayCanvas);
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                SaveFileDialog dlg = new SaveFileDialog { Filter = "PNG Image|*.png" };
                if (dlg.ShowDialog() == true)
                {
                    using (var fs = File.OpenWrite(dlg.FileName))
                        encoder.Save(fs);
                    StatusText.Text = $"Snapshot saved: {System.IO.Path.GetFileName(dlg.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting snapshot: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportMeasurements_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var report = new
                {
                    PatientInfo = currentDicomFile != null ? new
                    {
                        Name = currentDicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientName, "N/A"),
                        ID = currentDicomFile.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "N/A"),
                        StudyDate = currentDicomFile.Dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "N/A")
                    } : null,
                    Measurements = measurements,
                    ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                SaveFileDialog dlg = new SaveFileDialog { Filter = "JSON File|*.json|Text File|*.txt" };
                if (dlg.ShowDialog() == true)
                {
                    if (dlg.FileName.EndsWith(".json"))
                    {
                        File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(report, Formatting.Indented));
                    }
                    else
                    {
                        var text = $"DICOM Measurement Report\nGenerated: {report.ExportDate}\n\nMeasurements:\n" +
                                  string.Join("\n", measurements.Select(m => $"- {m.Type}: {m.Value:F2} {m.Unit}"));
                        File.WriteAllText(dlg.FileName, text);
                    }
                    StatusText.Text = $"Report saved: {System.IO.Path.GetFileName(dlg.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }

    public class MeasurementData
    {
        public string Type { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}