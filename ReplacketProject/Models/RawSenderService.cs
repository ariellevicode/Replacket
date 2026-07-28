using System;
using System.Linq;
using SharpPcap;

namespace ReplacketProject.Models
{
    public class RawSenderService : IDisposable
    {
        private ILiveDevice _device; //handler to a NIC

       // finds and opens network card
        public bool InitializeDevice(string selectedDeviceDescription)
        {
            if (string.IsNullOrWhiteSpace(selectedDeviceDescription))
                return false;

            // refresh the device list 
            CaptureDeviceList.Instance.Refresh();

            // match by description or name
            _device = CaptureDeviceList.Instance.FirstOrDefault(device =>
                device.Description == selectedDeviceDescription || device.Name == selectedDeviceDescription);

            if (_device != null)
            {
                // open the device with promiscuous mode disabled and default read timeout
                _device.Open(DeviceModes.None, 1000);
                return true;
            }

            return false;
        }

       
        // injects the exact, unedited raw byte array directly to the network interface card.
        
        public void SendRawPacket(byte[] rawPacketData)
        {
            if (_device != null && rawPacketData != null && rawPacketData.Length > 0)
            {
                _device.SendPacket(rawPacketData);
            }
        }

        
        // closes the hardware device interface.
        
        public void Close()
        {
            if (_device != null)
            {
                _device.Close();
                _device = null;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}