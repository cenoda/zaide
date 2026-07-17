using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia.Input;
using Microsoft.Extensions.Logging;
using Zaide.Features.Settings.Contracts;

namespace Zaide.App.Composition;
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
        ArgumentNullException.ThrowIfNull(settings);

        var overrides = settings.Current.Keybindings;
        var resolved = new Dictionary<string, (string CommandId, string Gesture)>(StringComparer.Ordinal);
        var commandsWithOverrides = new HashSet<string>();

        foreach (var (commandId, gesture) in overrides.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(commandId))
                continue;

            if (!_commands.ContainsKey(commandId))
            {
                _logger.LogWarning("Override for unregistered command ID: '{CommandId}'", commandId);
                continue;
            }

            if (gesture is null)
            {
                _logger.LogWarning("Invalid override gesture for '{CommandId}': null", commandId);
                continue;
            }

            if (gesture.Length == 0)
            {
                commandsWithOverrides.Add(commandId);
                continue;
            }

            if (!TryParseGesture(gesture, out var normalizedGesture))
            {
                _logger.LogWarning("Invalid override gesture for '{CommandId}': '{Gesture}'", commandId, gesture);
                continue;
            }

            commandsWithOverrides.Add(commandId);

            if (resolved.TryGetValue(normalizedGesture, out var existing))
            {
                if (string.Compare(commandId, existing.CommandId, StringComparison.Ordinal) < 0)
                {
                    LogGestureConflict(normalizedGesture, commandId, existing.CommandId);
                    resolved[normalizedGesture] = (commandId, normalizedGesture);
                }
                else
                {
                    LogGestureConflict(normalizedGesture, existing.CommandId, commandId);
                }
            }
            else
            {
                resolved[normalizedGesture] = (commandId, normalizedGesture);
            }
        }

        foreach (var (commandId, descriptor) in _commands.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (commandsWithOverrides.Contains(commandId))
                continue;

            foreach (var defaultGesture in descriptor.DefaultGestures)
            {
                if (string.IsNullOrWhiteSpace(defaultGesture))
                {
                    _logger.LogWarning("Invalid default gesture for '{CommandId}': '{Gesture}'", commandId, defaultGesture);
                    continue;
                }

                if (!TryParseGesture(defaultGesture, out var normalizedGesture))
                {
                    _logger.LogWarning("Invalid default gesture for '{CommandId}': '{Gesture}'", commandId, defaultGesture);
                    continue;
                }

                if (resolved.TryGetValue(normalizedGesture, out var existing))
                {
                    LogGestureConflict(normalizedGesture, existing.CommandId, commandId);
                    continue;
                }

                resolved[normalizedGesture] = (commandId, normalizedGesture);
            }
        }

        return resolved.Values
            .Select(v => new ResolvedKeyBinding(v.Gesture, v.CommandId))
            .ToList();
    }

    internal bool ValidateGestureFormat(string gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
        {
            _logger.LogWarning("Invalid gesture input: empty or whitespace");
            return false;
        }

        if (!TryParseGesture(gesture, out _))
        {
            _logger.LogWarning("Invalid gesture format: '{Gesture}'", gesture);
            return false;
        }

        return true;
    }

    internal void LogGestureConflict(string gesture, string winnerId, string loserId)
    {
        _logger.LogWarning(
            "Gesture conflict for '{Gesture}': '{WinnerId}' wins over '{LoserId}'",
            gesture, winnerId, loserId);
    }

    private static bool TryParseGesture(string gesture, out string normalizedGesture)
    {
        normalizedGesture = string.Empty;

        if (string.IsNullOrWhiteSpace(gesture))
            return false;

        var tokens = gesture.Split('+');
        if (tokens.Length == 0)
            return false;

        var modifiers = new List<string>();
        foreach (var token in tokens.Take(tokens.Length - 1))
        {
            var trimmed = token.Trim();
            if (trimmed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                modifiers.Add("Ctrl");
            else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifiers.Add("Alt");
            else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifiers.Add("Shift");
            else if (trimmed.Equals("Meta", StringComparison.OrdinalIgnoreCase))
                modifiers.Add("Meta");
            else
                return false;
        }

        var keyToken = tokens[^1].Trim();
        if (!Enum.TryParse<Avalonia.Input.Key>(keyToken, true, out var keyValue))
            return false;

        if (!keyValue.ToString().Equals(keyToken, StringComparison.OrdinalIgnoreCase))
            return false;

        var keyName = keyValue.ToString();
        normalizedGesture = string.Join("+", modifiers.Concat(new[] { keyName }));
        return true;
    }
}
