using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReplacketProject.ViewModels
{
    public class BoundPropertiesViewModel : BaseViewModel
    {
        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
                ProgressValue = 0;
                ProgressMaximum = 100;
                LastPacketPosition = 0;
            }
        }

        private ObservableCollection<string> _networkDevices = new();
        public ObservableCollection<string> NetworkDevices
        {
            get => _networkDevices;
            set { _networkDevices = value; OnPropertyChanged(); }
        }

        private string _selectedDevice;
        public string SelectedDevice
        {
            get => _selectedDevice;
            set { _selectedDevice = value; OnPropertyChanged(); }
        }

        private string _packetDisplayInfo;
        public string PacketDisplayInfo
        {
            get => _packetDisplayInfo;
            set { _packetDisplayInfo = value; OnPropertyChanged(); }
        }

        private int _delayTime = 0;
        public int DelayTime
        {
            get => _delayTime;
            set { _delayTime = value; OnPropertyChanged(); }
        }

        private int _repeatCount = 1;
        public int RepeatCount
        {
            get => _repeatCount;
            set { _repeatCount = value; OnPropertyChanged(); }
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        private double _progressMaximum = 100;
        public double ProgressMaximum
        {
            get => _progressMaximum;
            set
            {
                _progressMaximum = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        public double ProgressPercentage
        {
            get
            {
                if (ProgressMaximum <= 0) return 0;
                return Math.Min(100, Math.Round((ProgressValue / ProgressMaximum) * 100));
            }
        }

        private double _playbackSpeed = 1.0;
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set
            {
                if (value > 0)
                {
                    _playbackSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isFixedDelay = false;
        public bool IsFixedDelay
        {
            get => _isFixedDelay;
            set
            {
                _isFixedDelay = value;
                OnPropertyChanged();
            }
        }

       
        public int LastPacketPosition { get; set; } = 0;

    }
}
