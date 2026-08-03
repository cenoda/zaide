using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Zaide-owned ACP runtime launch identity. Registry and distribution fields are
/// provenance evidence only and never used for routing.
/// </summary>
internal sealed class AcpRuntimeIdentity
{
    public AcpRuntimeIdentity(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? registryId = null,
        string? distributionProvenance = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        if (!System.IO.Path.IsPathRooted(executablePath))
        {
            throw new ArgumentException("Executable path must be absolute.", nameof(executablePath));
        }

        ExecutablePath = System.IO.Path.GetFullPath(executablePath);
        // Defensive snapshot: the caller may continue to mutate the source
        // collection after construction. Without this copy, identity and
        // fingerprint equality drift independently of any revision/epoch
        // mutation, which breaks durable/in-flight identity comparisons.
        Arguments = (arguments ?? Array.Empty<string>()).ToArray();
        RegistryId = NormalizeOptional(registryId);
        DistributionProvenance = NormalizeOptional(distributionProvenance);
    }

    public string ExecutablePath { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string? RegistryId { get; }

    public string? DistributionProvenance { get; }

    public bool MatchesExecutable(string observedExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(observedExecutablePath))
        {
            return false;
        }

        return string.Equals(
            ExecutablePath,
            System.IO.Path.GetFullPath(observedExecutablePath),
            StringComparison.Ordinal);
    }

    public bool MatchesArguments(IReadOnlyList<string> observedArguments) =>
        Arguments.SequenceEqual(observedArguments, StringComparer.Ordinal);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
