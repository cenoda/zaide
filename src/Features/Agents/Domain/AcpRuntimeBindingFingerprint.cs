using System;
using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Complete durable ACP binding identity for atomic runtime-auth and probe
/// publication guards. Includes revision plus runtime/expected identity fields.
/// </summary>
internal sealed class AcpRuntimeBindingFingerprint
{
    public AcpRuntimeBindingFingerprint(
        long revision,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion)
    {
        if (revision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), revision, "Revision must be >= 1.");
        }

        ArgumentNullException.ThrowIfNull(runtimeIdentity);
        if (string.IsNullOrWhiteSpace(expectedAgentName))
        {
            throw new ArgumentException("Expected agent name is required.", nameof(expectedAgentName));
        }

        if (string.IsNullOrWhiteSpace(expectedAgentVersion))
        {
            throw new ArgumentException("Expected agent version is required.", nameof(expectedAgentVersion));
        }

        Revision = revision;
        ExecutablePath = runtimeIdentity.ExecutablePath;
        Arguments = runtimeIdentity.Arguments;
        ExpectedAgentName = expectedAgentName.Trim();
        ExpectedAgentVersion = expectedAgentVersion.Trim();
    }

    public long Revision { get; }

    public string ExecutablePath { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string ExpectedAgentName { get; }

    public string ExpectedAgentVersion { get; }

    public bool Matches(AgentActorBackendBinding binding)
    {
        if (binding.BackendId != AgentBackendIds.Acp || binding.AcpRuntime is null)
        {
            return false;
        }

        return binding.Revision == Revision
               && string.Equals(binding.ExpectedAgentName, ExpectedAgentName, StringComparison.Ordinal)
               && string.Equals(binding.ExpectedAgentVersion, ExpectedAgentVersion, StringComparison.Ordinal)
               && string.Equals(binding.AcpRuntime.ExecutablePath, ExecutablePath, StringComparison.Ordinal)
               && binding.AcpRuntime.MatchesArguments(Arguments);
    }

    public static AcpRuntimeBindingFingerprint FromBinding(AgentActorBackendBinding binding) =>
        new(
            binding.Revision,
            binding.AcpRuntime!,
            binding.ExpectedAgentName!,
            binding.ExpectedAgentVersion!);
}
