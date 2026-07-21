using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One capability row within a versioned backend capability snapshot.
/// </summary>
internal sealed class AgentCapabilityRow
{
    private AgentCapabilityRow(AgentCapabilityId capabilityId, AgentCapabilityState state)
    {
        CapabilityId = capabilityId;
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public AgentCapabilityId CapabilityId { get; }

    public AgentCapabilityState State { get; }

    public static AgentCapabilityRow Create(AgentCapabilityId capabilityId, AgentCapabilityState state)
    {
        if (capabilityId == default)
        {
            throw new ArgumentException("Capability id is required.", nameof(capabilityId));
        }

        return new AgentCapabilityRow(capabilityId, state);
    }
}

/// <summary>
/// Immutable versioned capability snapshot for one backend identity.
/// </summary>
internal sealed class AgentCapabilitySnapshot
{
    private AgentCapabilitySnapshot(
        int version,
        AgentBackendId backendId,
        AgentCapabilityRow[] rows)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Version must be positive.");
        }

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (rows.Length == 0)
        {
            throw new ArgumentException("At least one capability row is required.", nameof(rows));
        }

        Version = version;
        BackendId = backendId;
        Rows = Array.AsReadOnly(rows);
    }

    public int Version { get; }

    public AgentBackendId BackendId { get; }

    public IReadOnlyList<AgentCapabilityRow> Rows { get; }

    public bool TryGetState(
        AgentCapabilityId capabilityId,
        [NotNullWhen(true)] out AgentCapabilityState? state)
    {
        foreach (var row in Rows)
        {
            if (row.CapabilityId == capabilityId)
            {
                state = row.State;
                return true;
            }
        }

        state = null;
        return false;
    }

    public static AgentCapabilitySnapshot CreateInitial(
        AgentBackendId backendId,
        IEnumerable<AgentCapabilityRow> rows,
        int version = 1) =>
        new(version, backendId, NormalizeRows(rows));

    public AgentCapabilitySnapshot WithRow(AgentCapabilityRow row, int version)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (version <= Version)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "Capability snapshot version must increase.");
        }

        var updatedRows = Rows
            .Where(existing => existing.CapabilityId != row.CapabilityId)
            .Append(row);

        return new AgentCapabilitySnapshot(version, BackendId, NormalizeRows(updatedRows));
    }

    private static AgentCapabilityRow[] NormalizeRows(IEnumerable<AgentCapabilityRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var normalized = rows.ToList();
        if (normalized.Count == 0)
        {
            throw new ArgumentException("At least one capability row is required.", nameof(rows));
        }

        var seen = new HashSet<AgentCapabilityId>();
        foreach (var row in normalized)
        {
            if (row.CapabilityId == default)
            {
                throw new ArgumentException(
                    "Capability rows cannot use the default capability id.",
                    nameof(rows));
            }

            if (!seen.Add(row.CapabilityId))
            {
                throw new ArgumentException(
                    "Duplicate capability rows are not allowed.",
                    nameof(rows));
            }
        }

        return normalized
            .OrderBy(row => row.CapabilityId.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
