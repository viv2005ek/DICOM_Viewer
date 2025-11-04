using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp1.Models;

namespace WpfApp1.Managers
{
    public class ToolManager
    {
        public MeasurementTool ActiveMeasurementTool { get; private set; } = MeasurementTool.None;
        public AnnotationTool ActiveAnnotationTool { get; private set; } = AnnotationTool.None;

        private readonly Button _btnDistance, _btnAngle, _btnArea;
        private readonly Button _btnFreehand, _btnArrow, _btnText;
        private readonly TextBlock _activeToolText;
        private readonly FrameworkElement _mainWindow;

        // Define colors for active states
        private readonly SolidColorBrush _inactiveBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1f35"));
        private readonly SolidColorBrush _measurementActiveBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
        private readonly SolidColorBrush _annotationActiveBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

        public ToolManager(Button btnDistance, Button btnAngle, Button btnArea,
                          Button btnFreehand, Button btnArrow, Button btnText,
                          TextBlock activeToolText, FrameworkElement mainWindow)
        {
            _btnDistance = btnDistance;
            _btnAngle = btnAngle;
            _btnArea = btnArea;
            _btnFreehand = btnFreehand;
            _btnArrow = btnArrow;
            _btnText = btnText;
            _activeToolText = activeToolText;
            _mainWindow = mainWindow;
        }

        public void ActivateDistanceTool()
        {
            DeactivateAll();
            ActiveMeasurementTool = MeasurementTool.Distance;
            _activeToolText.Text = "Active Tool: Distance (Click 2 points)";
            _btnDistance.Background = _measurementActiveBackground;
            _mainWindow.Cursor = Cursors.Cross;
        }

        public void ActivateAngleTool()
        {
            DeactivateAll();
            ActiveMeasurementTool = MeasurementTool.Angle;
            _activeToolText.Text = "Active Tool: Angle (Click 3 points)";
            _btnAngle.Background = _measurementActiveBackground;
            _mainWindow.Cursor = Cursors.Cross;
        }

        public void ActivateAreaTool()
        {
            DeactivateAll();
            ActiveMeasurementTool = MeasurementTool.Area;
            _activeToolText.Text = "Active Tool: Area/ROI (Click points, right-click to finish)";
            _btnArea.Background = _measurementActiveBackground;
            _mainWindow.Cursor = Cursors.Cross;
        }

        public void ActivateFreehandTool()
        {
            DeactivateAll();
            ActiveAnnotationTool = AnnotationTool.Freehand;
            _activeToolText.Text = "Active Tool: Freehand (Click and drag)";
            _btnFreehand.Background = _annotationActiveBackground;
            _mainWindow.Cursor = Cursors.Pen;
        }

        public void ActivateArrowTool()
        {
            DeactivateAll();
            ActiveAnnotationTool = AnnotationTool.Arrow;
            _activeToolText.Text = "Active Tool: Arrow (Click 2 points)";
            _btnArrow.Background = _annotationActiveBackground;
            _mainWindow.Cursor = Cursors.Cross;
        }

        public void ActivateTextTool()
        {
            DeactivateAll();
            ActiveAnnotationTool = AnnotationTool.Text;
            _activeToolText.Text = "Active Tool: Text (Click to place)";
            _btnText.Background = _annotationActiveBackground;
            _mainWindow.Cursor = Cursors.IBeam;
        }

        public void DeactivateAll()
        {
            ActiveMeasurementTool = MeasurementTool.None;
            ActiveAnnotationTool = AnnotationTool.None;
            _activeToolText.Text = "Active Tool: None";
            _mainWindow.Cursor = Cursors.Arrow;

            // Reset all buttons to inactive state
            _btnDistance.Background = _inactiveBackground;
            _btnAngle.Background = _inactiveBackground;
            _btnArea.Background = _inactiveBackground;
            _btnFreehand.Background = _inactiveBackground;
            _btnArrow.Background = _inactiveBackground;
            _btnText.Background = _inactiveBackground;
        }
    }
}