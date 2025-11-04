using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public class MeasurementService
    {
        private readonly List<MeasurementData> _measurements = new();
        private readonly List<UIElement> _measurementElements = new();
        private readonly List<Point> _currentPoints = new();

        public IReadOnlyList<MeasurementData> Measurements => _measurements.AsReadOnly();
        public IReadOnlyList<Point> CurrentPoints => _currentPoints.AsReadOnly();

        public void AddPoint(Point point)
        {
            _currentPoints.Add(point);
        }

        public void ClearCurrentPoints()
        {
            _currentPoints.Clear();
        }

        public void AddMarker(Canvas canvas, Point point)
        {
            var marker = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = Brushes.Yellow,
                Stroke = Brushes.Red,
                StrokeThickness = 1
            };
            Canvas.SetLeft(marker, point.X - 3);
            Canvas.SetTop(marker, point.Y - 3);
            canvas.Children.Add(marker);
            _measurementElements.Add(marker);
        }

        public double CompleteDistanceMeasurement(Canvas canvas, double pixelSpacingX, double pixelSpacingY)
        {
            if (_currentPoints.Count < 2) return 0;

            Point p1 = _currentPoints[0];
            Point p2 = _currentPoints[1];

            var line = new Line
            {
                X1 = p1.X, Y1 = p1.Y,
                X2 = p2.X, Y2 = p2.Y,
                Stroke = Brushes.Cyan,
                StrokeThickness = 2
            };
            canvas.Children.Add(line);
            _measurementElements.Add(line);

            double dx = (p2.X - p1.X) * pixelSpacingX;
            double dy = (p2.Y - p1.Y) * pixelSpacingY;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            var label = new TextBlock
            {
                Text = $"{distance:F2} mm",
                Foreground = Brushes.Yellow,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(3),
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, (p1.X + p2.X) / 2);
            Canvas.SetTop(label, (p1.Y + p2.Y) / 2 - 20);
            canvas.Children.Add(label);
            _measurementElements.Add(label);

            _measurements.Add(new MeasurementData { Type = "Distance", Value = distance, Unit = "mm" });
            _currentPoints.Clear();

            return distance;
        }

        public double CompleteAngleMeasurement(Canvas canvas)
        {
            if (_currentPoints.Count < 3) return 0;

            Point p1 = _currentPoints[0];
            Point p2 = _currentPoints[1];
            Point p3 = _currentPoints[2];

            var line1 = new Line { X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y, Stroke = Brushes.Cyan, StrokeThickness = 2 };
            var line2 = new Line { X1 = p2.X, Y1 = p2.Y, X2 = p3.X, Y2 = p3.Y, Stroke = Brushes.Cyan, StrokeThickness = 2 };
            canvas.Children.Add(line1);
            canvas.Children.Add(line2);
            _measurementElements.Add(line1);
            _measurementElements.Add(line2);

            Vector v1 = new Vector(p1.X - p2.X, p1.Y - p2.Y);
            Vector v2 = new Vector(p3.X - p2.X, p3.Y - p2.Y);
            double angle = Vector.AngleBetween(v1, v2);
            if (angle < 0) angle += 360;

            var label = new TextBlock
            {
                Text = $"{angle:F1}°",
                Foreground = Brushes.Yellow,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(3),
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, p2.X + 10);
            Canvas.SetTop(label, p2.Y - 20);
            canvas.Children.Add(label);
            _measurementElements.Add(label);

            _measurements.Add(new MeasurementData { Type = "Angle", Value = angle, Unit = "degrees" });
            _currentPoints.Clear();

            return angle;
        }

        public void DrawPolygonPreview(Canvas canvas)
        {
            var oldPreview = _measurementElements.OfType<Polygon>().FirstOrDefault();
            if (oldPreview != null)
            {
                canvas.Children.Remove(oldPreview);
                _measurementElements.Remove(oldPreview);
            }

            var polygon = new Polygon
            {
                Stroke = Brushes.Cyan,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(50, 0, 255, 255))
            };
            foreach (var pt in _currentPoints)
            {
                polygon.Points.Add(pt);
            }
            canvas.Children.Add(polygon);
            _measurementElements.Add(polygon);
        }

        public double CompleteAreaMeasurement(Canvas canvas, double pixelSpacingX, double pixelSpacingY)
        {
            if (_currentPoints.Count < 3) return 0;

            double area = 0;
            for (int i = 0; i < _currentPoints.Count; i++)
            {
                Point p1 = _currentPoints[i];
                Point p2 = _currentPoints[(i + 1) % _currentPoints.Count];
                area += (p1.X * p2.Y - p2.X * p1.Y);
            }
            area = Math.Abs(area / 2.0) * pixelSpacingX * pixelSpacingY;

            double cx = _currentPoints.Average(p => p.X);
            double cy = _currentPoints.Average(p => p.Y);

            var label = new TextBlock
            {
                Text = $"{area:F2} mm²",
                Foreground = Brushes.Yellow,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(3),
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, cx);
            Canvas.SetTop(label, cy - 20);
            canvas.Children.Add(label);
            _measurementElements.Add(label);

            _measurements.Add(new MeasurementData { Type = "Area", Value = area, Unit = "mm²" });
            _currentPoints.Clear();

            return area;
        }

        public void ClearAll(Canvas canvas)
        {
            foreach (var element in _measurementElements)
            {
                canvas.Children.Remove(element);
            }
            _measurementElements.Clear();
            _measurements.Clear();
            _currentPoints.Clear();
        }
    }
}