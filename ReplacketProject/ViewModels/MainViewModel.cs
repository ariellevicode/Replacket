using Microsoft.Win32;
using PacketDotNet;
using ReplacketProject.Models;
using ReplacketProject.Models.Commands;
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
    public class MainViewModel : BoundPropertiesViewModel
    {
        
        // execution is currently active
        
        public int _lastPacketPosition = 0; // Tracks the last processed packet count across runs
        public CancellationTokenSource _cts;

        // commands
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand BrowseFileCommand { get; }
        public ICommand FileDroppedCommand { get; }
        public ICommand IncrementCommand { get; }
        public ICommand DecrementCommand { get; }
        public ICommand ResetCommand { get; }

        public MainViewModel()
        {
            
            // bind Commands
            StartCommand = new StartCommand(this);
            ResetCommand = new ResetCommand(this);
            StopCommand = new StopCommand(this);
            BrowseFileCommand = new BrowseFileCommand(this);
            FileDroppedCommand = new FileDroppedCommand(this);
            IncrementCommand = new IncrementCommand(this);
            DecrementCommand = new DecrementCommand(this);

            NetworkDeviceModel deviceModel = new NetworkDeviceModel();
            NetworkDevices = new ObservableCollection<string>(deviceModel.GetAvailableNetworkDevices());
        }
        private void ResetPlayback()
        {
            _cts?.Cancel();
            _lastPacketPosition = 0;
            ProgressValue = 0;
            ClearDisplay();
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
                    DelayTime += 250;
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
                    if (DelayTime >= 250) DelayTime -= 250;
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

        public void ClearDisplay()
        {
            PacketDisplayInfo = string.Empty;
        }

        public async Task ProcessPcapFileAsync()
        {
            if (!ValidatePlaybackReady()) return;

            IsProcessing = true;
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            using RawSenderService rawSender = new RawSenderService();
            if (!rawSender.InitializeDevice(SelectedDevice))
            {
                PacketDisplayInfo = "Status: Could not open the selected network adapter.";
                IsProcessing = false;
                return;
            }

            if (_lastPacketPosition == 0)
            {
                PacketDisplayInfo = "calculating total packets...\n";
                ProgressValue = 0;
            }

            await Task.Run(() => ExecutePlaybackSession(rawSender, token));

            IsProcessing = false;
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

                    using CaptureFileReaderDevice device = new CaptureFileReaderDevice(FilePath);
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

                RawCapture rawPacket = capture.GetPacket();
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
                Packet parsedPacket = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
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
            if (parsedPacket == null) return "[Unparseable Packet]";
            // stringbuilder as to dynamically append new information
            StringBuilder sb = new StringBuilder();

            // identify packet type 
            string packetType = "Unknown";
            Packet currentLayer = parsedPacket;
            Packet applicationLayer = parsedPacket;

            while (currentLayer != null)
            {
                // strip the word "Packet" for a cleaner display 
                packetType = currentLayer.GetType().Name.Replace("Packet", "");
                applicationLayer = currentLayer;
                currentLayer = currentLayer.PayloadPacket;
            }

            sb.AppendLine($"Type: {packetType}");

            // extract and format the payload
            byte[] payloadBytes = applicationLayer.PayloadData;

            if (payloadBytes != null && payloadBytes.Length > 0)
            {
                // hex
                sb.AppendLine($"Payload (Hex): {BitConverter.ToString(payloadBytes)}");

                // decoded text for readability
                sb.Append("Payload (Text): ");
                foreach (byte b in payloadBytes)
                {
                    // readable ASCII characters, otherwise print a dot for binary data
                    sb.Append((b >= 32 && b <= 126) ? (char)b : '.');
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("Payload: [No Application Data]");
            }

            return sb.ToString();
        }

        public int GetPcapPacketCount()
        {
            using CaptureFileReaderDevice device = new CaptureFileReaderDevice(FilePath);
            device.Open(); //NOTEEEE

            int count = 0;
            // discards the actual packet data becuase we are just using GetNextPacket to iterate (hence the "out _")
            while (device.GetNextPacket(out _) == GetPacketStatus.PacketRead) 
            {
                count++;
            }

            return count;
        }
    }
}