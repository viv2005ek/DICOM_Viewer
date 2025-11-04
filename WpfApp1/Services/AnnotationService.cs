using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfApp1.Services
{
    public class AnnotationService
    {
        private readonly List<UIElement> _annotationElements = new();
        private readonly List<Point> _freehandPoints = new();
        private Polyline _currentPolyline;

        public Polyline CurrentPolyline => _currentPolyline;

        public void StartFreehand(Canvas canvas, Point startPoint)
        {
            _currentPolyline = new Polyline { Stroke = Brushes.Red, StrokeThickness = 2 };
            _freehandPoints.Clear();
            _freehandPoints.Add(startPoint);
            _currentPolyline.Points.Add(startPoint);
            canvas.Children.Add(_currentPolyline);
            _annotationElements.Add(_currentPolyline);
        }

        public void ContinueFreehand(Point point)
        {
            if (_currentPolyline != null)
            {
                _freehandPoints.Add(point);
                _currentPolyline.Points.Add(point);
            }
        }

        public void EndFreehand()
        {
            _currentPolyline = null;
            _freehandPoints.Clear();
        }

        public void DrawArrow(Canvas canvas, Point start, Point end)
        {
            var line = new Line
            {
                X1 = start.X, Y1 = start.Y,
                X2 = end.X, Y2 = end.Y,
                Stroke = Brushes.Orange,
                StrokeThickness = 3
            };
            canvas.Children.Add(line);
            _annotationElements.Add(line);

            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            double arrowLength = 15;
            double arrowAngle = Math.PI / 6;

            Point p1 = new Point(
                end.X - arrowLength * Math.Cos(angle - arrowAngle),
                end.Y - arrowLength * Math.Sin(angle - arrowAngle));
            Point p2 = new Point(
                end.X - arrowLength * Math.Cos(angle + arrowAngle),
                end.Y - arrowLength * Math.Sin(angle + arrowAngle));

            var arrowHead = new Polygon
            {
                Fill = Brushes.Orange,
                Stroke = Brushes.Orange,
                StrokeThickness = 2
            };
            arrowHead.Points.Add(end);
            arrowHead.Points.Add(p1);
            arrowHead.Points.Add(p2);
            canvas.Children.Add(arrowHead);
            _annotationElements.Add(arrowHead);
        }

        public void AddText(Canvas canvas, Point position, string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = Brushes.Yellow,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(5),
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(tb, position.X);
            Canvas.SetTop(tb, position.Y);
            canvas.Children.Add(tb);
            _annotationElements.Add(tb);
        }

        public void ClearAll(Canvas canvas)
        {
            foreach (var element in _annotationElements)
            {
                canvas.Children.Remove(element);
            }
            _annotationElements.Clear();
        }
    }
}