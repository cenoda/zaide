using System;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Result of attempting to create a file action proposal.
/// </summary>
internal sealed class AgentFileProposalResult
{
    private readonly AgentFileActionProposal? _proposal;
    private readonly Exception? _exception;
    private readonly bool _success;

    private AgentFileProposalResult(AgentFileActionProposal proposal)
    {
        _proposal = proposal;
        _success = true;
    }

    private AgentFileProposalResult(Exception exception)
    {
        _exception = exception;
        _success = false;
    }

    /// <summary>
    /// Whether the proposal was successfully created.
    /// </summary>
    public bool IsSuccess => _success;

    /// <summary>
    /// The created proposal, if successful.
    /// </summary>
    public AgentFileActionProposal Proposal =>
        _proposal ?? throw new InvalidOperationException("Proposal is not available for failed results.");

    /// <summary>
    /// The exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception => _exception;

    /// <summary>
    /// Creates a successful proposal result.
    /// </summary>
    public static AgentFileProposalResult Success(AgentFileActionProposal proposal) =>
        new(proposal);

    /// <summary>
    /// Creates a failed proposal result.
    /// </summary>
    public static AgentFileProposalResult Failed(Exception exception) =>
        new(exception);

    /// <summary>
    /// Creates a failed proposal result with a message.
    /// </summary>
    public static AgentFileProposalResult Failed(string message) =>
        new(new InvalidOperationException(message));
}