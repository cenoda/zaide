using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Zaide.App.Composition;
/// <summary>
/// Immutable command metadata. Sealed class per umbrella decision D5.
/// </summary>
public sealed class CommandDescriptor
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public IReadOnlyList<string> DefaultGestures { get; }
    public ICommand Command { get; }

    public CommandDescriptor(
        string id,
        string displayName,
        string category,
        IReadOnlyList<string> defaultGestures,
        ICommand command)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Command ID must not be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Command category must not be empty.", nameof(category));

        Id = id;
        DisplayName = displayName;
        Category = category;
        DefaultGestures = defaultGestures is not null
            ? new List<string>(defaultGestures).AsReadOnly()
            : throw new ArgumentNullException(nameof(defaultGestures));
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }
}
