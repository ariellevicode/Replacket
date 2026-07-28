using SharpPcap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReplacketProject.Models
{
    public class NetworkDeviceModel
    {
        public List<string> GetAvailableNetworkDevices()
        {
            List<string> displayList = new List<string>();


            CaptureDeviceList devices = CaptureDeviceList.Instance;

            if (devices.Count < 1)
            {
                Debug.WriteLine("No capture devices were found on this machine.");
                return displayList;
            }

            
            foreach (ICaptureDevice device in devices)
            {

                displayList.Add(device.Description);
            }

            return displayList;
        }
    }
}
