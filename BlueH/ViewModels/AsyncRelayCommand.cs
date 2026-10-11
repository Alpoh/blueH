using System.Windows.Input;

namespace BlueH.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;

    private EventHandler? _canExecuteChanged;
    private bool _isRunning;

    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add
        {
            CommandManager.RequerySuggested += value;
            _canExecuteChanged += value;
        }
        remove
        {
            CommandManager.RequerySuggested -= value;
            _canExecuteChanged -= value;
        }
    }

    public bool CanExecute(object? parameter)
    {
        return !_isRunning && (_canExecute?.Invoke(parameter) ?? true);
    }

    public void Execute(object? parameter)
    {
        _ = ExecuteAsync(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        _canExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ExecuteAsync(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        _canExecuteChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isRunning = false;
            _canExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
