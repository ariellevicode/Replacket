using Microsoft.Win32;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
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
            set { _filePath = value; OnPropertyChanged(); }
        }

        private string _packetDisplayInfo;
        public string PacketDisplayInfo
        {
            get => _packetDisplayInfo;
            set { _packetDisplayInfo = value; OnPropertyChanged(); }
        }

        private readonly StringBuilder _displayBuffer;

        // execution is currently active
        private bool _isProcessing;
        private CancellationTokenSource _cts;

        // commands
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand BrowseFileCommand { get; }
        public ICommand FileDroppedCommand { get; }

        public MainViewModel()
        {
            _displayBuffer = new StringBuilder();

            // bind Commands
            StartCommand = new RelayCommand(async () => await ProcessPcapFileAsync(), () => !_isProcessing);
            StopCommand = new RelayCommand(StopProcessing, () => _isProcessing);
            BrowseFileCommand = new RelayCommand(BrowseFile);
            FileDroppedCommand = new RelayCommand(OnFileDropped);
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

            _isProcessing = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _displayBuffer.Clear();
            PacketDisplayInfo = "Starting capture...\n";

            await Task.Run(() =>
            {
                try
                {
                    using var device = new CaptureFileReaderDevice(FilePath);
                    device.Open();

                    int packetCount = 0;

                    while (device.GetNextPacket(out PacketCapture capture) == GetPacketStatus.PacketRead)
                    {
                        ClearDisplay();
                        
                        // check if the user pressed the stop button
                        if (token.IsCancellationRequested)
                        {
                            _displayBuffer.AppendLine("\nProcessing stopped by user.");
                            break;
                        }

                        packetCount++;
                        var rawPacket = capture.GetPacket();
                        var parsedPacket = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

                        FormatAndDisplayPacket(parsedPacket, packetCount);

                        // wip pass to network sending function
                        // SendPacket(rawPacket); 

                        Task.Delay(1).Wait();
                    }

                    if (!token.IsCancellationRequested)
                    {
                        _displayBuffer.AppendLine("\nFinished processing file.");
                    }

                    PacketDisplayInfo = _displayBuffer.ToString();
                }
                catch (Exception ex)
                {
                    _displayBuffer.AppendLine($"\nError: {ex.Message}");
                    PacketDisplayInfo = _displayBuffer.ToString();
                }
                finally
                {
                    _isProcessing = false;
                }
            });
        }

        private void StopProcessing()
        {
            // signal the cancellation token to stop the processing loop
            _cts?.Cancel();
        }

        private void FormatAndDisplayPacket(Packet parsedPacket, int packetCount)
        {
            byte[] payloadBytes = parsedPacket.PayloadData ?? parsedPacket.Bytes;
            if (payloadBytes != null && payloadBytes.Length > 0)
            {
                string rawHexPayload = BitConverter.ToString(payloadBytes);

                // append the hex string directly to the display buffer
                _displayBuffer.AppendLine(rawHexPayload);

                // update the bound property for the UI
                PacketDisplayInfo = _displayBuffer.ToString();
            }
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