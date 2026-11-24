using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Polhem.Chat;

/// <summary>
/// One item of a conversation as a user interface shows it: a prompt, a reply, a reasoning block, a tool call, a turn
/// summary or an error. Entries raise <see cref="PropertyChanged"/> while a reply streams in, so a view bound to them
/// updates in place.
/// </summary>
/// <remarks>
/// Applications can derive their own entry types, add them to <see cref="ChatConversation.Entries"/>, and give them a
/// template of their own in the front end.
/// </remarks>
public abstract class ChatEntry : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged"/> for the named property.</summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Sets a backing field and raises <see cref="PropertyChanged"/> when the value changes.</summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns><see langword="true"/> when the value changed.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
