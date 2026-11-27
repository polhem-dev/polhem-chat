using System.Collections.Specialized;

namespace Polhem.Chat;

/// <summary>The host's commands the user reaches by typing a slash at the start of the prompt box.</summary>
public sealed partial class ChatComposer
{
    private const char CommandTrigger = '/';

    private IEnumerable<PromptCommand>? _commands;
    private IReadOnlyList<PromptCommand> _commandMatches = [];
    private PromptCommand? _selectedCommand;
    private bool _commandListClosed;

    /// <summary>
    /// Gets or sets the commands offered in the prompt box: typing <c>/</c> at the start of the box lists those whose name
    /// matches what follows. The list is read again as the user types, and at once when the collection raises
    /// <see cref="INotifyCollectionChanged.CollectionChanged"/> while the composer is connected.
    /// </summary>
    public IEnumerable<PromptCommand>? Commands
    {
        get => _commands;
        set
        {
            if (ReferenceEquals(value, _commands))
            {
                return;
            }
            UnwatchCommands();
            _commands = value;
            WatchCommands();
            OnPropertyChanged();
            UpdateCommands();
        }
    }

    /// <summary>
    /// Gets the commands whose name matches what the user typed after the slash: those that start with it first, then
    /// those that contain it, ignoring case. It is empty while the list is closed.
    /// </summary>
    public IReadOnlyList<PromptCommand> CommandMatches => _commandMatches;

    /// <summary>
    /// Gets a value indicating whether the list of commands is open: the box starts with a slash followed by no white
    /// space, at least one command matches, and the user has not closed the list since typing the slash.
    /// </summary>
    public bool IsCommandListOpen => _commandMatches.Count > 0;

    /// <summary>Gets the command that Enter chooses while the list is open, or <see langword="null"/> while it is closed.</summary>
    public PromptCommand? SelectedCommand => _selectedCommand;

    /// <summary>
    /// Moves the selection in the open list of commands, wrapping around at either end, as the arrow keys do. Nothing
    /// happens while the list is closed.
    /// </summary>
    /// <param name="offset">How many commands to move by: negative moves up, positive down.</param>
    public void MoveCommandSelection(int offset)
    {
        if (_commandMatches.Count == 0 || _selectedCommand is null)
        {
            return;
        }
        var count = _commandMatches.Count;
        var index = IndexOfCommand(_selectedCommand);
        SetSelectedCommand(_commandMatches[(((index + offset) % count) + count) % count]);
    }

    /// <summary>
    /// Closes the list of commands, as Esc does, and keeps it closed until the box no longer starts with a slash.
    /// </summary>
    public void CloseCommandList()
    {
        if (_commandListClosed)
        {
            return;
        }
        _commandListClosed = true;
        UpdateCommands();
    }

    /// <summary>
    /// Chooses a command: its text replaces the text in the box and the list closes. A command that
    /// <see cref="PromptCommand.SendsImmediately"/> is then submitted from the box, with the files waiting there, as if the
    /// user had pressed Enter; while a reply is in progress it stays in the box instead, for the user to send later.
    /// </summary>
    /// <param name="command">The command, or <see langword="null"/> for <see cref="SelectedCommand"/>.</param>
    /// <returns>A task that completes as <see cref="SubmitAsync"/> does when the command is sent, and at once otherwise.</returns>
    public Task ChooseCommandAsync(PromptCommand? command = null)
    {
        command ??= _selectedCommand;
        if (command is null)
        {
            return Task.CompletedTask;
        }
        _commandListClosed = true;
        Prompt = command.Text;
        return command.SendsImmediately && !IsGenerating ? SubmitAsync() : Task.CompletedTask;
    }

    /// <summary>
    /// Describes the selected command for a screen reader, with <see cref="ChatStrings.FormatCommand"/>: its name,
    /// description and place in the list. A view announces it as the selection moves.
    /// </summary>
    /// <returns>The text, or <see langword="null"/> while the list is closed.</returns>
    public string? DescribeSelectedCommand() => _selectedCommand is { } command
        ? Strings.FormatCommand(command, IndexOfCommand(command) + 1, _commandMatches.Count)
        : null;

    private void UpdateCommands()
    {
        var prompt = _prompt ?? string.Empty;
        var typing = prompt.Length > 0 && prompt[0] == CommandTrigger;
        if (!typing)
        {
            _commandListClosed = false;
        }
        IReadOnlyList<PromptCommand> matches = typing && !_commandListClosed && _commands is not null
            ? Match(prompt.AsSpan(1), _commands)
            : [];
        var wasOpen = IsCommandListOpen;
        if (!matches.SequenceEqual(_commandMatches))
        {
            _commandMatches = matches;
            OnPropertyChanged(nameof(CommandMatches));
            if (wasOpen != IsCommandListOpen)
            {
                OnPropertyChanged(nameof(IsCommandListOpen));
            }
        }
        // The selection stays on the same command while it still matches, so a list that grows or shrinks under the
        // user's typing does not move what Enter chooses.
        SetSelectedCommand(_selectedCommand is not null && IndexOfCommand(_selectedCommand) >= 0
            ? _selectedCommand
            : matches.FirstOrDefault());
    }

    private static PromptCommand[] Match(ReadOnlySpan<char> query, IEnumerable<PromptCommand> commands)
    {
        foreach (var character in query)
        {
            if (char.IsWhiteSpace(character))
            {
                return [];
            }
        }
        var typed = query.ToString();
        var starting = new List<PromptCommand>();
        var containing = new List<PromptCommand>();
        foreach (var command in commands)
        {
            if (command is null)
            {
                continue;
            }
            if (command.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            {
                starting.Add(command);
            }
            else if (command.Name.Contains(typed, StringComparison.OrdinalIgnoreCase))
            {
                containing.Add(command);
            }
        }
        return [.. starting, .. containing];
    }

    private int IndexOfCommand(PromptCommand command)
    {
        for (var index = 0; index < _commandMatches.Count; index++)
        {
            if (ReferenceEquals(_commandMatches[index], command))
            {
                return index;
            }
        }
        return -1;
    }

    private void SetSelectedCommand(PromptCommand? command)
    {
        if (!ReferenceEquals(command, _selectedCommand))
        {
            _selectedCommand = command;
            OnPropertyChanged(nameof(SelectedCommand));
        }
    }

    // NOTE: Like the runner and the conversation, the host's collection usually outlives the view, so the composer listens
    // to it only while it is connected.
    private void WatchCommands()
    {
        if (_isConnected && _commands is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += OnCommandsChanged;
        }
    }

    private void UnwatchCommands()
    {
        if (_commands is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged -= OnCommandsChanged;
        }
    }

    private void OnCommandsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateCommands();
}
