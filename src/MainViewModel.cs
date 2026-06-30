using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RamTreeMap
{
    /// <summary>
    /// ViewModel for the main window, managing loading state and process data.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private bool _isLoading;
        private ObservableCollection<ProcessMemoryInfo> _processes;

        private bool _hideSmallPrograms;
        private bool _showSystemMemory;

        private readonly ProcessMemoryService _memoryService;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<ProcessMemoryInfo> Processes
        {
            get => _processes;
            set
            {
                if (_processes != value)
                {
                    _processes = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HideSmallPrograms
        {
            get => _hideSmallPrograms;
            set
            {
                if (_hideSmallPrograms != value)
                {
                    _hideSmallPrograms = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowSystemMemory
        {
            get => _showSystemMemory;
            set
            {
                if (_showSystemMemory != value)
                {
                    _showSystemMemory = value;
                    OnPropertyChanged();
                }
            }
        }

        public MainViewModel()
        {
            _memoryService = new ProcessMemoryService();
            _processes = new ObservableCollection<ProcessMemoryInfo>();
            _isLoading = false;
            _hideSmallPrograms = false;
            _showSystemMemory = false;
        }

        /// <summary>
        /// Loads process memory data asynchronously as a dictionary with PID as key.
        /// </summary>
        public async Task LoadProcessDataAsync()
        {
            IsLoading = true;
            try
            {
                var processDictionary = await _memoryService.GetRunningProcessesAsync();
                Processes.Clear();

                var sortedProcesses = processDictionary.Values
                    .OrderByDescending(p => p.RamUsage)
                    .ToList();

                foreach (var process in sortedProcesses)
                {
                    Processes.Add(process);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
