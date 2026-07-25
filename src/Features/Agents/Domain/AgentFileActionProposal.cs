using System;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable file action proposal that binds together the proposal identity,
/// operation, path, base state, proposed state, workspace scope, and permission
/// fingerprint. This is the non-mutating representation used for permission
/// review and later mutation execution.
/// </summary>
internal sealed class AgentFileActionProposal : IEquatable<AgentFileActionProposal>
{
    private readonly AgentFileProposal _proposal;
    private readonly WorkspaceActionScope _workspaceScope;
    private readonly AgentActionRequestFingerprint _permissionFingerprint;
    private readonly AgentContentRevision? _permissionFingerprintBaseRevision;

    /// <summary>
    /// Creates a new immutable file action proposal.
    /// </summary>
    /// <param name="proposalId">Unique identifier for this proposal.</param>
    /// <param name="proposal">The underlying file proposal with operation, path, and revisions.</param>
    /// <param name="workspaceScope">The workspace scope at the time of proposal creation.</param>
    /// <param name="permissionFingerprint">The permission fingerprint that binds this proposal to a specific request.</param>
    /// <param name="permissionFingerprintBaseRevision">The base revision captured in the permission fingerprint, if any.</param>
    public AgentFileActionProposal(
        AgentFileProposalId proposalId,
        AgentFileProposal proposal,
        WorkspaceActionScope workspaceScope,
        AgentActionRequestFingerprint permissionFingerprint,
        AgentContentRevision? permissionFingerprintBaseRevision)
    {
        if (proposal is null) throw new ArgumentNullException(nameof(proposal));
        if (workspaceScope is null) throw new ArgumentNullException(nameof(workspaceScope));
        if (permissionFingerprint == default) throw new ArgumentException("Permission fingerprint is required.", nameof(permissionFingerprint));

        if (proposalId == default)
        {
            throw new ArgumentException("Proposal id is required.", nameof(proposalId));
        }

        ProposalId = proposalId;
        _proposal = proposal;
        _workspaceScope = workspaceScope;
        _permissionFingerprint = permissionFingerprint;
        _permissionFingerprintBaseRevision = permissionFingerprintBaseRevision;
    }

    /// <summary>
    /// Unique identifier for this proposal.
    /// </summary>
    public AgentFileProposalId ProposalId { get; }

    /// <summary>
    /// The underlying file proposal with operation, path, and content revisions.
    /// </summary>
    public AgentFileProposal Proposal => _proposal;

    /// <summary>
    /// The workspace scope at the time of proposal creation.
    /// </summary>
    public WorkspaceActionScope WorkspaceScope => _workspaceScope;

    /// <summary>
    /// The permission fingerprint that binds this proposal to a specific request.
    /// </summary>
    public AgentActionRequestFingerprint PermissionFingerprint => _permissionFingerprint;

    /// <summary>
    /// The base revision captured in the permission fingerprint, if any.
    /// Used for stale base detection.
    /// </summary>
    public AgentContentRevision? PermissionFingerprintBaseRevision => _permissionFingerprintBaseRevision;

    /// <summary>
    /// The operation type (create, replace, delete).
    /// </summary>
    public AgentFileProposalOperation Operation => _proposal.Operation;

    /// <summary>
    /// The workspace-relative path of the file.
    /// </summary>
    public AgentWorkspaceRelativePath Path => _proposal.Path;

    /// <summary>
    /// Whether the base file existed at the time of proposal creation.
    /// </summary>
    public bool BaseExists => _proposal.BaseExists;

    /// <summary>
    /// The base content revision, if the file existed.
    /// </summary>
    public AgentContentRevision? BaseRevision => _proposal.BaseRevision;

    /// <summary>
    /// The proposed content revision, if applicable (not for delete operations).
    /// </summary>
    public AgentContentRevision? ProposedRevision => _proposal.ProposedRevision;

    /// <summary>
    /// Bounded change summary for display purposes.
    /// </summary>
    public string BoundedChangeSummary => _proposal.BoundedChangeSummary;

    /// <summary>
    /// Checks if the base content has changed since the proposal was created.
    /// This is used for stale base detection before decision consumption.
    /// </summary>
    /// <param name="currentBaseRevision">The current base revision from the filesystem.</param>
    /// <returns>True if the base is stale (different from what was captured), false otherwise.</returns>
    public bool IsBaseStale(AgentContentRevision? currentBaseRevision)
    {
        // For create operations, the base should not exist, so if it exists now, it's stale
        if (Operation == AgentFileProposalOperation.Create)
        {
            return currentBaseRevision is not null;
        }

        // For replace and delete operations, compare the current base revision with the captured one
        if (BaseRevision is null)
        {
            // Base didn't exist at proposal time but exists now - this shouldn't happen for replace/delete
            return currentBaseRevision is not null;
        }

        // Base existed at proposal time - check if it's different now
        return !BaseRevision.Equals(currentBaseRevision);
    }

    /// <summary>
    /// Checks if the permission fingerprint base revision matches the proposal's base revision.
    /// This ensures the proposal is bound to the same base state that was used for permission review.
    /// </summary>
    /// <returns>True if the permission fingerprint base revision matches the proposal's base revision.</returns>
    public bool PermissionFingerprintMatchesBase()
    {
        if (PermissionFingerprintBaseRevision is null && BaseRevision is null)
        {
            return true;
        }

        if (PermissionFingerprintBaseRevision is null || BaseRevision is null)
        {
            return false;
        }

        return PermissionFingerprintBaseRevision.Equals(BaseRevision);
    }

    public bool Equals(AgentFileActionProposal? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return ProposalId.Equals(other.ProposalId)
            && _proposal.Equals(other._proposal)
            && _workspaceScope.Equals(other._workspaceScope)
            && _permissionFingerprint.Equals(other._permissionFingerprint)
            && Nullable.Equals(_permissionFingerprintBaseRevision, other._permissionFingerprintBaseRevision);
    }

    public override bool Equals(object? obj) => Equals(obj as AgentFileActionProposal);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = ProposalId.GetHashCode();
            hash = (hash * 397) ^ _proposal.GetHashCode();
            hash = (hash * 397) ^ _workspaceScope.GetHashCode();
            hash = (hash * 397) ^ _permissionFingerprint.GetHashCode();
            hash = (hash * 397) ^ (_permissionFingerprintBaseRevision?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public static bool operator ==(AgentFileActionProposal? left, AgentFileActionProposal? right)
    {
        if (left is null)
        {
            return right is null;
        }
        return left.Equals(right);
    }

    public static bool operator !=(AgentFileActionProposal? left, AgentFileActionProposal? right) =>
        !(left == right);
}