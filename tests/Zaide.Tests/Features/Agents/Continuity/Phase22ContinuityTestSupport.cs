using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Continuity;

internal static class Phase22ContinuityTestSupport
{
    public sealed class Harness : IDisposable
    {
        private readonly string _rootDirectory;

        public Harness(
            AgentBackendId backendId,
            string? workspaceRoot = null,
            string? processCwd = null)
        {
            _rootDirectory = Path.Combine(
                Path.GetTempPath(),
                "ZaidePhase223M4_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDirectory);

            WorkspaceRoot = workspaceRoot
                ?? Path.Combine(_rootDirectory, "workspace");
            Directory.CreateDirectory(WorkspaceRoot);

            ProcessCwd = processCwd ?? Path.Combine(_rootDirectory, "cwd");
            Directory.CreateDirectory(ProcessCwd);

            WorkspaceKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(WorkspaceRoot);
            LegacyCwdKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(ProcessCwd);

            Store = Phase21ContinuityTestSupport.CreateStore(_rootDirectory);
            BindingStore = new AgentActorBackendBindingStore();
            ActorId = ActorId.PanelSeed("agent-m4");
            Phase21ContinuityTestSupport.SeedBinding(BindingStore, ActorId, backendId);
            BackendId = backendId;

            Scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(WorkspaceRoot);
            Authority = new FakeWorkspaceActionAuthority(Scope) { HasWorkspace = true };

            EventStream = new AgentEventStream();
            ConversationStore = ConversationsTestSupport.CreateStore();
            Catalog = ConversationsTestSupport.CreateCatalog();
            Conversation = ConversationStore.GetOrCreateDirectConversation(
                Catalog.CanonicalHuman.Id,
                ActorId);
            ConversationId = Conversation.Id;

            Coordinator = Phase21ContinuityTestSupport.CreateCoordinator(Store, BindingStore);
            ConversationProjector = new AgentSessionContinuityConversationProjector(
                ConversationStore,
                Catalog);
            WorkspaceOpenReconciler = new AgentSessionContinuityWorkspaceOpenReconciler(
                Coordinator,
                new PathDerivedAgentDurableWorkspaceStorageKeyResolver(),
                ConversationProjector,
                Authority);
            LegacyCwdReader = new AgentSessionContinuityLegacyCwdReader(
                new AgentSessionContinuityInspector(Store),
                new PathDerivedAgentDurableWorkspaceStorageKeyResolver(),
                () => ProcessCwd);
            StartupReconciler = new AgentSessionContinuityStartupReconciler(
                Coordinator,
                new PathDerivedAgentDurableWorkspaceStorageKeyResolver(),
                LegacyCwdReader,
                ConversationProjector,
                () => ProcessCwd);

            Backend = CreateBackend(backendId);
            SessionService = CreateSessionService(Coordinator);
            Projection = new AgentConversationEventProjection(
                EventStream,
                ConversationStore,
                Catalog);
            ContinuitySubscriber = new AgentSessionContinuityEventSubscriber(
                EventStream,
                Coordinator);
        }

        public string WorkspaceRoot { get; }

        public string ProcessCwd { get; }

        public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

        public AgentDurableWorkspaceStorageKey LegacyCwdKey { get; }

        public AgentDurableRecordFileStore Store { get; }

        public AgentActorBackendBindingStore BindingStore { get; }

        public ActorId ActorId { get; }

        public AgentBackendId BackendId { get; }

        public FakeWorkspaceActionAuthority Authority { get; }

        public WorkspaceActionScope Scope { get; }

        public AgentEventStream EventStream { get; }

        public IConversationStore ConversationStore { get; }

        public IActorCatalog Catalog { get; }

        public Conversation Conversation { get; }

        public ConversationId ConversationId { get; }

        public AgentSessionContinuityCoordinator Coordinator { get; }

        public AgentSessionContinuityConversationProjector ConversationProjector { get; }

        public AgentSessionContinuityWorkspaceOpenReconciler WorkspaceOpenReconciler { get; }

        public AgentSessionContinuityLegacyCwdReader LegacyCwdReader { get; }

        public AgentSessionContinuityStartupReconciler StartupReconciler { get; }

        public FakeAgentBackend Backend { get; }

        public AgentSessionService SessionService { get; }

        public AgentConversationEventProjection Projection { get; }

        public AgentSessionContinuityEventSubscriber ContinuitySubscriber { get; }

        public AgentSessionService CreateSessionService(AgentSessionContinuityCoordinator coordinator) =>
            new(
                new IAgentBackend[] { Backend },
                EventStream,
                brokerFactory: null,
                auditStore: null,
                workspaceAuthority: Authority,
                continuityCoordinator: coordinator,
                workspaceKeyResolver: new PathDerivedAgentDurableWorkspaceStorageKeyResolver(),
                workspaceRootProvider: AgentContinuityWorkspaceRootProvider.CreateOpenedWorkspaceProvider(Authority));

        public async Task<ExecutionRunId> SendExplicitResendAsync(AgentSessionService sessionService)
        {
            var messageEntryId = ConversationEntryId.New();
            var snapshot = await sessionService.SendAsync(
                ConversationId,
                Catalog.CanonicalHuman.Id,
                ActorId,
                BackendId,
                messageEntryId,
                "explicit re-send after interruption",
                CancellationToken.None);
            return snapshot.RunId;
        }

        public AgentSessionId RecordInterruptedCheckpointAtWorkspaceRoot(
            AgentRunStatus runStatus = AgentRunStatus.Running)
        {
            var sessionId = AgentSessionId.New();
            Coordinator.RecordCheckpoint(Phase21ContinuityTestSupport.CreateInterruptedCheckpoint(
                WorkspaceKey,
                WorkspaceRoot,
                ConversationId,
                sessionId,
                ActorId,
                BackendId,
                runStatus));
            return sessionId;
        }

        public AgentSessionId RecordLegacyCwdCheckpoint(
            AgentRunStatus runStatus = AgentRunStatus.Running)
        {
            var sessionId = AgentSessionId.New();
            Coordinator.RecordCheckpoint(Phase21ContinuityTestSupport.CreateInterruptedCheckpoint(
                LegacyCwdKey,
                ProcessCwd,
                ConversationId,
                sessionId,
                ActorId,
                BackendId,
                runStatus));
            return sessionId;
        }

        public int CountInterruptedProjectionEntries() =>
            ConversationStore.TryGet(ConversationId, out var conversation)
                ? conversation.Entries.Count(e =>
                    e.Kind == ConversationEntryKind.ExecutionFailure
                    && e.Content.StartsWith(
                        AgentConversationEventProjection.InterruptedRunContentPrefix,
                        StringComparison.Ordinal))
                : 0;

        public void Dispose()
        {
            ContinuitySubscriber.Dispose();
            Projection.Dispose();
            SessionService.Dispose();
            WorkspaceOpenReconciler.Dispose();
            Phase21ContinuityTestSupport.DeleteDirectory(_rootDirectory);
        }

        private static FakeAgentBackend CreateBackend(AgentBackendId backendId)
        {
            var fake = new FakeAgentBackend(backendId);
            fake.SetCompletion("ok");
            return fake;
        }
    }
}
