using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace SEE_INSADE.Core.Config
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = "Config/settings.json";
        private static AppConfig? _currentConfig;

        public static AppConfig Current => _currentConfig ??= LoadConfig();

        public static ObservableCollection<ScanProfile> ScanProfiles { get; private set; } = new ObservableCollection<ScanProfile>();
        public static ObservableCollection<FilterPreset> FilterPresets { get; private set; } = new ObservableCollection<FilterPreset>();

        public static event Action<AppConfig>? ConfigChanged;

        static ConfigManager()
        {
            InitializeProfiles();
            InitializeFilterPresets();
        }

        public static void Load()
        {
            _currentConfig = LoadConfig();
            NormalizeConfig(_currentConfig);
            Logger.Log("Configuration loaded");
        }

        private static AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json) ?? CreateDefaultConfig();
                    NormalizeConfig(config);
                    return config;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load config: {ex.Message}");
            }

            return CreateDefaultConfig();
        }

        public static void Save()
        {
            try
            {
                NormalizeConfig(Current);
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? ".");
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(ConfigPath, json);

                ConfigChanged?.Invoke(Current);
                Logger.Log("Configuration saved");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save config: {ex.Message}");
            }
        }

        private static AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                ScanSettings = new ScanSettings
                {
                    Width = 1400,
                    Height = 620,
                    Speed = 1.0,
                    AutoStart = false,
                    DetectorCount = 620,
                    OperationMode = "ArchivePlayback",
                    ArchiveScanFolder = @"C:\Users\nikol\OneDrive\Desktop\03"
                },
                DisplaySettings = new DisplaySettings
                {
                    Brightness = 1.0,
                    Contrast = 1.0,
                    ShowGrid = true,
                    ShowDetectorInfo = true,
                    UseGpuAcceleration = false,
                    RenderQualityMode = "Balanced",
                    LastOperatorPreset = "Airport Standard",
                    ShowProDashboard = true,
                    ShowHotkeyHints = true,
                    SnapshotDirectory = "Scans",
                    AutoSaveAnalysisReport = true
                },
                FilterSettings = new FilterSettings
                {
                    ActivePreset = "Airport Standard",
                    CustomFilters = new List<ActiveFilter>()
                }
            };
        }

        private static void NormalizeConfig(AppConfig? config)
        {
            if (config == null)
                return;

            config.ScanSettings ??= new ScanSettings();
            config.DisplaySettings ??= new DisplaySettings();
            config.FilterSettings ??= new FilterSettings();
            config.FilterSettings.CustomFilters ??= new List<ActiveFilter>();

            if (string.IsNullOrWhiteSpace(config.DisplaySettings.RenderQualityMode))
                config.DisplaySettings.RenderQualityMode = "Balanced";

            if (string.IsNullOrWhiteSpace(config.DisplaySettings.LastOperatorPreset))
                config.DisplaySettings.LastOperatorPreset = "Airport Standard";

            if (string.IsNullOrWhiteSpace(config.DisplaySettings.SnapshotDirectory))
                config.DisplaySettings.SnapshotDirectory = "Scans";

            if (string.IsNullOrWhiteSpace(config.FilterSettings.ActivePreset))
                config.FilterSettings.ActivePreset = "Airport Standard";

            if (string.IsNullOrWhiteSpace(config.ScanSettings.OperationMode))
                config.ScanSettings.OperationMode = "ArchivePlayback";

            if (string.IsNullOrWhiteSpace(config.ScanSettings.ArchiveScanFolder))
                config.ScanSettings.ArchiveScanFolder = @"C:\Users\nikol\OneDrive\Desktop\03";
        }

        private static void InitializeProfiles()
        {
            ScanProfiles.Clear();
            ScanProfiles.Add(new ScanProfile { Name = "Fast", Description = "High speed scanning, lower visual inspection time", ScanSpeed = 2.0, DetectorSensitivity = 0.95 });
            ScanProfiles.Add(new ScanProfile { Name = "Standard", Description = "Balanced airport inspection profile", ScanSpeed = 1.0, DetectorSensitivity = 1.0 });
            ScanProfiles.Add(new ScanProfile { Name = "Detailed", Description = "Slower scan for detailed analysis", ScanSpeed = 0.5, DetectorSensitivity = 1.15 });
        }

        private static void InitializeFilterPresets()
        {
            FilterPresets.Clear();
            FilterPresets.Add(new FilterPreset { Name = "Airport Standard", Description = "Balanced color view for everyday baggage inspection" });
            FilterPresets.Add(new FilterPreset { Name = "Maximum Penetration", Description = "Dark dense objects and steel steps are easier to inspect" });
            FilterPresets.Add(new FilterPreset { Name = "Organic Threat", Description = "Highlights organics, liquids, sugar and plastic" });
            FilterPresets.Add(new FilterPreset { Name = "Metal Search", Description = "Highlights metals and electronics" });
            FilterPresets.Add(new FilterPreset { Name = "Edge Inspection", Description = "Emphasizes edges, wires and small details" });
            FilterPresets.Add(new FilterPreset { Name = "Low Noise", Description = "Smoother image for long monitoring sessions" });
            FilterPresets.Add(new FilterPreset { Name = "Density Inspector", Description = "Density map for penetration and shielding checks" });
            FilterPresets.Add(new FilterPreset { Name = "High Contrast", Description = "Sharper image with stronger contrast" });
        }

        public static void ApplyProfile(ScanProfile profile)
        {
            Current.ScanSettings.Speed = profile.ScanSpeed;
            Current.ScanSettings.DetectorSensitivity = profile.DetectorSensitivity;
            Save();
        }

        public static void ApplyFilterPreset(FilterPreset preset)
        {
            Current.FilterSettings.ActivePreset = preset.Name;
            Current.DisplaySettings.LastOperatorPreset = preset.Name;
            Save();
        }
    }

    public class AppConfig
    {
        public ScanSettings ScanSettings { get; set; } = new ScanSettings();
        public DisplaySettings DisplaySettings { get; set; } = new DisplaySettings();
        public FilterSettings FilterSettings { get; set; } = new FilterSettings();
    }

    public class ScanSettings
    {
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 400;
        public double Speed { get; set; } = 1.0;
        public bool AutoStart { get; set; } = false;
        public int DetectorCount { get; set; } = 400;
            public double DetectorSensitivity { get; set; } = 0.8;
        public string OperationMode { get; set; } = "ArchivePlayback";
        public string ArchiveScanFolder { get; set; } = @"C:\Users\nikol\OneDrive\Desktop\03";
    }

    public class DisplaySettings
    {
        public double Brightness { get; set; } = 1.0;
        public double Contrast { get; set; } = 1.0;
        public bool ShowGrid { get; set; } = true;
        public bool ShowDetectorInfo { get; set; } = true;
        public string ColorScheme { get; set; } = "Dark";

        // Experimental: ComputeSharp/DX12 GPU filters. If unavailable, ImageProcessor falls back to CPU.
        public bool UseGpuAcceleration { get; set; } = false;

        // Pro UI/settings layer.
        public string RenderQualityMode { get; set; } = "Balanced";
        public string LastOperatorPreset { get; set; } = "Airport Standard";
        public bool ShowProDashboard { get; set; } = true;
        public bool ShowHotkeyHints { get; set; } = true;
        public string SnapshotDirectory { get; set; } = "Scans";
        public bool AutoSaveAnalysisReport { get; set; } = true;
    }

    public class FilterSettings
    {
        public string ActivePreset { get; set; } = "Airport Standard";
        public List<ActiveFilter> CustomFilters { get; set; } = new List<ActiveFilter>();
    }

    public class ScanProfile
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double ScanSpeed { get; set; } = 1.0;
        public double DetectorSensitivity { get; set; } = 0.8;
    }

    public class FilterPreset
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<ActiveFilter> Filters { get; set; } = new List<ActiveFilter>();
    }

    public class ActiveFilter
    {
        public string FilterType { get; set; } = "";
        public double Intensity { get; set; } = 1.0;
        public bool IsEnabled { get; set; } = true;
    }
}
