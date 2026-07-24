using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable command request represented as executable plus argument vector.
/// </summary>
internal sealed class AgentExecuteCommandActionPayload : AgentActionPayload
{
    public AgentExecuteCommandActionPayload(
        string executable,
        IReadOnlyList<string> arguments,
        AgentWorkspaceRelativePath workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            throw new ArgumentException("Executable is required.", nameof(executable));
        }

        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Command arguments cannot be blank.", nameof(arguments));
        }

        Executable = executable.Trim();
        Arguments = arguments.ToArray();
        WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
    }

    public override AgentActionKind Kind => AgentActionKind.ExecuteCommand;

    public string Executable { get; }

    public IReadOnlyList<string> Arguments { get; }

    public AgentWorkspaceRelativePath WorkingDirectory { get; }
}
