using System.Windows;
using System.Windows.Controls;

namespace SEE_INSADE.UI.Dialogs
{
    public partial class CalibrationWizard : Window
    {
        private int _currentStep = 0;
        private readonly string[] _steps = { "Preparation", "DetectorCheck", "EnergyCalibration", "GeometrySetup", "ImageQuality", "FinalVerification" };

        public CalibrationWizard()
        {
            InitializeComponent();
            InitializeWizard();
        }

        private void InitializeWizard()
        {
            StepsListBox.SelectedIndex = 0;
            UpdateStepVisibility();
            UpdateProgress();
        }

        private void StepsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StepsListBox.SelectedIndex >= 0)
            {
                _currentStep = StepsListBox.SelectedIndex;
                UpdateStepVisibility();
                UpdateProgress();
            }
        }

        private void NextStep_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < _steps.Length - 1)
            {
                _currentStep++;
                StepsListBox.SelectedIndex = _currentStep;
                UpdateStepVisibility();
                UpdateProgress();
            }
        }

        private void PreviousStep_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 0)
            {
                _currentStep--;
                StepsListBox.SelectedIndex = _currentStep;
                UpdateStepVisibility();
                UpdateProgress();
            }
        }

        private void UpdateStepVisibility()
        {
            PreparationStep.Visibility = _currentStep == 0 ? Visibility.Visible : Visibility.Collapsed;
            DetectorCheckStep.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            // Add other steps as needed
        }

        private void UpdateProgress()
        {
            double progress = ((double)_currentStep + 1) / _steps.Length * 100;
            OverallProgress.Value = progress;
        }

        private void RestartWizard_Click(object sender, RoutedEventArgs e)
        {
            _currentStep = 0;
            StepsListBox.SelectedIndex = 0;
            UpdateStepVisibility();
            UpdateProgress();
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Calibration profile saved successfully!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}