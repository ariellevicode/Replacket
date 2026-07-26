using Microsoft.Win32;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Text;
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


        // commands
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand BrowseFileCommand { get; }
        public ICommand FileDroppedCommand { get; }

        public MainViewModel()
        {
            _displayBuffer = new StringBuilder();

            // bind StartCommand to the async processing method
            StartCommand = new RelayCommand(async () => await ProcessPcapFileAsync());
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

        private async Task ProcessPcapFileAsync()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                PacketDisplayInfo = "Status: No file path provided.";
                return;
            }

            _displayBuffer.Clear();
            PacketDisplayInfo = "Starting capture...\n";

            // process on background thread
            await Task.Run(() =>
            {
                try
                {
                    using var device = new CaptureFileReaderDevice(FilePath);
                    device.Open();

                    int packetCount = 0;

                    while (device.GetNextPacket(out PacketCapture capture) == GetPacketStatus.PacketRead)
                    {
                        packetCount++;
                        var rawPacket = capture.GetPacket();
                        var parsedPacket = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

                        
                        FormatAndDisplayPacket(parsedPacket, packetCount);

                        // pass to network sending function
                        // SendPacket(rawPacket);
                        // WIP

                        Task.Delay(5).Wait();
                    }

                    _displayBuffer.AppendLine("\nFinished processing file.");
                    PacketDisplayInfo = _displayBuffer.ToString();
                }
                catch (Exception ex)
                {
                    _displayBuffer.AppendLine($"\nError: {ex.Message}");
                    PacketDisplayInfo = _displayBuffer.ToString();
                }
            });
        }

        private void FormatAndDisplayPacket(Packet parsedPacket, int packetCount)
        {
            string packetSummary = $"[{packetCount}] {parsedPacket}\n" + new string('-', 50) + "\n";
            _displayBuffer.AppendLine(packetSummary);

            // property update automatically dispatches safely to UI thread via BaseViewModel
            PacketDisplayInfo = _displayBuffer.ToString();
        }
    }
}