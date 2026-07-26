using ReplacketProject.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ReplacketProject.Controls
{
    /// <summary>
    /// Interaction logic for FileUploadControl.xaml
    /// </summary>
    public partial class FileUploadControl : UserControl
    {
        public FileUploadControl()
        {
            InitializeComponent();
        }
        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            // Forward the drag event args to the ViewModel's command if DataContext is set
            if (DataContext is MainViewModel vm)
            {
                vm.FileDroppedCommand.Execute(e);
            }
        }
    }
}
