using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Contracts.Transparency.Trace;

namespace Zaide.Features.Agents.Application.Transparency.Trace;

/// <summary>
/// In-process registry of backend evidence sources. Composition root
/// registers one source per production backend; the registry auto-populates
/// from the <see cref="IEnumerable{T}"/> of registered sources supplied at
/// construction. Tests may register additional sources through
/// <see cref="Register"/>.
/// </summary>
internal sealed class AgentTraceSourceRegistry : IAgentTraceSourceRegistry
{
    private readonly Dictionary<string, IAgentTraceBackendEvidenceSource> _sources =
        new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public AgentTraceSourceRegistry()
    {
    }

    public AgentTraceSourceRegistry(IEnumerable<IAgentTraceBackendEvidenceSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        foreach (var source in sources)
        {
            Register(source);
        }
    }

    public IReadOnlyList<IAgentTraceBackendEvidenceSource> All
    {
        get
        {
            lock (_gate)
            {
                return _sources.Values.ToArray();
            }
        }
    }

    public void Register(IAgentTraceBackendEvidenceSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.BackendId))
        {
            throw new ArgumentException(
                "Source backend id is required.",
                nameof(source));
        }

        lock (_gate)
        {
            _sources[source.BackendId] = source;
        }
    }

    public bool TryGet(string backendId, out IAgentTraceBackendEvidenceSource source)
    {
        source = null!;
        if (string.IsNullOrWhiteSpace(backendId))
        {
            return false;
        }

        lock (_gate)
        {
            return _sources.TryGetValue(backendId, out source!);
        }
    }
}
