#nullable disable
using Dicom;
using Dicom.Imaging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp1.Services;
using WpfApp1.Managers;
using WpfApp1.Models;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        // Services
        private readonly DicomLoaderService _loaderService;
        private readonly ImageRenderingService _renderingService;
        private readonly TransformService _transformService;
        private readonly MeasurementService _measurementService;
        private readonly AnnotationService _annotationService;
        private readonly CinePlayerService _cinePlayerService;
        private readonly HUCalculatorService _huCalculatorService;
        private readonly ExportService _exportService;

        // Managers
        private readonly ToolManager _toolManager;

        // State
        private DicomFile _currentDicomFile;
        private DicomImage _dicomImage;
        private List<DicomFile> _dicomSeriesFiles = new List<DicomFile>();
        private List<DicomImage> _dicomSeries = new List<DicomImage>();
        private int _currentSlice = 0;
        private double _pixelSpacingX = 1.0;
        private double _pixelSpacingY = 1.0;
        private TransformState _transformState = new TransformState();
        private bool _isPanning = false;
        private Point _lastPanPoint;
        private List<Point> _tempPoints = new List<Point>();

        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            _loaderService = new DicomLoaderService();
            _renderingService = new ImageRenderingService();
            _transformService = new TransformService();
            _measurementService = new MeasurementService();
            _annotationService = new AnnotationService();
            _cinePlayerService = new CinePlayerService();
            _huCalculatorService = new HUCalculatorService();
            _exportService = new ExportService();

            // Initialize managers
            _toolManager = new ToolManager(
                BtnDistance, BtnAngle, BtnArea,
                BtnFreehand, BtnArrow, BtnText,
                ActiveToolText, this);

            // Setup cine player
            _cinePlayerService.SetCallback(OnCineSliceChanged);

            // Setup canvas
            OverlayCanvas.Width = 512;
            OverlayCanvas.Height = 512;
        }

        #region DICOM Loading
        private void LoadDicom_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "DICOM Files (*.dcm)|*.dcm|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var (file, image) = _loaderService.LoadSingleFile(dlg.FileName);

                    _currentDicomFile = file;
                    _dicomImage = image;
                    _dicomSeries.Clear();
                    _dicomSeriesFiles.Clear();
                    _dicomSeries.Add(image);
                    _dicomSeriesFiles.Add(file);

                    SliceSlider.Maximum = Math.Max(0, _dicomSeries.Count - 1);
                    _currentSlice = 0;

                    (_pixelSpacingX, _pixelSpacingY) = _loaderService.ExtractPixelSpacing(file);

                    UpdateImage();
                    ShowMetadata(file);
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
            var dlg = new OpenFileDialog
            {
                Filter = "DICOM Files (*.dcm)|*.dcm|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var (files, images) = _loaderService.LoadSeries(dlg.FileNames);

                    _dicomSeries = images;
                    _dicomSeriesFiles = files;

                    SliceSlider.Maximum = Math.Max(0, _dicomSeries.Count - 1);
                    _currentSlice = 0;
                    _dicomImage = _dicomSeries[_currentSlice];
                    _currentDicomFile = _dicomSeriesFiles[_currentSlice];

                    (_pixelSpacingX, _pixelSpacingY) = _loaderService.ExtractPixelSpacing(_currentDicomFile);

                    UpdateImage();
                    ShowMetadata(_currentDicomFile);
                    ResetTransform_Click(null, null);
                    StatusText.Text = $"Loaded series: {_dicomSeries.Count} images";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading series: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region Image Rendering
        private void UpdateImage()
        {
            if (_dicomImage == null) return;

            try
            {
                var source = _renderingService.RenderDicomImage(
                    _dicomImage,
                    (int)WindowSlider.Value,
                    (int)LevelSlider.Value);

                DicomImage.Source = source;
                OverlayCanvas.Width = source.PixelWidth;
                OverlayCanvas.Height = source.PixelHeight;

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
            var transform = _transformService.CreateTransformGroup(
                _transformState,
                OverlayCanvas.Width / 2,
                OverlayCanvas.Height / 2);

            OverlayCanvas.RenderTransform = transform;
            ZoomLabel.Text = $"Zoom: {(_transformState.Scale * 100):F0}%";
        }
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
            if (_dicomSeries.Count == 0) return;

            _currentSlice = (int)SliceSlider.Value;
            if (_currentSlice >= 0 && _currentSlice < _dicomSeries.Count)
            {
                _dicomImage = _dicomSeries[_currentSlice];
                _currentDicomFile = _dicomSeriesFiles[_currentSlice];
                UpdateImage();
                SliceInfo.Text = $"{_currentSlice + 1}/{_dicomSeries.Count}";
            }
        }

        private void OnCineSliceChanged(int slice)
        {
            SliceSlider.Value = slice;
        }
        #endregion

        #region Cine Playback
        private void PlayCine_Click(object sender, RoutedEventArgs e)
        {
            if (_dicomSeries.Count > 1)
            {
                _cinePlayerService.Play(_currentSlice, _dicomSeries.Count - 1);
                StatusText.Text = "Playing cine...";
            }
        }

        private void StopCine_Click(object sender, RoutedEventArgs e)
        {
            _cinePlayerService.Stop();
            StatusText.Text = "Cine stopped";
        }
        #endregion

        #region Transform Tools
        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            _transformService.RotateLeft(_transformState);
            ApplyTransforms();
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            _transformService.RotateRight(_transformState);
            ApplyTransforms();
        }

        private void FlipHorizontal_Click(object sender, RoutedEventArgs e)
        {
            _transformService.FlipHorizontal(_transformState);
            ApplyTransforms();
        }

        private void FlipVertical_Click(object sender, RoutedEventArgs e)
        {
            _transformService.FlipVertical(_transformState);
            ApplyTransforms();
        }

        private void ResetTransform_Click(object sender, RoutedEventArgs e)
        {
            _transformService.Reset(_transformState);
            ApplyTransforms();
        }
        #endregion

        #region Zoom Controls
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _transformService.ZoomIn(_transformState);
            ApplyTransforms();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _transformService.ZoomOut(_transformState);
            ApplyTransforms();
        }

        private void Zoom100_Click(object sender, RoutedEventArgs e)
        {
            _transformState.Scale = 1.0;
            _transformState.PanOffset = new Point(0, 0);
            ApplyTransforms();
        }

        private void FitToScreen_Click(object sender, RoutedEventArgs e)
        {
            if (DicomImage.Source != null)
            {
                _transformService.CalculateFitToScreen(
                    _transformState,
                    ImageScrollViewer.ActualWidth,
                    ImageScrollViewer.ActualHeight,
                    OverlayCanvas.Width,
                    OverlayCanvas.Height);
                ApplyTransforms();
            }
        }

        private void ImageScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                Point mousePos = e.GetPosition(OverlayCanvas);
                double oldScale = _transformState.Scale;

                if (e.Delta > 0)
                    _transformService.ZoomIn(_transformState, 1.1);
                else
                    _transformService.ZoomOut(_transformState, 1.1);

                double scaleChange = _transformState.Scale / oldScale;
                _transformState.PanOffset = new Point(
                    mousePos.X - (mousePos.X - _transformState.PanOffset.X) * scaleChange,
                    mousePos.Y - (mousePos.Y - _transformState.PanOffset.Y) * scaleChange
                );

                ApplyTransforms();
                e.Handled = true;
            }
            else
            {
                if (_dicomSeries.Count > 1)
                {
                    if (e.Delta > 0 && _currentSlice > 0)
                        SliceSlider.Value = _currentSlice - 1;
                    else if (e.Delta < 0 && _currentSlice < _dicomSeries.Count - 1)
                        SliceSlider.Value = _currentSlice + 1;
                    e.Handled = true;
                }
            }
        }
        #endregion

        #region Pan and Mouse Controls
        private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_toolManager.ActiveMeasurementTool == MeasurementTool.None &&
                _toolManager.ActiveAnnotationTool == AnnotationTool.None)
            {
                _isPanning = true;
                _lastPanPoint = e.GetPosition(this);
                OverlayCanvas.CaptureMouse();
                Cursor = Cursors.Hand;
            }
            else if (_toolManager.ActiveMeasurementTool != MeasurementTool.None)
            {
                HandleMeasurementClick(e);
            }
            else if (_toolManager.ActiveAnnotationTool != AnnotationTool.None)
            {
                HandleAnnotationClick(e);
            }
        }

        private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                OverlayCanvas.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
            }
            else if (_toolManager.ActiveAnnotationTool == AnnotationTool.Freehand &&
                     _annotationService.CurrentPolyline != null)
            {
                _annotationService.EndFreehand();
            }
        }

        private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(OverlayCanvas);
            MousePosText.Text = $"Position: ({(int)mousePos.X}, {(int)mousePos.Y})";

            if (ChkShowHU.IsChecked == true && _currentDicomFile != null)
            {
                HUValueText.Text = _huCalculatorService.CalculateHU(_currentDicomFile, mousePos);
            }

            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(this);
                Vector delta = currentPoint - _lastPanPoint;
                _transformState.PanOffset = new Point(
                    _transformState.PanOffset.X + delta.X,
                    _transformState.PanOffset.Y + delta.Y
                );
                _lastPanPoint = currentPoint;
                ApplyTransforms();
            }
            else if (_toolManager.ActiveAnnotationTool == AnnotationTool.Freehand &&
                     e.LeftButton == MouseButtonState.Pressed &&
                     _annotationService.CurrentPolyline != null)
            {
                _annotationService.ContinueFreehand(mousePos);
            }
        }

        private void OverlayCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_toolManager.ActiveMeasurementTool == MeasurementTool.Area &&
                _measurementService.CurrentPoints.Count >= 3)
            {
                var area = _measurementService.CompleteAreaMeasurement(
                    OverlayCanvas, _pixelSpacingX, _pixelSpacingY);
                StatusText.Text = $"Area: {area:F2} mm²";
                _toolManager.DeactivateAll();
            }
            else
            {
                _toolManager.DeactivateAll();
            }
        }
        #endregion

        #region Measurement Tools
        private void MeasureDistance_Click(object sender, RoutedEventArgs e)
        {
            _toolManager.ActivateDistanceTool();
            _measurementService.ClearCurrentPoints();
        }

        private void MeasureAngle_Click(object sender, RoutedEventArgs e)
        {
            _toolManager.ActivateAngleTool();
            _measurementService.ClearCurrentPoints();
        }

        private void MeasureArea_Click(object sender, RoutedEventArgs e)
        {
            _toolManager.ActivateAreaTool();
            _measurementService.ClearCurrentPoints();
        }

        private void HandleMeasurementClick(MouseButtonEventArgs e)
        {
            Point clickPos = e.GetPosition(OverlayCanvas);
            _measurementService.AddPoint(clickPos);
            _measurementService.AddMarker(OverlayCanvas, clickPos);

            switch (_toolManager.ActiveMeasurementTool)
            {
                case MeasurementTool.Distance:
                    if (_measurementService.CurrentPoints.Count == 2)
                    {
                        var distance = _measurementService.CompleteDistanceMeasurement(
                            OverlayCanvas, _pixelSpacingX, _pixelSpacingY);
                        StatusText.Text = $"Distance: {distance:F2} mm";
                        _toolManager.DeactivateAll();
                    }
                    break;

                case MeasurementTool.Angle:
                    if (_measurementService.CurrentPoints.Count == 3)
                    {
                        var angle = _measurementService.CompleteAngleMeasurement(OverlayCanvas);
                        StatusText.Text = $"Angle: {angle:F1}°";
                        _toolManager.DeactivateAll();
                    }
                    break;

                case MeasurementTool.Area:
                    if (_measurementService.CurrentPoints.Count > 2)
                    {
                        _measurementService.DrawPolygonPreview(OverlayCanvas);
                    }
                    break;
            }
        }

        private void ClearMeasurements_Click(object sender, RoutedEventArgs e)
        {
            _measurementService.ClearAll(OverlayCanvas);
            StatusText.Text = "Measurements cleared";
        }
        #endregion

        #region Annotation Tools
        private void Freehand_Click(object sender, RoutedEventArgs e)
        {
            _toolManager.ActivateFreehandTool();
        }

        private void Arrow_Click(object sender, RoutedEventArgs e)
        {
            _toolManager.ActivateArrowTool();
            _tempPoints.Clear();
        }

        private void TextAnnotation_Click(object sender, RoutedEventArgs e)
        {
            _toolManager.ActivateTextTool();
        }

        private void HandleAnnotationClick(MouseButtonEventArgs e)
        {
            Point clickPos = e.GetPosition(OverlayCanvas);

            switch (_toolManager.ActiveAnnotationTool)
            {
                case AnnotationTool.Freehand:
                    if (_annotationService.CurrentPolyline == null)
                    {
                        _annotationService.StartFreehand(OverlayCanvas, clickPos);
                    }
                    break;

                case AnnotationTool.Arrow:
                    _tempPoints.Add(clickPos);
                    if (_tempPoints.Count == 2)
                    {
                        _annotationService.DrawArrow(OverlayCanvas, _tempPoints[0], _tempPoints[1]);
                        _tempPoints.Clear();
                        _toolManager.DeactivateAll();
                    }
                    break;

                case AnnotationTool.Text:
                    string text = Microsoft.VisualBasic.Interaction.InputBox(
                        "Enter annotation text:", "Text Annotation", "");
                    if (!string.IsNullOrEmpty(text))
                    {
                        _annotationService.AddText(OverlayCanvas, clickPos, text);
                    }
                    _toolManager.DeactivateAll();
                    break;
            }
        }

        private void ClearAnnotations_Click(object sender, RoutedEventArgs e)
        {
            _annotationService.ClearAll(OverlayCanvas);
            StatusText.Text = "Annotations cleared";
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
                    if (_currentSlice > 0)
                        SliceSlider.Value = _currentSlice - 1;
                    break;
                case Key.Down:
                    if (_currentSlice < _dicomSeries.Count - 1)
                        SliceSlider.Value = _currentSlice + 1;
                    break;
                case Key.Escape:
                    _toolManager.DeactivateAll();
                    break;
                case Key.Space:
                    if (_toolManager.ActiveMeasurementTool == MeasurementTool.Area &&
                        _measurementService.CurrentPoints.Count >= 3)
                    {
                        var area = _measurementService.CompleteAreaMeasurement(
                            OverlayCanvas, _pixelSpacingX, _pixelSpacingY);
                        StatusText.Text = $"Area: {area:F2} mm²";
                        _toolManager.DeactivateAll();
                    }
                    break;
            }
        }
        #endregion

        #region Metadata
        private void ShowMetadata(DicomFile file)
        {
            MetadataTree.Items.Clear();
            var metadata = _loaderService.GetMetadata(file);

            foreach (var (key, value) in metadata)
            {
                var node = new TreeViewItem { Header = $"{key}: {value}" };
                MetadataTree.Items.Add(node);
            }
        }
        #endregion

        #region Export
        private void ExportSnapshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog { Filter = "PNG Image|*.png" };
                if (dlg.ShowDialog() == true)
                {
                    _exportService.ExportSnapshot(OverlayCanvas, dlg.FileName);
                    StatusText.Text = $"Snapshot saved: {System.IO.Path.GetFileName(dlg.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting snapshot: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportMeasurements_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog { Filter = "JSON File|*.json|Text File|*.txt" };
                if (dlg.ShowDialog() == true)
                {
                    if (dlg.FileName.EndsWith(".json"))
                    {
                        _exportService.ExportMeasurementsAsJson(
                            _currentDicomFile,
                            new List<MeasurementData>(_measurementService.Measurements),
                            dlg.FileName);
                    }
                    else
                    {
                        _exportService.ExportMeasurementsAsText(
                            new List<MeasurementData>(_measurementService.Measurements),
                            dlg.FileName);
                    }
                    StatusText.Text = $"Report saved: {System.IO.Path.GetFileName(dlg.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting report: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}