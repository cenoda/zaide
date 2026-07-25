using System;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Production factory for run-scoped <see cref="ContractAgentActionBroker"/>
/// instances with all Phase 17 executor dependencies.
/// </summary>
internal sealed class AgentActionBrokerFactory : IAgentActionBrokerFactory
{
    private readonly IWorkspaceActionAuthority _workspaceAuthority;
    private readonly IAgentFileReader _fileReader;
    private readonly IAgentFileMutator _fileMutator;
    private readonly IAgentCommandResolver _commandResolver;
    private readonly IAgentCommandExecutor _commandExecutor;
    private readonly IAgentPermissionReviewService _permissionReviewService;
    private readonly IAgentDocumentReconciler _documentReconciler;

    public AgentActionBrokerFactory(
        IWorkspaceActionAuthority workspaceAuthority,
        IAgentFileReader fileReader,
        IAgentFileMutator fileMutator,
        IAgentCommandResolver commandResolver,
        IAgentCommandExecutor commandExecutor,
        IAgentPermissionReviewService permissionReviewService,
        IAgentDocumentReconciler documentReconciler)
    {
        _workspaceAuthority = workspaceAuthority ?? throw new ArgumentNullException(nameof(workspaceAuthority));
        _fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
        _fileMutator = fileMutator ?? throw new ArgumentNullException(nameof(fileMutator));
        _commandResolver = commandResolver ?? throw new ArgumentNullException(nameof(commandResolver));
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _permissionReviewService = permissionReviewService ?? throw new ArgumentNullException(nameof(permissionReviewService));
        _documentReconciler = documentReconciler ?? throw new ArgumentNullException(nameof(documentReconciler));
    }

    public IAgentActionBroker CreateRunScopedBroker(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ActorId initiatingActorId,
        ActorId targetActorId,
        AgentBackendId backendId,
        IAgentActionEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(eventPublisher);

        return new ContractAgentActionBroker(
            sessionId,
            runId,
            conversationId,
            initiatingActorId,
            targetActorId,
            backendId,
            _workspaceAuthority,
            _fileReader,
            _fileMutator,
            _commandResolver,
            _commandExecutor,
            new AgentActionRunSlotTracker(),
            new AgentActionCorrelationRegistry(),
            _permissionReviewService,
            _documentReconciler,
            eventPublisher);
    }
}
