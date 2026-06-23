using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SEE_INSADE.Core.Config;
using SEE_INSADE.Core.Localization;
using SEE_INSADE.Core.Security;
using SEE_INSADE.Services.Scanning;

namespace SEE_INSADE.UI.Plugins
{
    public partial class ConfigurationPluginWindow : Window
    {
        private readonly ScanService _scanService;
        private readonly UserAccessService _users = UserAccessService.Instance;

        public ConfigurationPluginWindow(ScanService scanService)
        {
            InitializeComponent();
            LocalizationHelper.Apply(this);

            _scanService = scanService;
            RoleComboBox.ItemsSource = Enum.GetValues(typeof(UserRole));
            PluginListBox.ItemsSource = new[]
            {
                "see-insade.configuration - deep system configuration",
                "see-insade.detector-check - live detector monitor"
            };

            _users.Load();
            LoadConfiguration();
            LoadUsers();
        }

        private void LoadConfiguration()
        {
            ConfigManager.Load();
            var config = ConfigManager.Current;

            WidthTextBox.Text = config.ScanSettings.Width.ToString(CultureInfo.InvariantCulture);
            HeightTextBox.Text = config.ScanSettings.Height.ToString(CultureInfo.InvariantCulture);
            DetectorCountTextBox.Text = config.ScanSettings.DetectorCount.ToString(CultureInfo.InvariantCulture);
            SpeedTextBox.Text = config.ScanSettings.Speed.ToString(CultureInfo.InvariantCulture);
            DetectorSensitivityTextBox.Text = config.ScanSettings.DetectorSensitivity.ToString(CultureInfo.InvariantCulture);
            AutoStartCheckBox.IsChecked = config.ScanSettings.AutoStart;

            BrightnessTextBox.Text = config.DisplaySettings.Brightness.ToString(CultureInfo.InvariantCulture);
            ContrastTextBox.Text = config.DisplaySettings.Contrast.ToString(CultureInfo.InvariantCulture);
            ColorSchemeTextBox.Text = config.DisplaySettings.ColorScheme;
            ShowGridCheckBox.IsChecked = config.DisplaySettings.ShowGrid;
            ShowDetectorInfoCheckBox.IsChecked = config.DisplaySettings.ShowDetectorInfo;

            ActivePresetTextBox.Text = config.FilterSettings.ActivePreset;

            CurrentUserText.Text = $"Current user: {_users.CurrentUser?.DisplayName ?? "none"}";
            StatusText.Text = "Configuration loaded";
        }

        private void SaveConfiguration()
        {
            var config = ConfigManager.Current;

            config.ScanSettings.Width = ReadInt(WidthTextBox, config.ScanSettings.Width, 64, 4096);
            config.ScanSettings.Height = ReadInt(HeightTextBox, config.ScanSettings.Height, 32, 2048);
            config.ScanSettings.DetectorCount = ReadInt(DetectorCountTextBox, config.ScanSettings.DetectorCount, 16, 4096);
            config.ScanSettings.Speed = ReadDouble(SpeedTextBox, config.ScanSettings.Speed, 0.05, 10.0);
            config.ScanSettings.DetectorSensitivity = ReadDouble(DetectorSensitivityTextBox, config.ScanSettings.DetectorSensitivity, 0.05, 5.0);
            config.ScanSettings.AutoStart = AutoStartCheckBox.IsChecked == true;

            config.DisplaySettings.Brightness = ReadDouble(BrightnessTextBox, config.DisplaySettings.Brightness, 0.05, 5.0);
            config.DisplaySettings.Contrast = ReadDouble(ContrastTextBox, config.DisplaySettings.Contrast, 0.05, 5.0);
            config.DisplaySettings.ColorScheme = ColorSchemeTextBox.Text.Trim();
            config.DisplaySettings.ShowGrid = ShowGridCheckBox.IsChecked == true;
            config.DisplaySettings.ShowDetectorInfo = ShowDetectorInfoCheckBox.IsChecked == true;

            config.FilterSettings.ActivePreset = ActivePresetTextBox.Text.Trim();

            ConfigManager.Save();
            _users.Save();
            StatusText.Text = "Configuration saved";
        }

        private void LoadUsers()
        {
            UsersGrid.ItemsSource = null;
            UsersGrid.ItemsSource = _users.Users;

            if (_users.Users.Count > 0)
                UsersGrid.SelectedIndex = 0;
        }

        private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersGrid.SelectedItem is not UserAccount user)
                return;

            UserNameTextBox.Text = user.UserName;
            DisplayNameTextBox.Text = user.DisplayName;
            RoleComboBox.SelectedItem = user.Role;
            AccessLevelTextBox.Text = user.AccessLevel.ToString(CultureInfo.InvariantCulture);
            PasswordBox.Password = "";
            UserActiveCheckBox.IsChecked = user.IsActive;
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var user = ReadUserFromFields(new UserAccount());
            _users.AddUser(user);
            LoadUsers();
            UsersGrid.SelectedItem = user;
            StatusText.Text = "User added";
        }

        private void UpdateUser_Click(object sender, RoutedEventArgs e)
        {
            if (UsersGrid.SelectedItem is not UserAccount user)
                return;

            ReadUserFromFields(user);
            _users.Save();
            LoadUsers();
            UsersGrid.SelectedItem = _users.Users.FirstOrDefault(item => item.Id == user.Id);
            StatusText.Text = "User updated";
        }

        private void RemoveUser_Click(object sender, RoutedEventArgs e)
        {
            if (UsersGrid.SelectedItem is not UserAccount user)
                return;

            _users.RemoveUser(user);
            LoadUsers();
            StatusText.Text = "User removed";
        }

        private UserAccount ReadUserFromFields(UserAccount user)
        {
            user.UserName = string.IsNullOrWhiteSpace(UserNameTextBox.Text)
                ? "user"
                : UserNameTextBox.Text.Trim();
            user.DisplayName = string.IsNullOrWhiteSpace(DisplayNameTextBox.Text)
                ? user.UserName
                : DisplayNameTextBox.Text.Trim();
            user.Role = RoleComboBox.SelectedItem is UserRole role ? role : UserRole.Observer;
            user.AccessLevel = ReadInt(AccessLevelTextBox, UserAccessService.GetDefaultAccessLevel(user.Role), 0, 100);
            user.IsActive = UserActiveCheckBox.IsChecked == true;
            user.Permissions = UserAccessService.GetDefaultPermissions(user.Role);

            if (!string.IsNullOrWhiteSpace(PasswordBox.Password))
                user.PasswordHash = UserAccessService.HashPassword(PasswordBox.Password);

            return user;
        }

        private static int ReadInt(TextBox textBox, int fallback, int min, int max)
        {
            return int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? Math.Clamp(value, min, max)
                : fallback;
        }

        private static double ReadDouble(TextBox textBox, double fallback, double min, double max)
        {
            return double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? Math.Clamp(value, min, max)
                : fallback;
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            _users.Load();
            LoadConfiguration();
            LoadUsers();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveConfiguration();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
