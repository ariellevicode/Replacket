using Microsoft.Win32;
using PacketDotNet;
using ReplacketProject.Models;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ReplacketProject.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        // bound properties
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
                _lastPacketPosition = 0;
            }
        }

        private ObservableCollection<string> _networkDevices;
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

        private readonly StringBuilder _displayBuffer;

        // execution is currently active
        private bool _isProcessing;
        private int _lastPacketPosition = 0; // Tracks the last processed packet count across runs
        private CancellationTokenSource _cts;

        // commands
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand BrowseFileCommand { get; }
        public ICommand FileDroppedCommand { get; }
        public ICommand IncrementCommand { get; }
        public ICommand DecrementCommand { get; }

        public MainViewModel()
        {
            _displayBuffer = new StringBuilder();

            // bind Commands
            StartCommand = new RelayCommand(async () => await ProcessPcapFileAsync(), () => !_isProcessing);
            StopCommand = new RelayCommand(StopProcessing, () => _isProcessing);
            BrowseFileCommand = new RelayCommand(BrowseFile);
            FileDroppedCommand = new RelayCommand(OnFileDropped);
            IncrementCommand = new RelayCommand(IncrementValue);
            DecrementCommand = new RelayCommand(DecrementValue);

            var deviceModel = new NetworkDeviceModel();
            NetworkDevices = new ObservableCollection<string>(deviceModel.GetAvailableNetworkDevices());
        }

        private void IncrementValue(object parameter)
        {
            if (parameter is string propName)
            {
                if (propName == "Repeat")
                {
                    RepeatCount++;
                }
                else if (propName == "Delay")
                {
                    DelayTime++;
                }
                else if (propName == "Speed")
                {
                    PlaybackSpeed = Math.Round(PlaybackSpeed + 0.25, 2);
                }
            }
        }

        private void DecrementValue(object parameter)
        {
            if (parameter is string propName)
            {
                if (propName == "Repeat")
                {
                    if (RepeatCount > 1) RepeatCount--;
                }
                else if (propName == "Delay")
                {
                    if (DelayTime > 0) DelayTime--;
                }
                else if (propName == "Speed")
                {
                    if (PlaybackSpeed > 0.25) PlaybackSpeed = Math.Round(PlaybackSpeed - 0.25, 2);
                }
            }
        }

        private void BrowseFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Pcap Files (*.pcap;*.pcapng)|*.pcap;*.pcapng|All Files (*.*)|*.*",
                Title = "Select a PCAP File"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                FilePath = openFileDialog.FileName;
            }
        }

        private void OnFileDropped(object parameter)
        {
            if (parameter is DragEventArgs e && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    FilePath = files[0];
                }
            }
        }

        private void ClearDisplay()
        {
            _displayBuffer.Clear();
            PacketDisplayInfo = string.Empty;
        }

        private async Task ProcessPcapFileAsync()
        {
            if (!ValidatePlaybackReady()) return;

            _isProcessing = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            using var rawSender = new RawSenderService();
            if (!rawSender.InitializeDevice(SelectedDevice))
            {
                PacketDisplayInfo = "Status: Could not open the selected network adapter.";
                _isProcessing = false;
                return;
            }

            if (_lastPacketPosition == 0)
            {
                PacketDisplayInfo = "calculating total packets...\n";
                ProgressValue = 0;
            }

            await Task.Run(() => ExecutePlaybackSession(rawSender, token));

            _isProcessing = false;
        }

        private bool ValidatePlaybackReady()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                PacketDisplayInfo = "Status: No file path provided.";
                return false;
            }

            if (!File.Exists(FilePath))
            {
                PacketDisplayInfo = "Status: File path does not exist.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(SelectedDevice))
            {
                PacketDisplayInfo = "Status: Please select a network adapter first.";
                return false;
            }

            return true;
        }

        private void ExecutePlaybackSession(RawSenderService rawSender, CancellationToken token)
        {
            for (int i = 0; i < RepeatCount; i++)
            {
                try
                {
                    if (_lastPacketPosition == 0)
                    {
                        int totalPackets = GetPcapPacketCount();
                        if (totalPackets == 0)
                        {
                            PacketDisplayInfo = "Status: File contains no packets.";
                            return;
                        }
                        ProgressMaximum = totalPackets;
                    }

                    using var device = new CaptureFileReaderDevice(FilePath);
                    device.Open();

                    // skip already processed packets if resumed
                    int skippedCount = 0;
                    while (skippedCount < _lastPacketPosition && device.GetNextPacket(out _) == GetPacketStatus.PacketRead)
                    {
                        skippedCount++;
                    }

                    PlaySinglePcapPass(device, rawSender, token);

                    if (token.IsCancellationRequested) break;

                    PacketDisplayInfo = $"Finished processing file (Repeat {i + 1} of {RepeatCount}).\nTotal Packets: {_lastPacketPosition}";
                    _lastPacketPosition = 0; // reset position for next repeat run
                    ProgressValue = _lastPacketPosition;
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    PacketDisplayInfo = $"Error: {ex.Message}";
                    break;
                }
            }
        }

        private void PlaySinglePcapPass(CaptureFileReaderDevice device, RawSenderService rawSender, CancellationToken token)
        {
            DateTime? lastOriginalTimestamp = null;
            Stopwatch uiTimer = Stopwatch.StartNew();
            string latestPacketHex = string.Empty;

            while (device.GetNextPacket(out PacketCapture capture) == GetPacketStatus.PacketRead)
            {
                if (token.IsCancellationRequested)
                {
                    PacketDisplayInfo = $"Processing stopped at packet {_lastPacketPosition}.\n\n{latestPacketHex}";
                    break;
                }

                var rawPacket = capture.GetPacket();
                DateTime currentOriginalTimestamp = rawPacket.Timeval.Date;

                // calculate timing gap if this is NOT the first packet in the stream
                if (lastOriginalTimestamp.HasValue)
                {
                    TimeSpan originalGap = currentOriginalTimestamp - lastOriginalTimestamp.Value;

                    double finalWaitMilliseconds = IsFixedDelay
                        ? DelayTime
                        : (originalGap.TotalMilliseconds / PlaybackSpeed) + DelayTime;

                    if (finalWaitMilliseconds > 0)
                    {
                        HighPrecisionDelay(TimeSpan.FromMilliseconds(finalWaitMilliseconds));
                    }
                }

                lastOriginalTimestamp = currentOriginalTimestamp;
                _lastPacketPosition++;

                // inject raw packet to NIC via SharpPcap
                rawSender.SendRawPacket(rawPacket.Data);

                // parse packet solely for UI formatting
                var parsedPacket = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
                latestPacketHex = GetFormattedPacketHex(parsedPacket);

                if (uiTimer.ElapsedMilliseconds >= 100)
                {
                    ProgressValue = _lastPacketPosition;
                    PacketDisplayInfo = $"Packet {_lastPacketPosition}:\n{latestPacketHex}";
                    uiTimer.Restart();
                }
            }
        }

        private void HighPrecisionDelay(TimeSpan delay)
        {
            if (delay <= TimeSpan.Zero) return;

            Stopwatch sw = Stopwatch.StartNew();
            double targetTicks = delay.Ticks;

            if (delay.TotalMilliseconds > 15)
            {
                Thread.Sleep((int)(delay.TotalMilliseconds - 10));
            }

            while (sw.ElapsedTicks < targetTicks)
            {
                Thread.SpinWait(10);
            }
        }

        private void StopProcessing()
        {
            _cts?.Cancel();
        }

        private string GetFormattedPacketHex(Packet parsedPacket)
        {
            byte[] payloadBytes = parsedPacket.PayloadData ?? parsedPacket.Bytes;

            if (payloadBytes != null && payloadBytes.Length > 0)
            {
                return BitConverter.ToString(payloadBytes);
            }

            return "[No Payload Data]";
        }

        public int GetPcapPacketCount()
        {
            using var device = new CaptureFileReaderDevice(FilePath);
            device.Open();

            int count = 0;
            while (device.GetNextPacket(out _) == GetPacketStatus.PacketRead)
            {
                count++;
            }

            return count;
        }
    }
}