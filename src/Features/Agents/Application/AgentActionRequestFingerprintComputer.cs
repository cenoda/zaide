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

    public static AgentActionRequestFingerprint Compute(
        WorkspaceIdentity workspaceIdentity,
        WorkspaceGeneration workspaceGeneration,
        ExecutionRunId runId,
        AgentResolvedCommand resolvedCommand)
    {
        ArgumentNullException.ThrowIfNull(resolvedCommand);
        var canonical = BuildCanonicalCommandText(
            workspaceIdentity,
            workspaceGeneration,
            runId,
            resolvedCommand);
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
                if (!AgentResolvedCommand.TryCreate(command, out var resolvedCommand, out var error))
                {
                    throw new InvalidOperationException(error);
                }

                AppendResolvedCommand(builder, resolvedCommand!);
                break;

            default:
                throw new InvalidOperationException($"Unsupported action payload type '{payload.GetType().Name}'.");
        }

        return builder.ToString();
    }

    private static string BuildCanonicalCommandText(
        WorkspaceIdentity workspaceIdentity,
        WorkspaceGeneration workspaceGeneration,
        ExecutionRunId runId,
        AgentResolvedCommand resolvedCommand)
    {
        var builder = new StringBuilder();
        builder.Append("kind=").Append(AgentActionKind.ExecuteCommand.ToString()).Append('\n');
        builder.Append("workspace=").Append(workspaceIdentity.Value).Append('\n');
        builder.Append("generation=").Append(workspaceGeneration.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("run=").Append(runId.Value).Append('\n');
        AppendResolvedCommand(builder, resolvedCommand);
        return builder.ToString();
    }

    private static void AppendResolvedCommand(StringBuilder builder, AgentResolvedCommand resolvedCommand)
    {
        builder.Append("executable=").Append(resolvedCommand.CanonicalAbsoluteExecutablePath).Append('\n');
        builder.Append("denylist=").Append(resolvedCommand.DenylistResult.Classification.ToString()).Append('\n');
        builder.Append("working-directory=").Append(resolvedCommand.WorkingDirectory.NormalizedPath).Append('\n');
        builder.Append("arguments=").Append(string.Join('\u001f', resolvedCommand.Arguments));
    }
}
