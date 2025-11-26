using System;
using System.ComponentModel;

namespace SEE_INSADE.Models
{
    public class EnhancedDetector : INotifyPropertyChanged
    {
        public int Id { get; set; }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        private string _status = "Offline";
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        private double _efficiency;
        public double Efficiency
        {
            get => _efficiency;
            set
            {
                _efficiency = value;
                OnPropertyChanged(nameof(Efficiency));
                OnPropertyChanged(nameof(EfficiencyColor));
            }
        }

        public double Temperature { get; set; }
        public double Voltage { get; set; }
        public int PhotonCount { get; set; }
        public DateTime LastCalibration { get; set; }
        public DateTime NextMaintenance { get; set; }

        public string StatusColor => Status switch
        {
            "Working" => "#4CAF50",
            "Maintenance" => "#FF9800",
            "Faulty" => "#F44336",
            "Offline" => "#9E9E9E",
            _ => "#9E9E9E"
        };

        public string EfficiencyColor => Efficiency switch
        {
            >= 90 => "#4CAF50",
            >= 70 => "#FF9800",
            _ => "#F44336"
        };

        public string HealthStatus
        {
            get
            {
                if (Efficiency >= 90 && Temperature < 40 && Voltage >= 4.8)
                    return "Excellent";
                if (Efficiency >= 70 && Temperature < 50 && Voltage >= 4.5)
                    return "Good";
                if (Efficiency >= 50 && Temperature < 60 && Voltage >= 4.2)
                    return "Satisfactory";
                return "Requires Attention";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}