using SEE_INSADE.Models;
using SEE_INSADE.Services;
using SEE_INSADE.Utilities;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SEE_INSADE.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IDetectorService _detectorService;
        private readonly IScanEmulationService _scanEmulationService;
        private readonly IDetectorDiagnosticService _diagnosticService;
        private CancellationTokenSource? _scanCancellationTokenSource;

        public FilterViewModel FilterVM { get; }
        public ObservableCollection<EnhancedDetector> Detectors { get; } = new ObservableCollection<EnhancedDetector>();
        public ObservableCollection<EnhancedDetector> FilteredDetectors { get; } = new ObservableCollection<EnhancedDetector>();

        private EnhancedDetector? _selectedDetector;
        public EnhancedDetector? SelectedDetector
        {
            get => _selectedDetector;
            set
            {
                _selectedDetector = value;
                OnPropertyChanged(nameof(SelectedDetector));
                OnPropertyChanged(nameof(IsDetectorSelected));

                if (_selectedDetector != null)
                {
                    HealthStatus = _diagnosticService.GetHealthStatus(_selectedDetector);
                }
                else
                {
                    CurrentDiagnosticResult = null;
                    HealthStatus = null;
                }
            }
        }

        public bool IsDetectorSelected => SelectedDetector != null;

        private ScanProgress? _currentScanProgress;
        public ScanProgress? CurrentScanProgress
        {
            get => _currentScanProgress;
            set
            {
                _currentScanProgress = value;
                OnPropertyChanged(nameof(CurrentScanProgress));
                OnPropertyChanged(nameof(IsScanning));
            }
        }

        public bool IsScanning => CurrentScanProgress != null;

        private DetectorDiagnosticResult? _currentDiagnosticResult;
        public DetectorDiagnosticResult? CurrentDiagnosticResult
        {
            get => _currentDiagnosticResult;
            set
            {
                _currentDiagnosticResult = value;
                OnPropertyChanged(nameof(CurrentDiagnosticResult));
                OnPropertyChanged(nameof(HasDiagnosticResult));
            }
        }

        public bool HasDiagnosticResult => CurrentDiagnosticResult != null;

        private DetectorHealthStatus? _healthStatus;
        public DetectorHealthStatus? HealthStatus
        {
            get => _healthStatus;
            set
            {
                _healthStatus = value;
                OnPropertyChanged(nameof(HealthStatus));
            }
        }

        private bool _isDiagnosticRunning;
        public bool IsDiagnosticRunning
        {
            get => _isDiagnosticRunning;
            set
            {
                _isDiagnosticRunning = value;
                OnPropertyChanged(nameof(IsDiagnosticRunning));
            }
        }

        public ICommand RunDiagnosticCommand { get; }
        public ICommand CalibrateDetectorCommand { get; }
        public ICommand StartScanCommand { get; }
        public ICommand CancelScanCommand { get; }
        public ICommand RefreshDetectorsCommand { get; }

        public MainViewModel(IDetectorService detectorService,
                           IScanEmulationService scanEmulationService,
                           IDetectorDiagnosticService diagnosticService)
        {
            _detectorService = detectorService;
            _scanEmulationService = scanEmulationService;
            _diagnosticService = diagnosticService;

            FilterVM = new FilterViewModel();
            FilterVM.FiltersChanged += OnFiltersChanged;

            RunDiagnosticCommand = new RelayCommand(async () => await RunDiagnosticAsync(),
                () => SelectedDetector != null && !IsDiagnosticRunning);
            CalibrateDetectorCommand = new RelayCommand(async () => await CalibrateDetectorAsync(),
                () => SelectedDetector != null && !IsDiagnosticRunning);
            StartScanCommand = new RelayCommand(async () => await StartScanAsync(),
                () => SelectedDetector != null && !IsScanning);
            CancelScanCommand = new RelayCommand(CancelScan,
                () => IsScanning);
            RefreshDetectorsCommand = new RelayCommand(async () => await LoadDetectorsAsync());

            InitializeData();
        }

        private async void InitializeData()
        {
            await LoadDetectorsAsync();
        }

        public async Task LoadDetectorsAsync()
        {
            try
            {
                var detectors = await _detectorService.GetDetectorsAsync();
                Detectors.Clear();

                var random = new Random();

                foreach (var detector in detectors)
                {
                    Detectors.Add(new EnhancedDetector
                    {
                        Id = detector.Id,
                        Name = detector.Name,
                        Status = detector.Status,
                        Efficiency = detector.Efficiency,
                        Temperature = Math.Round(random.NextDouble() * 30 + 20, 1),
                        Voltage = Math.Round(random.NextDouble() * 0.5 + 4.5, 2),
                        PhotonCount = random.Next(1000, 10000),
                        LastCalibration = DateTime.Now.AddDays(-random.Next(0, 30)),
                        NextMaintenance = DateTime.Now.AddDays(random.Next(1, 90))
                    });
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading detectors: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnFiltersChanged()
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var filtered = Detectors.AsEnumerable();

            if (FilterVM.SelectedStatus != "All")
                filtered = filtered.Where(d => d.Status == FilterVM.SelectedStatus);

            if (!string.IsNullOrEmpty(FilterVM.SearchText))
                filtered = filtered.Where(d => d.Name.Contains(FilterVM.SearchText, StringComparison.OrdinalIgnoreCase));

            filtered = filtered.Where(d => d.LastCalibration >= FilterVM.StartDate &&
                                         d.LastCalibration <= FilterVM.EndDate);

            FilteredDetectors.Clear();
            foreach (var detector in filtered)
            {
                FilteredDetectors.Add(detector);
            }
        }

        public async Task RunDiagnosticAsync()
        {
            if (SelectedDetector == null) return;

            IsDiagnosticRunning = true;
            CurrentDiagnosticResult = null;

            try
            {
                CurrentDiagnosticResult = await _diagnosticService.RunDiagnosticAsync(SelectedDetector.Id);
                HealthStatus = _diagnosticService.GetHealthStatus(SelectedDetector);

                if (CurrentDiagnosticResult.IsSuccessful)
                {
                    SelectedDetector.Status = "Working";
                    SelectedDetector.Efficiency = CurrentDiagnosticResult.Metrics["Efficiency"];
                }
                else
                {
                    SelectedDetector.Status = "Requires Attention";
                }

                await _detectorService.UpdateDetectorStatusAsync(
                    SelectedDetector.Id,
                    SelectedDetector.Status,
                    SelectedDetector.Efficiency);

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Diagnostic error: {ex.Message}", "Diagnostic Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsDiagnosticRunning = false;
            }
        }

        public async Task CalibrateDetectorAsync()
        {
            if (SelectedDetector == null) return;

            IsDiagnosticRunning = true;

            try
            {
                var result = await _diagnosticService.CalibrateDetectorAsync(SelectedDetector.Id);
                if (result.IsSuccessful)
                {
                    SelectedDetector.Efficiency = result.NewEfficiency;
                    SelectedDetector.Status = "Working";
                    SelectedDetector.LastCalibration = DateTime.Now;

                    await _detectorService.UpdateDetectorStatusAsync(
                        SelectedDetector.Id,
                        SelectedDetector.Status,
                        SelectedDetector.Efficiency);

                    MessageBox.Show($"Calibration completed successfully!\nEfficiency improved: {result.OldEfficiency}% → {result.NewEfficiency}%",
                        "Calibration Completed", MessageBoxButton.OK, MessageBoxImage.Information);

                    ApplyFilters();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Calibration error: {ex.Message}", "Calibration Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsDiagnosticRunning = false;
            }
        }

        public async Task StartScanAsync()
        {
            if (SelectedDetector == null)
            {
                MessageBox.Show("Select a detector for scanning", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedDetector.Status != "Working")
            {
                MessageBox.Show("Selected detector is not ready for scanning. Run diagnostic or calibration.",
                    "Detector Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var parameters = new ScanParameters
            {
                EnergyLevel = 120,
                ScanSpeed = 5,
                Resolution = 1.0,
                ScanMode = "High Precision"
            };

            if (!_scanEmulationService.ValidateScanParameters(parameters))
            {
                MessageBox.Show("Invalid scan parameters", "Parameter Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _scanCancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<ScanProgress>(UpdateScanProgress);

            try
            {
                var result = await _scanEmulationService.PerformScanAsync(parameters, progress, _scanCancellationTokenSource.Token);

                if (result.Status == "Completed")
                {
                    MessageBox.Show($"Scan completed successfully!\nObjects detected: {result.DetectedObjects?.Count ?? 0}\nExecution time: {(result.EndTime - result.StartTime).TotalSeconds:0.0} sec.",
                        "Scan Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Scan cancelled", "Information",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Scan cancelled by user", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Scan error: {ex.Message}", "Scan Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CurrentScanProgress = null;
            }
        }

        public void CancelScan()
        {
            _scanCancellationTokenSource?.Cancel();
        }

        private void UpdateScanProgress(ScanProgress progress)
        {
            CurrentScanProgress = progress;
        }

        public async Task UpdateDetectorStatusAsync(int detectorId, string status, double efficiency)
        {
            try
            {
                await _detectorService.UpdateDetectorStatusAsync(detectorId, status, efficiency);

                var detector = Detectors.FirstOrDefault(d => d.Id == detectorId);
                if (detector != null)
                {
                    detector.Status = status;
                    detector.Efficiency = efficiency;
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Status update error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}