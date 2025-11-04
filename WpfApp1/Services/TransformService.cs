using System.Windows;
using System.Windows.Media;

namespace WpfApp1.Services
{
    public class TransformState
    {
        public double Scale { get; set; } = 1.0;
        public double Rotation { get; set; } = 0.0;
        public bool IsFlippedHorizontal { get; set; } = false;
        public bool IsFlippedVertical { get; set; } = false;
        public Point PanOffset { get; set; } = new Point(0, 0);
    }

    public class TransformService
    {
        public TransformGroup CreateTransformGroup(TransformState state, double centerX, double centerY)
        {
            var group = new TransformGroup();
            
            group.Children.Add(new ScaleTransform(
                state.Scale * (state.IsFlippedHorizontal ? -1 : 1),
                state.Scale * (state.IsFlippedVertical ? -1 : 1),
                centerX,
                centerY));
            
            group.Children.Add(new RotateTransform(state.Rotation, centerX, centerY));
            group.Children.Add(new TranslateTransform(state.PanOffset.X, state.PanOffset.Y));
            
            return group;
        }

        public void ZoomIn(TransformState state, double factor = 1.2)
        {
            state.Scale *= factor;
        }

        public void ZoomOut(TransformState state, double factor = 1.2)
        {
            state.Scale /= factor;
        }

        public void RotateLeft(TransformState state)
        {
            state.Rotation = (state.Rotation + 90) % 360;
        }

        public void RotateRight(TransformState state)
        {
            state.Rotation = (state.Rotation - 90) % 360;
            if (state.Rotation < 0) state.Rotation += 360;
        }

        public void FlipHorizontal(TransformState state)
        {
            state.IsFlippedHorizontal = !state.IsFlippedHorizontal;
        }

        public void FlipVertical(TransformState state)
        {
            state.IsFlippedVertical = !state.IsFlippedVertical;
        }

        public void Reset(TransformState state)
        {
            state.Scale = 1.0;
            state.Rotation = 0.0;
            state.IsFlippedHorizontal = false;
            state.IsFlippedVertical = false;
            state.PanOffset = new Point(0, 0);
        }

        public void CalculateFitToScreen(TransformState state, double viewWidth, double viewHeight, 
            double imageWidth, double imageHeight)
        {
            double scaleX = viewWidth / imageWidth;
            double scaleY = viewHeight / imageHeight;
            state.Scale = Math.Min(scaleX, scaleY) * 0.95;
            state.PanOffset = new Point(0, 0);
        }
    }
}