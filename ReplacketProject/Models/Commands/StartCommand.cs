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
    public class StartCommand : RelayCommand
    {
        public StartCommand(MainViewModel mainViewModel)
            : base(
                execute: () => ExecuteStart(mainViewModel),
                canExecute: () => CanStart(mainViewModel)
            )
        {

        }
        private static bool CanStart(MainViewModel mainViewModel)
        {
            return !mainViewModel.IsProcessing;
        }
        private static async Task ExecuteStart(MainViewModel mainViewModel)
        {
            await mainViewModel.ProcessPcapFileAsync();
        }
    }
}
