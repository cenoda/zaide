using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Complete durable ACP binding identity for atomic runtime-auth and probe
/// publication guards. Includes revision plus runtime/expected identity fields.
/// </summary>
internal sealed class AcpRuntimeBindingFingerprint : IEquatable<AcpRuntimeBindingFingerprint>
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
        // Genuine snapshot: capture the arguments list at construction so a
        // caller-mutable source cannot alter this fingerprint's identity.
        Arguments = runtimeIdentity.Arguments.ToArray();
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

    public bool Equals(AcpRuntimeBindingFingerprint? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Revision == other.Revision
               && string.Equals(ExpectedAgentName, other.ExpectedAgentName, StringComparison.Ordinal)
               && string.Equals(ExpectedAgentVersion, other.ExpectedAgentVersion, StringComparison.Ordinal)
               && string.Equals(ExecutablePath, other.ExecutablePath, StringComparison.Ordinal)
               && Arguments.SequenceEqual(other.Arguments, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as AcpRuntimeBindingFingerprint);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Revision);
        hash.Add(ExpectedAgentName, StringComparer.Ordinal);
        hash.Add(ExpectedAgentVersion, StringComparer.Ordinal);
        hash.Add(ExecutablePath, StringComparer.Ordinal);
        foreach (var argument in Arguments)
        {
            hash.Add(argument, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public static AcpRuntimeBindingFingerprint FromBinding(AgentActorBackendBinding binding) =>
        new(
            binding.Revision,
            binding.AcpRuntime!,
            binding.ExpectedAgentName!,
            binding.ExpectedAgentVersion!);
}
