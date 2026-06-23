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
            Logger.Log("Configuration loaded");
        }

        private static AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? CreateDefaultConfig();
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
                    DetectorCount = 620
                },
                DisplaySettings = new DisplaySettings
                {
                    Brightness = 1.0,
                    Contrast = 1.0,
                    ShowGrid = true,
                    ShowDetectorInfo = true
                },
                FilterSettings = new FilterSettings
                {
                    ActivePreset = "Standard",
                    CustomFilters = new List<ActiveFilter>()
                }
            };
        }

        private static void InitializeProfiles()
        {
            ScanProfiles.Clear();
            ScanProfiles.Add(new ScanProfile { Name = "Fast", ScanSpeed = 2.0 });
            ScanProfiles.Add(new ScanProfile { Name = "Standard", ScanSpeed = 1.0 });
            ScanProfiles.Add(new ScanProfile { Name = "Detailed", ScanSpeed = 0.5 });
        }

        private static void InitializeFilterPresets()
        {
            FilterPresets.Clear();
            FilterPresets.Add(new FilterPreset { Name = "Standard" });
            FilterPresets.Add(new FilterPreset { Name = "High Contrast" });
        }

        public static void ApplyProfile(ScanProfile profile)
        {
            Current.ScanSettings.Speed = profile.ScanSpeed;
            Save();
        }

        public static void ApplyFilterPreset(FilterPreset preset)
        {
            Current.FilterSettings.ActivePreset = preset.Name;
            Save();
        }
    }

    // Классы конфигурации...
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
    }

    public class DisplaySettings
    {
        public double Brightness { get; set; } = 1.0;
        public double Contrast { get; set; } = 1.0;
        public bool ShowGrid { get; set; } = true;
        public bool ShowDetectorInfo { get; set; } = true;
        public string ColorScheme { get; set; } = "Dark";
    }

    public class FilterSettings
    {
        public string ActivePreset { get; set; } = "Standard";
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
