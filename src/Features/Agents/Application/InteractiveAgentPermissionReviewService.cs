using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Production agent permission review service. Presents review requests on the
/// visible Zaide-owned review surface through
/// <see cref="IAgentPermissionDialogPresenter"/>.
///
/// <para>
/// Fail-closed behavior: when no presenter is configured (headless
/// construction) or the presenter fails, the exception propagates to the
/// broker, which classifies the outcome as
/// <see cref="AgentActionFailureKind.PermissionUnavailable"/>. No decision
/// object is fabricated for an unreviewable request.
/// </para>
///
/// <para>
/// Cancellation is preserved: <see cref="OperationCanceledException"/>
/// propagates so the broker returns <see cref="AgentActionResultKind.Cancelled"/>
/// rather than <see cref="AgentActionFailureKind.PermissionDenied"/>.
/// </para>
///
/// <para>
/// Decisions are created with <see cref="AgentPermissionDecisionStatus.Published"/>
/// for allowed actions (the broker atomically consumes them via
/// <see cref="AgentPermissionDecision.TryConsume"/> after validation) or
/// <see cref="AgentPermissionDecisionStatus.Denied"/> for denied/dismissed
/// ones. The classification is always
/// <see cref="AgentActionPermissionClassification.RequiresUserDecision"/>
/// because this service is only invoked when the policy classifier requires a
/// user decision.
/// </para>
/// </summary>
internal sealed class InteractiveAgentPermissionReviewService : IAgentPermissionReviewService
{
    private readonly IAgentPermissionDialogPresenter? _presenter;

    /// <summary>
    /// Creates the service. When <paramref name="presenter"/> is null the
    /// service fails closed by throwing on every review request (headless
    /// mode; the broker maps this to
    /// <see cref="AgentActionFailureKind.PermissionUnavailable"/>). The
    /// production wiring injects <see cref="Presentation.PermissionReviewDialogPresenter"/>.
    /// </summary>
    public InteractiveAgentPermissionReviewService(
        IAgentPermissionDialogPresenter? presenter = null)
    {
        _presenter = presenter;
    }

    /// <inheritdoc/>
    public async ValueTask<AgentPermissionDecision> RequestDecisionAsync(
        AgentActionRequest request,
        AgentActionDisplaySummary displaySummary,
        WorkspaceActionScope? workspaceScope,
        CancellationToken cancellationToken)
    {
        AgentPathEvidenceInvocationCounters.RecordPermissionReviewRequest();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(displaySummary);

        cancellationToken.ThrowIfCancellationRequested();

        if (_presenter is null)
        {
            // No visible review surface exists. Fail closed as
            // PermissionUnavailable instead of fabricating a user denial.
            throw new InvalidOperationException(
                "No permission review surface is available; the request cannot be reviewed.");
        }

        // OperationCanceledException and presenter failures intentionally
        // propagate: the broker maps them to Cancelled and
        // PermissionUnavailable respectively.
        var isAllowed = await _presenter.ShowAsync(
            request, displaySummary, workspaceScope, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(AgentActionBudgets.PermissionDecisionLifetime);
        var decisionId = AgentPermissionDecisionId.New();

        // Allowed decisions start as Published; the broker atomically
        // transitions Published → Consumed after re-validating fingerprint,
        // classification, status, expiry, and workspace freshness.
        // Denied decisions (including dismiss) are terminal.
        var status = isAllowed
            ? AgentPermissionDecisionStatus.Published
            : AgentPermissionDecisionStatus.Denied;

        return new AgentPermissionDecision(
            decisionId,
            request.Fingerprint,
            AgentActionPermissionClassification.RequiresUserDecision,
            status,
            now,
            expiresAt,
            isAllowed);
    }
}
