using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Computes deterministic fingerprints for immutable action requests.
/// </summary>
internal static class AgentActionRequestFingerprintComputer
{
    public static AgentActionRequestFingerprint Compute(AgentActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Compute(
            request.WorkspaceIdentity,
            request.WorkspaceGeneration,
            request.RunId,
            request.Payload);
    }

    public static AgentActionRequestFingerprint Compute(
        WorkspaceIdentity workspaceIdentity,
        WorkspaceGeneration workspaceGeneration,
        ExecutionRunId runId,
        AgentActionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var canonical = BuildCanonicalText(workspaceIdentity, workspaceGeneration, runId, payload);
        return AgentActionRequestFingerprint.FromCanonicalText(canonical);
    }

    private static string BuildCanonicalText(
        WorkspaceIdentity workspaceIdentity,
        WorkspaceGeneration workspaceGeneration,
        ExecutionRunId runId,
        AgentActionPayload payload)
    {
        var builder = new StringBuilder();
        builder.Append("kind=").Append(payload.Kind.ToString()).Append('\n');
        builder.Append("workspace=").Append(workspaceIdentity.Value).Append('\n');
        builder.Append("generation=").Append(workspaceGeneration.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("run=").Append(runId.Value).Append('\n');

        switch (payload)
        {
            case AgentReadFileActionPayload read:
                builder.Append("path=").Append(read.Path.NormalizedPath);
                break;

            case AgentCreateFileActionPayload create:
                builder.Append("path=").Append(create.Path.NormalizedPath).Append('\n');
                builder.Append("proposed=").Append(create.ProposedRevision.Value);
                break;

            case AgentReplaceFileActionPayload replace:
                builder.Append("path=").Append(replace.Path.NormalizedPath).Append('\n');
                builder.Append("base=").Append(replace.BaseRevision.Value).Append('\n');
                builder.Append("proposed=").Append(replace.ProposedRevision.Value);
                break;

            case AgentDeleteFileActionPayload delete:
                builder.Append("path=").Append(delete.Path.NormalizedPath).Append('\n');
                builder.Append("base=").Append(delete.BaseRevision.Value);
                break;

            case AgentExecuteCommandActionPayload command:
                builder.Append("executable=").Append(command.Executable).Append('\n');
                builder.Append("working-directory=").Append(command.WorkingDirectory.NormalizedPath).Append('\n');
                builder.Append("arguments=").Append(string.Join('\u001f', command.Arguments));
                break;

            default:
                throw new InvalidOperationException($"Unsupported action payload type '{payload.GetType().Name}'.");
        }

        return builder.ToString();
    }
}
