using SEE_INSADE.Utilities;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SEE_INSADE.ViewModels
{
    public class FilterViewModel : INotifyPropertyChanged
    {
        private string _selectedStatus = "All";
        private DateTime _startDate = DateTime.Now.AddDays(-7);
        private DateTime _endDate = DateTime.Now;
        private string _searchText = "";

        public ObservableCollection<string> StatusOptions { get; } = new ObservableCollection<string>
        {
            "All",
            "Working",
            "Maintenance",
            "Faulty",
            "Offline"
        };

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                _selectedStatus = value;
                OnPropertyChanged(nameof(SelectedStatus));
                FiltersChanged?.Invoke();
            }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                _startDate = value;
                OnPropertyChanged(nameof(StartDate));
                FiltersChanged?.Invoke();
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                _endDate = value;
                OnPropertyChanged(nameof(EndDate));
                FiltersChanged?.Invoke();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                FiltersChanged?.Invoke();
            }
        }

        public Action? FiltersChanged { get; set; }

        public RelayCommand ResetFiltersCommand { get; }

        public FilterViewModel()
        {
            ResetFiltersCommand = new RelayCommand(ResetFilters);
        }

        public void ResetFilters()
        {
            SelectedStatus = "All";
            StartDate = DateTime.Now.AddDays(-7);
            EndDate = DateTime.Now;
            SearchText = "";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}