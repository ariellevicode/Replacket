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
    public class StopCommand : RelayCommand
    {
        public StopCommand(MainViewModel mainViewModel)
            : base(
                execute: () => ExecuteStop(mainViewModel),
                canExecute: () => CanStop(mainViewModel)
            )
        {

        }
        private static bool CanStop(MainViewModel mainViewModel)
        {
            return mainViewModel.IsProcessing;
        }
        private static async Task ExecuteStop(MainViewModel mainViewModel)
        {
            mainViewModel._cts?.Cancel();
        }
    }
}
