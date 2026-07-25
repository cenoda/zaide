using System;
using System.Collections.Generic;
using System.Threading;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Test-only file reader that records how many times it executed and returns
/// path-aware or queued results. Used to prove duplicate requests do not
/// re-execute, exercise broker result mapping without touching the filesystem,
/// and model confirmed <see cref="AgentFileReadOutcome.NotFound"/> versus
/// successful reads for create proposal generation.
/// </summary>
internal sealed class CountingAgentFileReader : IAgentFileReader
{
    private readonly Dictionary<string, AgentFileReadResult> _pathResults =
        new(StringComparer.Ordinal);

    private readonly Queue<AgentFileReadResult> _queuedResults = new();

    private AgentFileReadResult _defaultResult;

    public CountingAgentFileReader()
    {
        _defaultResult = AgentFileReadResult.Rejected(
            AgentFileReadOutcome.NotFound,
            "File does not exist in the workspace.");
    }

    public CountingAgentFileReader(AgentFileReadResult defaultResult)
    {
        _defaultResult = defaultResult;
    }

    public int ReadCount { get; private set; }

    public AgentFileReadResult DefaultResult
    {
        get => _defaultResult;
        set => _defaultResult = value;
    }

    public void SetPathResult(AgentWorkspaceRelativePath path, AgentFileReadResult result) =>
        _pathResults[path.NormalizedPath] = result;

    public void SetPathResult(string normalizedPath, AgentFileReadResult result) =>
        _pathResults[normalizedPath] = result;

    public void EnqueueReads(params AgentFileReadResult[] results)
    {
        foreach (var result in results)
        {
            _queuedResults.Enqueue(result);
        }
    }

    public AgentFileReadResult Read(
        WorkspaceActionScope scope,
        AgentWorkspaceRelativePath path,
        CancellationToken cancellationToken)
    {
        ReadCount++;

        if (cancellationToken.IsCancellationRequested)
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.Cancelled,
                "Read was cancelled.");
        }

        if (_queuedResults.Count > 0)
        {
            return _queuedResults.Dequeue();
        }

        if (_pathResults.TryGetValue(path.NormalizedPath, out var pathResult))
        {
            return pathResult;
        }

        return _defaultResult;
    }
}
