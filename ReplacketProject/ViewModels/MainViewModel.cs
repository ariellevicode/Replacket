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

            // temp DI violation
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
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                PacketDisplayInfo = "Status: No file path provided.";
                return;
            }

            if (!File.Exists(FilePath))
            {
                PacketDisplayInfo = "Status: File path does not exist.";
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedDevice))
            {
                PacketDisplayInfo = "Status: Please select a network adapter first.";
                return;
            }

            _isProcessing = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // initialize raw sender service
            using var rawSender = new ReplacketProject.Models.RawSenderService();
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

            await Task.Run(() =>
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

                        // skip already processed packets
                        int skippedCount = 0;
                        while (skippedCount < _lastPacketPosition && device.GetNextPacket(out _) == GetPacketStatus.PacketRead)
                        {
                            skippedCount++;
                        }

                        Stopwatch uiTimer = Stopwatch.StartNew();
                        string latestPacketHex = string.Empty;

                        while (device.GetNextPacket(out PacketCapture capture) == GetPacketStatus.PacketRead)
                        {
                            if (token.IsCancellationRequested)
                            {
                                PacketDisplayInfo = $"Processing stopped at packet {_lastPacketPosition}.\n\n{latestPacketHex}";
                                break;
                            }

                            if (DelayTime > 0)
                            {
                                Task.Delay(DelayTime).Wait();
                            }

                            _lastPacketPosition++;

                            var rawPacket = capture.GetPacket();

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

                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

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

                _isProcessing = false;
            });
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
        // placeholder method for getting the amount of packets in a pcap file.
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