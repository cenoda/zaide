using System;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace Zaide.Services;

/// <summary>
/// Singleton command registry implementation (D5).
/// Receives ILogger&lt;CommandRegistry&gt; through DI for diagnostics.
/// </summary>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, CommandDescriptor> _commands = new();
    private readonly ILogger<CommandRegistry> _logger;

    public CommandRegistry(ILogger<CommandRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Register(CommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (_commands.ContainsKey(descriptor.Id))
            throw new InvalidOperationException(
                $"Command '{descriptor.Id}' is already registered.");

        _commands[descriptor.Id] = descriptor;
    }

    public IReadOnlyList<CommandDescriptor> GetAll()
    {
        return new List<CommandDescriptor>(_commands.Values);
    }

    public CommandDescriptor? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return _commands.TryGetValue(id, out var descriptor) ? descriptor : null;
    }

    public bool Execute(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId) || !_commands.TryGetValue(commandId, out var descriptor))
        {
            _logger.LogDebug("Unknown command ID: '{CommandId}'", commandId);
            return false;
        }

        try
        {
            if (!descriptor.Command.CanExecute(null))
            {
                _logger.LogDebug("Command '{CommandId}' is unavailable (CanExecute returned false)", commandId);
                return false;
            }

            descriptor.Command.Execute(null);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Command '{CommandId}' threw during execution", commandId);
            return false;
        }
    }

    public bool Execute<T>(string commandId, T parameter)
    {
        if (string.IsNullOrWhiteSpace(commandId) || !_commands.TryGetValue(commandId, out var descriptor))
        {
            _logger.LogDebug("Unknown command ID: '{CommandId}'", commandId);
            return false;
        }

        try
        {
            if (!descriptor.Command.CanExecute(parameter))
            {
                _logger.LogDebug(
                    "Command '{CommandId}' cannot execute with parameter of type {ParameterType} (CanExecute returned false)",
                    commandId, typeof(T).Name);
                return false;
            }

            descriptor.Command.Execute(parameter);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Command '{CommandId}' threw during typed execution", commandId);
            return false;
        }
    }

    public IReadOnlyList<ResolvedKeyBinding> ResolveKeyBindings(ISettingsService settings)
    {
        // M7a: no canonical commands registered, no resolution logic.
        // M8 owns gesture parsing, override resolution, and conflict behavior.
        return Array.Empty<ResolvedKeyBinding>();
    }

    /// <summary>
    /// Validate a neutral gesture string format (M8 internal helper, exposed for testing).
    /// Returns true if well-formed; false if malformed (logged at Warning level).
    /// </summary>
    internal bool ValidateGestureFormat(string gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
        {
            _logger.LogWarning("Invalid gesture input: empty or whitespace");
            return false;
        }

        // M8 owns full gesture parsing. M7a provides the validation contract surface
        // so that warning-level diagnostics are testable through the registry.
        return true;
    }

    /// <summary>
    /// Log a gesture conflict at Warning level (M8 internal helper, exposed for testing).
    /// </summary>
    internal void LogGestureConflict(string gesture, string winnerId, string loserId)
    {
        _logger.LogWarning(
            "Gesture conflict for '{Gesture}': '{WinnerId}' wins over '{LoserId}'",
            gesture, winnerId, loserId);
    }
}
