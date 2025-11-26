using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace SEE_INSADE.Core.Filters
{
    public class AdvancedFilterManager
    {
        private readonly List<AdvancedFilter> _advancedFilters = new List<AdvancedFilter>();

        public IEnumerable<AdvancedFilter> Filters => _advancedFilters;

        public AdvancedFilterManager()
        {
            InitializeDefaultFilters();
        }

        private void InitializeDefaultFilters()
        {
            _advancedFilters.Add(new MaterialEnhancementFilter());
            _advancedFilters.Add(new EdgeDetectionFilter());
            _advancedFilters.Add(new NoiseReductionFilter());
            _advancedFilters.Add(new SharpnessFilter());
            _advancedFilters.Add(new ColorBalanceFilter());
            _advancedFilters.Add(new GammaCorrectionFilter());
            _advancedFilters.Add(new ThresholdFilter());
        }

        public void AddFilter(AdvancedFilter filter) => _advancedFilters.Add(filter);
        public void RemoveFilter(string name) => _advancedFilters.RemoveAll(f => f.Name == name);
        public void ClearFilters() => _advancedFilters.Clear();

        public Color ApplyAdvancedFilters(Color input, MaterialType material, double density)
        {
            Color result = input;
            foreach (var filter in _advancedFilters)
            {
                if (filter.IsEnabled)
                    result = filter.Apply(result, material, density);
            }
            return result;
        }
    }

    public abstract class AdvancedFilter : Filter
    {
        public abstract string Category { get; }
    }

    public class MaterialEnhancementFilter : AdvancedFilter
    {
        public override string Category { get; } = "Material Processing";

        public MaterialEnhancementFilter()
        {
            Name = "Material Enhancement";
            Description = "Enhances material-specific features";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            return material switch
            {
                MaterialType.HeavyMetal => EnhanceMetal(input, density),
                MaterialType.Organic => EnhanceOrganic(input, density),
                MaterialType.Electronics => EnhanceElectronics(input, density),
                MaterialType.Glass => EnhanceGlass(input, density),
                _ => input
            };
        }

        private Color EnhanceMetal(Color input, double density)
        {
            double factor = 1.0 + (density * Intensity * 0.5);
            return Color.FromRgb(
                (byte)Math.Clamp(input.R * factor, 0, 255),
                (byte)Math.Clamp(input.G * 0.8, 0, 255),
                (byte)Math.Clamp(input.B * 0.8, 0, 255)
            );
        }

        private Color EnhanceOrganic(Color input, double density)
        {
            double factor = 1.0 + (density * Intensity * 0.3);
            return Color.FromRgb(
                (byte)Math.Clamp(input.R * factor, 0, 255),
                (byte)Math.Clamp(input.G * factor, 0, 255),
                (byte)Math.Clamp(input.B * 0.9, 0, 255)
            );
        }

        private Color EnhanceElectronics(Color input, double density)
        {
            return Color.FromRgb(
                (byte)Math.Clamp(input.R * 1.2, 0, 255),
                (byte)Math.Clamp(input.G * 0.7, 0, 255),
                (byte)Math.Clamp(input.B * 1.3, 0, 255)
            );
        }

        private Color EnhanceGlass(Color input, double density)
        {
            return Color.FromRgb(
                (byte)Math.Clamp(input.R * 0.8, 0, 255),
                (byte)Math.Clamp(input.G * 1.4, 0, 255),
                (byte)Math.Clamp(input.B * 1.4, 0, 255)
            );
        }
    }

    public class EdgeDetectionFilter : AdvancedFilter
    {
        public override string Category { get; } = "Feature Detection";

        public EdgeDetectionFilter()
        {
            Name = "Edge Detection";
            Description = "Detects and enhances edges in the image";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            byte intensity = (byte)((input.R + input.G + input.B) / 3);
            byte edgeValue = (byte)(intensity > 128 ? 255 : 0);

            return Color.FromRgb(edgeValue, edgeValue, edgeValue);
        }
    }

    public class NoiseReductionFilter : AdvancedFilter
    {
        public override string Category { get; } = "Image Quality";

        public NoiseReductionFilter()
        {
            Name = "Noise Reduction";
            Description = "Reduces image noise while preserving details";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            double reduction = 1.0 - (Intensity * 0.2);
            return Color.FromRgb(
                (byte)Math.Clamp(input.R * reduction, 0, 255),
                (byte)Math.Clamp(input.G * reduction, 0, 255),
                (byte)Math.Clamp(input.B * reduction, 0, 255)
            );
        }
    }

    public class SharpnessFilter : AdvancedFilter
    {
        public override string Category { get; } = "Image Quality";

        public SharpnessFilter()
        {
            Name = "Sharpness";
            Description = "Enhances image sharpness and details";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            double sharpness = 1.0 + (Intensity * 0.5);
            return Color.FromRgb(
                (byte)Math.Clamp(input.R * sharpness, 0, 255),
                (byte)Math.Clamp(input.G * sharpness, 0, 255),
                (byte)Math.Clamp(input.B * sharpness, 0, 255)
            );
        }
    }

    public class ColorBalanceFilter : AdvancedFilter
    {
        public override string Category { get; } = "Color Adjustment";

        public ColorBalanceFilter()
        {
            Name = "Color Balance";
            Description = "Adjusts color balance and temperature";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            return Color.FromRgb(
                (byte)Math.Clamp(input.R * (1.0 + Intensity * 0.3), 0, 255),
                (byte)Math.Clamp(input.G * (1.0 + Intensity * 0.1), 0, 255),
                (byte)Math.Clamp(input.B * (1.0 - Intensity * 0.2), 0, 255)
            );
        }
    }

    public class GammaCorrectionFilter : AdvancedFilter
    {
        public override string Category { get; } = "Color Adjustment";

        public GammaCorrectionFilter()
        {
            Name = "Gamma Correction";
            Description = "Adjusts image gamma correction";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            double gamma = 1.0 + (Intensity * 2.0);
            return Color.FromRgb(
                (byte)Math.Clamp(Math.Pow(input.R / 255.0, gamma) * 255, 0, 255),
                (byte)Math.Clamp(Math.Pow(input.G / 255.0, gamma) * 255, 0, 255),
                (byte)Math.Clamp(Math.Pow(input.B / 255.0, gamma) * 255, 0, 255)
            );
        }
    }

    public class ThresholdFilter : AdvancedFilter
    {
        public override string Category { get; } = "Feature Detection";

        public ThresholdFilter()
        {
            Name = "Threshold";
            Description = "Applies binary threshold to image";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            byte intensity = (byte)((input.R + input.G + input.B) / 3);
            byte threshold = (byte)(128 * Intensity);
            byte result = intensity > threshold ? (byte)255 : (byte)0;

            return Color.FromRgb(result, result, result);
        }
    }
}