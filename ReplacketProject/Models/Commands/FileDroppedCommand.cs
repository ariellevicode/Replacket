using ReplacketProject.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ReplacketProject.Models.Commands
{
    public class FileDroppedCommand : RelayCommand
    {
        public FileDroppedCommand(BoundPropertiesViewModel bpvm)
            : base(param => ExecuteFileDropped(bpvm, param))
        {

        }
        public static void ExecuteFileDropped(BoundPropertiesViewModel bpvm,object param)
        {
            if (param is DragEventArgs e && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    bpvm.FilePath = files[0];
                }
            }
        }

    }
}