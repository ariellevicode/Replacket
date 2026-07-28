using ReplacketProject.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReplacketProject.Models.Commands
{
    public class IncrementCommand : RelayCommand
    {
        public IncrementCommand(BoundPropertiesViewModel bpvm)
            : base(param => ExecuteIncrement(bpvm, param))
        {
        }

        private static void ExecuteIncrement(BoundPropertiesViewModel bpvm, object parameter)
        {
            if (parameter is not string propName) return;

            if (propName == "Repeat")
            {
                bpvm.RepeatCount++;
            }
            else if (propName == "Delay")
            {
                bpvm.DelayTime += 250;
            }
            else if (propName == "Speed")
            {
                bpvm.PlaybackSpeed = Math.Round(bpvm.PlaybackSpeed + 0.25, 2);
            }
        }
    }
}
