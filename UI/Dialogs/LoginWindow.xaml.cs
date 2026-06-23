using System.Windows;
using System.Windows.Input;
using SEE_INSADE.Core.Localization;
using SEE_INSADE.Core.Security;

namespace SEE_INSADE.UI.Dialogs
{
    public partial class LoginWindow : Window
    {
        private readonly UserAccessService _users = UserAccessService.Instance;

        public LoginWindow()
        {
            InitializeComponent();
            LocalizationHelper.Apply(this);
            UserNameTextBox.Focus();
            UserNameTextBox.SelectAll();
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
        {
            TrySignIn();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                TrySignIn();
        }

        private void TrySignIn()
        {
            if (_users.Authenticate(UserNameTextBox.Text, PasswordBox.Password, out var user))
            {
                DialogResult = true;
                Close();
                return;
            }

            ErrorText.Text = LocalizationManager.Instance.T("login.invalid");
            PasswordBox.SelectAll();
            PasswordBox.Focus();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
