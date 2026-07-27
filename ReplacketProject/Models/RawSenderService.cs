using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpPcap;
using System;
using System.Linq;

namespace ReplacketProject.Models
{
    

    
        public class RawSenderService : IDisposable
        {
            private ILiveDevice _device;

            /// <summary>
            /// Finds and opens the network adapter matching the selected description.
            /// </summary>
            public bool InitializeDevice(string selectedDeviceDescription)
            {
                if (string.IsNullOrWhiteSpace(selectedDeviceDescription))
                    return false;

                // Find the SharpPcap device matching your dropdown selection
                _device = CaptureDeviceList.Instance.FirstOrDefault(d =>
                    d.Description == selectedDeviceDescription || d.Name == selectedDeviceDescription);

                if (_device != null)
                {
                    // Open the device for raw packet transmission
                    _device.Open(DeviceModes.None);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Injects the exact, unedited raw byte array directly to the network interface card.
            /// </summary>
            public void SendRawPacket(byte[] rawPacketData)
            {
                if (_device != null && rawPacketData != null && rawPacketData.Length > 0)
                {
                    _device.SendPacket(rawPacketData);
                }
            }

            /// <summary>
            /// Closes the hardware device interface.
            /// </summary>
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
