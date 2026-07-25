using System;
using System.Text;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Builds permission-review-ready display summaries for immutable action payloads.
/// </summary>
internal static class AgentActionDisplaySummaryBuilder
{
    public static AgentActionDisplaySummary Build(AgentActionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return payload switch
        {
            AgentReadFileActionPayload read => new(
                AgentActionKind.ReadFile,
                "Read workspace file",
                BuildReadDetail(read),
                wasTruncated: false),
            AgentCreateFileActionPayload create => new(
                AgentActionKind.CreateFile,
                "Create workspace file",
                BuildCreateDetail(create),
                wasTruncated: false),
            AgentReplaceFileActionPayload replace => new(
                AgentActionKind.ReplaceFile,
                "Replace workspace file",
                BuildReplaceDetail(replace),
                wasTruncated: false),
            AgentDeleteFileActionPayload delete => new(
                AgentActionKind.DeleteFile,
                "Delete workspace file",
                BuildDeleteDetail(delete),
                wasTruncated: false),
            AgentExecuteCommandActionPayload command => new(
                AgentActionKind.ExecuteCommand,
                "Execute command",
                BuildCommandDetail(command),
                wasTruncated: false),
            _ => throw new InvalidOperationException($"Unsupported action payload type '{payload.GetType().Name}'."),
        };
    }

    private static string BuildReadDetail(AgentReadFileActionPayload read) =>
        new StringBuilder()
            .AppendLine($"Path: {read.Path.NormalizedPath}")
            .Append("Scope: one bounded regular-file read within the active workspace.")
            .ToString();

    private static string BuildCreateDetail(AgentCreateFileActionPayload create)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Path: {create.Path.NormalizedPath}");
        builder.AppendLine($"Proposed revision: {create.ProposedRevision.Value}");
        builder.AppendLine("Operation: create");
        builder.AppendLine("Scope: this exact request only.");
        builder.Append("Proposed content preview:");
        builder.AppendLine();
        builder.Append(create.ProposedText);
        return builder.ToString();
    }

    private static string BuildReplaceDetail(AgentReplaceFileActionPayload replace)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Path: {replace.Path.NormalizedPath}");
        builder.AppendLine($"Base revision: {replace.BaseRevision.Value}");
        builder.AppendLine($"Proposed revision: {replace.ProposedRevision.Value}");
        builder.AppendLine("Operation: replace");
        builder.AppendLine("Scope: this exact request only.");
        builder.Append("Proposed content preview:");
        builder.AppendLine();
        builder.Append(replace.ProposedText);
        return builder.ToString();
    }

    private static string BuildDeleteDetail(AgentDeleteFileActionPayload delete) =>
        new StringBuilder()
            .AppendLine($"Path: {delete.Path.NormalizedPath}")
            .AppendLine($"Base revision: {delete.BaseRevision.Value}")
            .AppendLine("Operation: delete")
            .Append("Scope: this exact request only.")
            .ToString();

    private static string BuildCommandDetail(AgentExecuteCommandActionPayload command)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Executable: {command.Executable}");
        builder.AppendLine($"Working directory: {command.WorkingDirectory.NormalizedPath}");
        builder.AppendLine("Arguments:");
        for (var index = 0; index < command.Arguments.Count; index++)
        {
            builder.Append(index).Append(": ").AppendLine(command.Arguments[index]);
        }

        builder.AppendLine("Scope: this exact request only.");
        builder.Append(
            "Disclosure: working-directory scope is not filesystem or network sandboxing.");
        return builder.ToString();
    }
}
