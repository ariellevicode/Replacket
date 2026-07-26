using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Windows.Input;

namespace ReplacketProject.ViewModels
{
    public class RelayCommand : ICommand
    {

        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            // check if execution method was provided
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        // constructor for commands without parameters 
        public RelayCommand(Action execute, Func<bool> canExecute = null)
            : this(_ => execute(), canExecute == null ? null : _ => canExecute())
        {
        }


        public event EventHandler CanExecuteChanged
        {
            // CommandManager is WPFs input watcher. It detects user interactions .
            // by hooking into CommandManager.RequerySuggested, we tell WPF to check 
            // the CanExecute method below every time the user interacts with the app.
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        // check if the command is allowed to run
        public bool CanExecute(object parameter)
        {
            // if no validation method was provided, set to true 
            return _canExecute == null || _canExecute(parameter);
        }

        // runs the action passed into the constructor
        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }
}