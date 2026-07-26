using System;

namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Passive, read-only source-control snapshot for IDE context assembly.
/// </summary>
public sealed class SourceControlStatusSnapshot
{
    public SourceControlStatusSnapshot(
        long generation,
        SourceControlSnapshotAvailability availability,
        string? workspacePath = null,
        RepositoryStatusSnapshot? repositoryStatus = null,
        string? statusMessage = null)
    {
        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generation),
                generation,
                "Generation cannot be negative.");
        }

        if (!Enum.IsDefined(availability))
        {
            throw new ArgumentOutOfRangeException(
                nameof(availability),
                availability,
                "Availability is invalid.");
        }

        Generation = generation;
        Availability = availability;
        WorkspacePath = workspacePath;
        RepositoryStatus = repositoryStatus?.CloneDefensively();
        StatusMessage = statusMessage;
    }

    public long Generation { get; }

    public SourceControlSnapshotAvailability Availability { get; }

    public string? WorkspacePath { get; }

    public RepositoryStatusSnapshot? RepositoryStatus { get; }

    public string? StatusMessage { get; }
}
