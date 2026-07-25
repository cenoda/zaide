using System.Threading;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Test-only file reader that records how many times it executed and returns a
/// configurable result. Used to prove duplicate requests do not re-execute and
/// to exercise broker result mapping without touching the filesystem.
/// </summary>
internal sealed class CountingAgentFileReader : IAgentFileReader
{
    public CountingAgentFileReader()
        : this(AgentFileReadResult.Success(
            "ok",
            AgentContentRevision.FromUtf8Text("ok"),
            byteLength: 2))
    {
    }

    public CountingAgentFileReader(AgentFileReadResult result)
    {
        Result = result;
    }

    public int ReadCount { get; private set; }

    public AgentFileReadResult Result { get; set; }

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

        return Result;
    }
}
