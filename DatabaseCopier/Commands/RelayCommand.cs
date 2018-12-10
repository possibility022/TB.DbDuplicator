using System;
using System.Windows.Input;

namespace DatabaseCopier.Commands
{
    public class RelayCommand<T> : ICommand
    {

        //ToDo Investigate, what is this? How it works!?
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        private Func<T> _action;

        private Func<bool> _canExecute;

        private bool _lastStatus = false;

        public RelayCommand(Func<T> action, Func<bool> canExecute)
        {
            _action = action;
            _canExecute = canExecute;
        }

        public RelayCommand(Func<T> action)
            : this(action, null)
        {

        }

        public bool CanExecute(object parameter)
        {
            if (_canExecute == null)
            {
                return true;
            }

            bool result = _canExecute.Invoke();
            return result;
        }

        public void Execute(object parameter)
        {
            _action.Invoke();
        }
    }
}
