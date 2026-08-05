using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Architecture;
using Zaide.Tests.Features.Agents.Infrastructure;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 17 M9 — adversarial closeout inventory, shutdown revocation,
/// non-deletion verification, and permission-review accessibility/layout ratchets.
/// </summary>
public sealed class Phase17AdversarialCloseoutTests : IDisposable
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    private readonly string _root;
    private readonly WorkspaceActionScope _scope;
    private readonly FakeWorkspaceActionAuthority _workspaceAuthority;
    private readonly AgentActionAuditStore _auditStore;
    private readonly AgentEventStream _eventStream;
    private readonly List<AgentEvent> _capturedEvents = new();
    private readonly FakeActionRequesterBackend _backend;
    private readonly AgentSessionService _session;
    private readonly ConversationStore _conversationStore;

    public Phase17AdversarialCloseoutTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "zaide-p17-m9-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "note.txt"), "hello");

        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_root);
        _workspaceAuthority = new FakeWorkspaceActionAuthority(_scope);
        _auditStore = new AgentActionAuditStore();
        _eventStream = new AgentEventStream();
        _eventStream.Events.Subscribe(_capturedEvents.Add);

        var brokerFactory = new AgentActionBrokerFactory(
            _workspaceAuthority,
            new WorkspaceFileReader(),
            new WorkspaceFileMutator(),
            new DefaultAgentCommandResolver(),
            new WorkspaceCommandExecutor(),
            new AllowingPermissionReviewService(),
            NullAgentDocumentReconciler.Instance);

        _backend = new FakeActionRequesterBackend();
        _session = new AgentSessionService(
            new IAgentBackend[] { _backend },
            _eventStream,
            brokerFactory,
            _auditStore,
            _workspaceAuthority);

        _conversationStore = Conversations.ConversationsTestSupport.CreateStore();
        _ = new AgentConversationEventProjection(_eventStream.Events, _conversationStore, Conversations.ConversationsTestSupport.CreateCatalog());
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    public static IEnumerable<object[]> AdversarialCoverageCases =>
        new List<object[]>
        {
            Row("path traversal", "Phase17WorkspaceReadFileReaderTests", "Normalize_AbsolutePath_IsRejectedAtBoundary"),
            Row("symlink escape", "Phase17WorkspaceReadFileReaderTests", "Read_FileSymlinkEscapingWorkspace_ReturnsPathEscaped"),
            Row("TOCTOU retarget", "Phase17WorkspaceReadFileReaderTests", "Read_SymlinkRetargetedBetweenValidationAndOpen_ReturnsPathEscaped"),
            Row("duplicate replay", "Phase17ActionContractsBrokerTests", "ContractAgentActionBroker_ReplaysDuplicateCorrelationKey"),
            Row("cancellation", "Phase17PermissionLifecycleTests", "CancellationDuringReview_ReturnsCancelled"),
            Row("workspace switch", "Phase17SessionEventIntegrationTests", "WorkspaceInvalidation_RevokesActiveRunBroker"),
            Row("redaction", "Phase17ActionContractsPolicyTests", "AgentActionAuditSummary_RedactsSecretsAndBoundsText"),
            Row("process tree", "Phase17CommandExecutionTests", "Executor_CancellationTerminatesProcessTree"),
        };

    [Theory]
    [MemberData(nameof(AdversarialCoverageCases))]
    public void AdversarialCoverage_RequiredRegressionTestExists(
        string category,
        string typeName,
        string methodName)
    {
        var assembly = typeof(Phase17AdversarialCloseoutTests).Assembly;
        var type = assembly.GetTypes().SingleOrDefault(candidate =>
            candidate.Name == typeName && candidate.Namespace!.StartsWith("Zaide.Tests", StringComparison.Ordinal));

        Assert.NotNull(type);
        var method = type!.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            ?? type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        Assert.False(string.IsNullOrWhiteSpace(category));
    }

    [Fact]
    public async Task SessionDisposeDuringPendingAction_RevokesPendingAuthority()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockingReview = new BlockingPermissionReviewService(entered);
        var brokerFactory = new AgentActionBrokerFactory(
            _workspaceAuthority,
            new WorkspaceFileReader(),
            new WorkspaceFileMutator(),
            new DefaultAgentCommandResolver(),
            new WorkspaceCommandExecutor(),
            blockingReview,
            NullAgentDocumentReconciler.Instance);

        var stream = new AgentEventStream();
        var captured = new List<AgentEvent>();
        stream.Events.Subscribe(captured.Add);
        var backend = new FakeActionRequesterBackend();
        backend.SetDelayedAction(
            TimeSpan.Zero,
            (broker, token) => broker.RequestAsync(
                new AgentCreateFileActionPayload(
                    AgentWorkspaceRelativePath.Normalize("new.txt"),
                    "created"),
                correlationKey: null,
                token),
            assistantText: "late");

        var session = new AgentSessionService(
            new IAgentBackend[] { backend },
            stream,
            brokerFactory,
            _auditStore,
            _workspaceAuthority);

        var conversation = _conversationStore.CreateDirectConversation(ActorId.HumanUser, ActorId.TownhallAgent);
        var conversationId = conversation.Id;

        var sendTask = session.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            backend.BackendId,
            ConversationEntryId.New(),
            "shutdown",
            CancellationToken.None);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        session.Dispose();
        var snapshot = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotEqual(AgentRunStatus.Running, snapshot.Status);
        Assert.Contains(
            captured,
            e => e.Kind == AgentEventKind.ActionResultReported
                 && e.Payload is AgentActionFactPayload fact
                 && (fact.ResultKind == AgentActionResultKind.Cancelled
                     || fact.ResultKind == AgentActionResultKind.Revoked
                     || fact.ResultKind == AgentActionResultKind.Failed));
    }

    [Fact]
    public void Phase15SessionEventFoundation_ProductionTypesPreserved()
    {
        var requiredRelativePaths = new[]
        {
            "src/Features/Agents/Application/AgentSessionService.cs",
            "src/Features/Agents/Application/AgentEventStream.cs",
            "src/Features/Agents/Application/AgentConversationEventProjection.cs",
            "src/Features/Agents/Contracts/IAgentBackend.cs",
            "src/Features/Agents/Domain/AgentEvent.cs",
            "src/Features/Agents/Domain/AgentEventKind.cs",
            "src/Features/Agents/Domain/AgentCapabilitySnapshot.cs",
        };

        foreach (var relativePath in requiredRelativePaths)
        {
            Assert.True(
                File.Exists(Path.Combine(RepositoryRoot, relativePath)),
                $"Missing Phase 15 foundation file: {relativePath}");
        }

        Assert.True(typeof(AgentSessionService).IsPublic == false || typeof(AgentSessionService).IsNotPublic);
        Assert.True(typeof(AgentEventStream).IsPublic == false || typeof(AgentEventStream).IsNotPublic);
    }

    [Fact]
    public void LegacyOpenAiCompatibleBackend_PathPreserved()
    {
        var relativePath = "src/Features/Agents/Infrastructure/LegacyOpenAiCompatibleAgentBackend.cs";
        Assert.True(File.Exists(Path.Combine(RepositoryRoot, relativePath)));

        var executionServicePath = "src/Features/Agents/Infrastructure/AgentExecutionService.cs";
        Assert.True(File.Exists(Path.Combine(RepositoryRoot, executionServicePath)));

        var executionContractPath = "src/Features/Agents/Contracts/IAgentExecutionService.cs";
        Assert.True(File.Exists(Path.Combine(RepositoryRoot, executionContractPath)));

        var registrationSource = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs"));
        Assert.DoesNotContain("LegacyOpenAiCompatibleAgentBackend", registrationSource, StringComparison.Ordinal);
        Assert.Contains("NativeHarnessAgentBackend", registrationSource, StringComparison.Ordinal);
        Assert.Contains("AgentExecutionService", registrationSource, StringComparison.Ordinal);

        Assert.True(typeof(LegacyOpenAiCompatibleAgentBackend).IsAssignableTo(typeof(IAgentBackend)));
        Assert.Contains(typeof(IAgentExecutionService), typeof(AgentExecutionService).GetInterfaces());

        var legacyTestType = typeof(LegacyOpenAiCompatibleAgentBackendTests);
        var capabilityTest = legacyTestType.GetMethod(
            "CapabilitySnapshot_WhenConfigured_ReportsMessageCompletionUsable",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(capabilityTest);
    }

    [Fact]
    public void ApplicationShutdown_RevokesAgentSessionBeforeExit()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "src/App/Composition/ApplicationShutdown.cs"));
        Assert.Contains("Revoke pending action authority before process exit.", source, StringComparison.Ordinal);
        Assert.Contains("DisposeOwner(services.GetService<IAgentSessionService>());", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PermissionReviewDialog_Axaml_PrimaryControlsHaveAccessibleNames()
    {
        var axaml = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Presentation/PermissionReviewDialog.axaml"));

        Assert.Contains("AutomationProperties.Name=\"Allow Action\"", axaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Deny Action\"", axaml, StringComparison.Ordinal);
        Assert.Contains("IsCancel=\"True\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Focusable=\"True\"", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void PermissionReviewDialog_CodeBehind_InitialFocusOnDeny()
    {
        var codeBehind = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Presentation/PermissionReviewDialog.axaml.cs"));

        Assert.Contains("DenyButton.Focus(NavigationMethod.Tab)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ResolveDismiss()", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void PermissionReviewDialog_Axaml_SupportsResizableNarrowAndWideLayout()
    {
        var axaml = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Presentation/PermissionReviewDialog.axaml"));

        Assert.Contains("CanResize=\"True\"", axaml, StringComparison.Ordinal);
        Assert.Contains("<ScrollViewer", axaml, StringComparison.Ordinal);
        Assert.Matches(
            new Regex(@"TextWrapping\s*=\s*""Wrap""", RegexOptions.CultureInvariant),
            axaml);
    }

    [Fact]
    public void ArchitectureInventory_Phase17Closeout_HasNoUnexplainedWeakening()
    {
        var inventory = new ArchitectureInventoryReader().Read();

        Assert.Equal(ArchitectureInventoryReader.M0TotalTopLevelTypes, inventory.TotalTopLevelTypeCount);
        // Phase 22.4 M1–M3: +7 authorized transparency surface production source files.
        Assert.Equal(884, inventory.SourceFiles.Count);
        Assert.Equal(838, inventory.SourceFiles.Count(f => f.TechnicalFolder == "Features"));
        Assert.Empty(ArchitectureRatchet.DetectRootFolderAdmissionViolations(inventory));
        Assert.Empty(ArchitectureVisibilityRatchet.DetectExpandedRootFolderAdmissionViolations(inventory));
    }

    private static object[] Row(string category, string typeName, string methodName) =>
        new object[] { category, typeName, methodName };

    private sealed class AllowingPermissionReviewService : IAgentPermissionReviewService
    {
        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                request.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                isAllow: true));
    }

    private sealed class BlockingPermissionReviewService : IAgentPermissionReviewService
    {
        private readonly TaskCompletionSource _entered;

        public BlockingPermissionReviewService(TaskCompletionSource entered)
        {
            _entered = entered;
        }

        public async ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Permission review should have been cancelled.");
        }
    }
}
