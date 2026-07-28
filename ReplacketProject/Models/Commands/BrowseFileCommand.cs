using Microsoft.Win32;
using ReplacketProject.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using ReplacketProject.ViewModels;

namespace ReplacketProject.Models.Commands
{
    internal class BrowseFileCommand : RelayCommand
    {
        private readonly BoundPropertiesViewModel _bpvm;

        public BrowseFileCommand(BoundPropertiesViewModel bpvm)
            : base(() => ExecuteBrowseFile(bpvm))
        {
            _bpvm = bpvm;
        }

        private static void ExecuteBrowseFile(BoundPropertiesViewModel bpvm)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Pcap Files (*.pcap;*.pcapng)|*.pcap;*.pcapng|All Files (*.*)|*.*",
                Title = "Select a PCAP File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                bpvm.FilePath = openFileDialog.FileName;
            }
        }
    }
}
