using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace SEE_INSADE.Core.Filters
{
    public class FilterPipeline
    {
        private readonly List<Filter> _filters = new List<Filter>();

        public Color ApplyFilters(Color input, MaterialType material, double density)
        {
            Color result = input;
            foreach (var filter in _filters)
            {
                if (filter.IsEnabled)
                    result = filter.Apply(result, material, density);
            }
            return result;
        }

        public void AddFilter(Filter filter) => _filters.Add(filter);
        public void RemoveFilter(string name) => _filters.RemoveAll(f => f.Name == name);
        public void Clear() => _filters.Clear();
        public void UpdateFilters(FilterPipeline other)
        {
            _filters.Clear();
            _filters.AddRange(other._filters);
        }

        public IEnumerable<Filter> GetActiveFilters() => _filters.FindAll(f => f.IsEnabled);
        public int GetActiveFiltersCount() => _filters.FindAll(f => f.IsEnabled).Count;
    }

    public abstract class Filter
    {
        public string Name { get; protected set; } = "";
        public string Description { get; protected set; } = "";
        public bool IsEnabled { get; set; } = true;
        public double Intensity { get; set; } = 1.0;
        public abstract Color Apply(Color input, MaterialType material, double density);
    }

    public class BrightnessFilter : Filter
    {
        public BrightnessFilter()
        {
            Name = "Brightness";
            Description = "Adjust brightness";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            double factor = 0.5 + (Intensity * 0.5);
            return Color.FromRgb(
                (byte)Math.Clamp(input.R * factor, 0, 255),
                (byte)Math.Clamp(input.G * factor, 0, 255),
                (byte)Math.Clamp(input.B * factor, 0, 255)
            );
        }
    }

    public class ContrastFilter : Filter
    {
        public ContrastFilter()
        {
            Name = "Contrast";
            Description = "Adjust contrast";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            double factor = Intensity * 2.0;
            return Color.FromRgb(
                (byte)Math.Clamp(((input.R / 255.0 - 0.5) * factor + 0.5) * 255, 0, 255),
                (byte)Math.Clamp(((input.G / 255.0 - 0.5) * factor + 0.5) * 255, 0, 255),
                (byte)Math.Clamp(((input.B / 255.0 - 0.5) * factor + 0.5) * 255, 0, 255)
            );
        }
    }

    // Добавим только базовые фильтры для начала
    public class InvertFilter : Filter
    {
        public InvertFilter()
        {
            Name = "Invert";
            Description = "Invert colors";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            return Color.FromRgb(
                (byte)(255 - input.R),
                (byte)(255 - input.G),
                (byte)(255 - input.B)
            );
        }
    }

    public class GrayscaleFilter : Filter
    {
        public GrayscaleFilter()
        {
            Name = "Grayscale";
            Description = "Convert to grayscale";
        }

        public override Color Apply(Color input, MaterialType material, double density)
        {
            byte gray = (byte)(input.R * 0.299 + input.G * 0.587 + input.B * 0.114);
            return Color.FromRgb(gray, gray, gray);
        }
    }
}
