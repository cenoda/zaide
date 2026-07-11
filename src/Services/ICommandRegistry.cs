using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>
/// Command registry interface (umbrella decision D5).
/// UI-framework neutral — Avalonia KeyBinding creation is the window layer's concern.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>
    /// Register a command descriptor. Throws InvalidOperationException on duplicate IDs.
    /// </summary>
    void Register(CommandDescriptor descriptor);

    /// <summary>
    /// Returns all registered descriptors.
    /// </summary>
    IReadOnlyList<CommandDescriptor> GetAll();

    /// <summary>
    /// Returns the descriptor for the given ID, or null if not found.
    /// </summary>
    CommandDescriptor? GetById(string id);

    /// <summary>
    /// Execute a parameterless command by ID. Returns false for unknown or unavailable commands.
    /// </summary>
    bool Execute(string commandId);

    /// <summary>
    /// Execute a parameterized command by ID. Returns false for unknown, unavailable,
    /// or type-incompatible commands. Never coerces parameters or infers types.
    /// </summary>
    bool Execute<T>(string commandId, T parameter);

    /// <summary>
    /// Resolve key bindings from defaults and user overrides.
    /// M8 owns canonical resolution; M7a returns an empty result for an empty registry.
    /// </summary>
    IReadOnlyList<ResolvedKeyBinding> ResolveKeyBindings(ISettingsService settings);
}
