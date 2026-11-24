using System.Windows.Input;

namespace Polhem.Chat.Avalonia;

/// <summary>A command that runs an action and asks a predicate whether it can run.</summary>
internal sealed class ViewCommand(Action<object?> execute, Func<object?, bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute(parameter);

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            execute(parameter);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
