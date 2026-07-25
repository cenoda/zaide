using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable file proposal captured before permission review or apply.
/// </summary>
internal sealed class AgentFileProposal
{
    public AgentFileProposal(
        AgentFileProposalOperation operation,
        AgentWorkspaceRelativePath path,
        bool baseExists,
        AgentContentRevision? baseRevision,
        AgentContentRevision? proposedRevision,
        string boundedChangeSummary)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "Proposal operation is invalid.");
        }

        Path = path ?? throw new ArgumentNullException(nameof(path));
        Operation = operation;
        BaseExists = baseExists;
        BaseRevision = baseRevision;
        ProposedRevision = proposedRevision;
        BoundedChangeSummary = boundedChangeSummary ?? string.Empty;

        ValidateRevisionRules(operation, baseExists, baseRevision, proposedRevision);
    }

    public AgentFileProposalOperation Operation { get; }

    public AgentWorkspaceRelativePath Path { get; }

    public bool BaseExists { get; }

    public AgentContentRevision? BaseRevision { get; }

    public AgentContentRevision? ProposedRevision { get; }

    public string BoundedChangeSummary { get; }

    private static void ValidateRevisionRules(
        AgentFileProposalOperation operation,
        bool baseExists,
        AgentContentRevision? baseRevision,
        AgentContentRevision? proposedRevision)
    {
        switch (operation)
        {
            case AgentFileProposalOperation.Create:
                if (baseExists)
                {
                    throw new ArgumentException("Create proposals require a missing base file.");
                }

                if (baseRevision is not null)
                {
                    throw new ArgumentException("Create proposals cannot include a base revision.");
                }

                if (proposedRevision is null)
                {
                    throw new ArgumentException("Create proposals require a proposed revision.");
                }

                break;

            case AgentFileProposalOperation.Replace:
                if (!baseExists)
                {
                    throw new ArgumentException("Replace proposals require an existing base file.");
                }

                if (baseRevision is null || proposedRevision is null)
                {
                    throw new ArgumentException("Replace proposals require base and proposed revisions.");
                }

                break;

            case AgentFileProposalOperation.Delete:
                if (!baseExists)
                {
                    throw new ArgumentException("Delete proposals require an existing base file.");
                }

                if (baseRevision is null)
                {
                    throw new ArgumentException("Delete proposals require a base revision.");
                }

                if (proposedRevision is not null)
                {
                    throw new ArgumentException("Delete proposals cannot include a proposed revision.");
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, "Proposal operation is invalid.");
        }
    }
}
