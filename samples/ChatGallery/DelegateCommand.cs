using System.Windows.Input;

namespace ChatGallery;

/// <summary>A command that calls a delegate with its parameter.</summary>
public sealed class DelegateCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute(parameter);
}
