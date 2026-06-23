using System.Windows;
using SEE_INSADE.Core.Localization;

namespace SEE_INSADE.UI.Dialogs
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LocalizationHelper.Apply(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                LocalizationManager.Instance.T("message.settingsSaved"),
                LocalizationManager.Instance.T("message.success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                LocalizationManager.Instance.T("message.settingsApplied"),
                LocalizationManager.Instance.T("message.success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
