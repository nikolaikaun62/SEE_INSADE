namespace SEE_INSADE.Core.Imaging
{
    public sealed class OperatorFilterSettings
    {
        public OperatorFilterMode Mode { get; set; } = OperatorFilterMode.EnhancedColor;
        public double Strength { get; set; } = 1.0;
        public bool BrightnessEnabled { get; set; }
        public double Brightness { get; set; } = 1.0;
        public bool ContrastEnabled { get; set; }
        public double Contrast { get; set; } = 1.0;
        public bool MaterialEnhancementEnabled { get; set; }
        public bool EdgeDetectionEnabled { get; set; }
        public bool NoiseReductionEnabled { get; set; }
    }
}
