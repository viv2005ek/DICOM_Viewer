namespace WpfApp1.Models
{
    public class MeasurementData
    {
        public string Type { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public enum MeasurementTool { None, Distance, Angle, Area }
    public enum AnnotationTool { None, Freehand, Arrow, Text }
}