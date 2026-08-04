using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Agents.Memory.Store;

namespace Zaide.Tests.Features.Agents.Transparency.Memory;

public sealed class Phase22MemorySurfaceTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly ServiceProvider _provider;

    public Phase22MemorySurfaceTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "Phase22MemorySurface_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);

        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.RemoveAll<IWorkspaceActionAuthority>();
        services.AddSingleton<IWorkspaceActionAuthority>(new FakeWorkspaceActionAuthority(
            FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot)));
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task MemorySurface_IsReachableAndUsesOpenedWorkspace()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        Assert.False(management.IsMemoryPanelOpen);

        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();
        Assert.True(management.IsMemoryPanelOpen);
        Assert.Equal(AgentMemorySurfaceState.Empty, management.MemoryInspection.SurfaceState);
        Assert.NotNull(management.MemoryInspection.Summary);
        Assert.NotEqual(
            PathDerivedAgentDurableWorkspaceStorageKeyResolver.UnboundWorkspaceKey,
            management.MemoryInspection.Summary!.WorkspaceKey.Value);

        management.CloseMemoryCommand.Execute().Subscribe();
        Assert.False(management.IsMemoryPanelOpen);
    }

    [Fact]
    public void MemorySurface_CommandRegistrationUsesAgentCategoryAndNoGesture()
    {
        var registry = _provider.GetRequiredService<ICommandRegistry>();
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        AgentTransparencyCommandRegistration.Register(registry, management);

        var descriptor = Assert.Single(registry.GetAll(), command => command.Id == "agent.memory.open");
        Assert.Equal("Open Agent Memory", descriptor.DisplayName);
        Assert.Equal("Agent", descriptor.Category);
        Assert.Empty(descriptor.DefaultGestures);
    }

    [Fact]
    public void Townhall_ReceivesTheProductionTransparencyManagementOwner()
    {
        var townhall = _provider.GetRequiredService<TownhallViewModel>();
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();

        Assert.Same(management, townhall.TransparencyManagement);
    }

    [Fact]
    public async Task MemorySurface_LifecycleCrudRoutesThroughCoordinatorWithUserProvenance()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var catalog = _provider.GetRequiredService<IActorCatalog>();
        var inspection = management.MemoryInspection;

        await management.BindMemoryTownhallContextAsync(
            new AgentMemoryInspectionViewModel.TownhallContext(
                conversationId: ConversationId.ForChannel("general"),
                agentActorId: ActorId.TownhallAgent,
                sessionId: "session:phase22-memory",
                projectId: null));

        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Empty, inspection.SurfaceState);

        inspection.SelectedScope = AgentMemoryScope.ProjectShared;
        inspection.DraftContent = "M2 lifecycle fact";
        var created = management.CreateMemoryFromDraft();
        Assert.Equal(AgentMemoryOperationStatus.Accepted, created.Status);
        Assert.Equal(AgentMemorySurfaceState.Ready, inspection.SurfaceState);
        Assert.NotNull(inspection.SelectedRecord);
        Assert.Equal(AgentMemorySourceKind.User, inspection.SelectedRecord!.Provenance.SourceKind);
        Assert.Equal(catalog.CanonicalHuman.Id, inspection.SelectedRecord.Provenance.AuthorActorId);
        Assert.Equal(AgentMemoryScope.ProjectShared, inspection.SelectedRecord.ScopeTarget.Scope);
        Assert.Equal(AgentMemoryStatus.Active, inspection.SelectedRecord.Status);
        Assert.Equal(AgentMemoryConflictKind.None, inspection.SelectedRecord.ConflictKind);

        var corrected = management.CorrectSelectedMemory("M2 corrected fact");
        Assert.Equal(AgentMemoryOperationStatus.Accepted, corrected.Status);
        Assert.Equal("M2 corrected fact", inspection.SelectedRecord!.Content);
        Assert.Equal(AgentMemorySourceKind.User, inspection.SelectedRecord.Provenance.SourceKind);

        var disabled = management.DisableSelectedMemory();
        Assert.Equal(AgentMemoryOperationStatus.Accepted, disabled.Status);
        Assert.Equal(AgentMemoryStatus.Disabled, inspection.SelectedRecord!.Status);

        // Supersede replacement becomes the selected active record.
        inspection.SelectRecord(inspection.Records.Single(r => r.Status == AgentMemoryStatus.Disabled).MemoryId);
        var superseded = management.SupersedeSelectedMemory("M2 replacement fact");
        Assert.Equal(AgentMemoryOperationStatus.Accepted, superseded.Status);
        Assert.Contains(inspection.Records, r => r.Status == AgentMemoryStatus.Superseded);
        Assert.Contains(inspection.Records, r =>
            r.Status == AgentMemoryStatus.Active && r.Content == "M2 replacement fact");

        var active = inspection.Records.Single(r => r.Status == AgentMemoryStatus.Active);
        management.SelectMemoryRecord(active.MemoryId);
        var deleted = management.DeleteSelectedMemory();
        Assert.Equal(AgentMemoryOperationStatus.Accepted, deleted.Status);
        Assert.DoesNotContain(inspection.Records, r => r.MemoryId.Equals(active.MemoryId));
    }

    [Fact]
    public async Task MemorySurface_MissingRequiredScopeDisablesCreateWithVisibleReason()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var inspection = management.MemoryInspection;

        await management.BindMemoryTownhallContextAsync(AgentMemoryInspectionViewModel.TownhallContext.Empty);
        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();

        inspection.DraftContent = "Needs scope context";
        inspection.SelectedScope = AgentMemoryScope.Session;
        Assert.False(inspection.CanSubmitCreate);
        Assert.Contains("Session scope", inspection.SubmitDenialReason, StringComparison.Ordinal);

        var denied = management.CreateMemoryFromDraft();
        Assert.Equal(AgentMemoryOperationStatus.InvalidRequest, denied.Status);
        Assert.Contains("Session scope", denied.Reason, StringComparison.Ordinal);

        inspection.SelectedScope = AgentMemoryScope.Agent;
        Assert.False(inspection.CanSubmitCreate);
        Assert.Contains("Agent scope", inspection.SubmitDenialReason, StringComparison.Ordinal);

        inspection.SelectedScope = AgentMemoryScope.Conversation;
        Assert.False(inspection.CanSubmitCreate);
        Assert.Contains("Conversation scope", inspection.SubmitDenialReason, StringComparison.Ordinal);

        // Project/shared only needs the opened workspace identity.
        inspection.SelectedScope = AgentMemoryScope.ProjectShared;
        Assert.True(inspection.CanSubmitCreate);
        Assert.Null(inspection.SubmitDenialReason);
    }

    [Fact]
    public async Task MemorySurface_InfluencePayloadsAreNotEditableLifecycleRecords()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var influence = _provider.GetRequiredService<AgentMemoryInfluenceRecorder>();
        var inspection = management.MemoryInspection;

        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();
        inspection.SelectedScope = AgentMemoryScope.ProjectShared;
        inspection.DraftContent = "Editable lifecycle memory";
        var created = management.CreateMemoryFromDraft();
        Assert.Equal(AgentMemoryOperationStatus.Accepted, created.Status);

        var workspaceKey = inspection.Summary!.WorkspaceKey;
        influence.RecordInfluence(
            workspaceKey,
            ExecutionRunId.New(),
            AgentSessionId.New(),
            AgentMemoryInfluenceState.Recorded,
            Array.Empty<AgentMemoryInfluenceRevision>());

        await management.RefreshMemorySurfaceAsync();

        Assert.Equal(AgentMemorySurfaceState.Ready, inspection.SurfaceState);
        Assert.All(inspection.Records, record =>
        {
            Assert.False(string.IsNullOrWhiteSpace(record.MemoryId.Value));
            Assert.NotEqual(default, record.Provenance.AuthorActorId);
        });
        Assert.Single(inspection.Records);
        Assert.Equal(created.MemoryId, inspection.Records[0].MemoryId);
        Assert.Contains("not editable", inspection.InfluenceEvidenceCaption, StringComparison.OrdinalIgnoreCase);

        // Store has both lifecycle and influence appends, but projection keeps them separate.
        var store = _provider.GetRequiredService<IAgentDurableRecordStore>();
        var replay = store.Replay(new AgentDurableRecordReplayRequest(
            workspaceKey,
            AgentDurableRecordClass.Memory,
            afterOrderingSequence: 0,
            maxRecords: 64));
        Assert.True(replay.Records.Count >= 2);
    }

    [Fact]
    public async Task MemorySurface_EmptyIsDistinctFromFailedAndUnavailable()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var authority = (FakeWorkspaceActionAuthority)_provider.GetRequiredService<IWorkspaceActionAuthority>();
        var inspection = management.MemoryInspection;

        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Empty, inspection.SurfaceState);
        Assert.Null(inspection.FailureReason);
        Assert.Contains("No durable memory", inspection.StatusCaption, StringComparison.Ordinal);

        authority.HasWorkspace = false;
        await management.RefreshMemorySurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Unavailable, inspection.SurfaceState);
        Assert.NotEqual(AgentMemorySurfaceState.Empty, inspection.SurfaceState);
        Assert.Contains("workspace", inspection.StatusCaption, StringComparison.OrdinalIgnoreCase);
        Assert.Null(inspection.SelectedRecord);

        authority.HasWorkspace = true;
        await management.RefreshMemorySurfaceAsync();
        Assert.Equal(AgentMemorySurfaceState.Empty, inspection.SurfaceState);
    }

    [Fact]
    public void MemorySurface_FailedLoadNeverLooksLikeEmpty()
    {
        var (rootDirectory, _, workspaceRoot) = Phase21MemoryTestSupport.CreateWorkspaceFixture();
        try
        {
            var store = Phase21MemoryTestSupport.CreateStore(rootDirectory);
            var coordinator = Phase21MemoryTestSupport.CreateCoordinator(store);
            var availability = new AgentMemoryAvailabilityProjection(
                coordinator,
                () => workspaceRoot);
            var catalog = _provider.GetRequiredService<IActorCatalog>();
            var authority = new FakeWorkspaceActionAuthority(
                FakeWorkspaceActionAuthority.CreateScopeFromDirectory(workspaceRoot));
            var inspection = new AgentMemoryInspectionViewModel(
                coordinator,
                availability,
                catalog,
                authority);

            store.Dispose();

            inspection.ReloadNow();

            Assert.Equal(AgentMemorySurfaceState.Failed, inspection.SurfaceState);
            Assert.NotEqual(AgentMemorySurfaceState.Empty, inspection.SurfaceState);
            Assert.False(
                string.Equals(
                    inspection.StatusCaption,
                    "No durable memory records for the opened workspace.",
                    StringComparison.Ordinal));
            Assert.NotNull(inspection.FailureReason);
            Assert.True(inspection.CanRetry);
            Assert.Null(inspection.SelectedRecord);
            Assert.Empty(inspection.Records);
        }
        finally
        {
            Phase21MemoryTestSupport.DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task MemorySurface_ContextSwitchClearsSelection()
    {
        var management = _provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        var inspection = management.MemoryInspection;

        await management.BindMemoryTownhallContextAsync(
            new AgentMemoryInspectionViewModel.TownhallContext(
                conversationId: ConversationId.ForChannel("general"),
                agentActorId: ActorId.TownhallAgent,
                sessionId: null,
                projectId: null));
        management.OpenMemoryCommand.Execute().Subscribe();
        await management.RefreshMemorySurfaceAsync();

        inspection.SelectedScope = AgentMemoryScope.ProjectShared;
        inspection.DraftContent = "Selection hygiene";
        Assert.Equal(AgentMemoryOperationStatus.Accepted, management.CreateMemoryFromDraft().Status);
        Assert.NotNull(inspection.SelectedRecord);

        await management.BindMemoryTownhallContextAsync(
            new AgentMemoryInspectionViewModel.TownhallContext(
                conversationId: ConversationId.ForChannel("random"),
                agentActorId: ActorId.PanelSeed("other"),
                sessionId: null,
                projectId: null));

        Assert.Null(inspection.SelectedRecord);
    }

    public void Dispose()
    {
        _provider.Dispose();
        try
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
        catch
        {
        }
    }
}
