using ReplacketProject.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReplacketProject.Models.Commands
{
    public class ResetCommand : RelayCommand
    {
        public ResetCommand(MainViewModel mainViewModel)
            : base(
                execute: () => ExecuteReset(mainViewModel),
                canExecute: () => CanReset(mainViewModel)
            )
        {

        }
        private static bool CanReset(MainViewModel mainViewModel)
        {
            return true;
        }
        private static async Task ExecuteReset(MainViewModel mainViewModel)
        {
            mainViewModel._cts?.Cancel();
            mainViewModel._lastPacketPosition = 0;
            mainViewModel.ProgressValue = 0;
            mainViewModel.ClearDisplay();
        }
    }
    
}

