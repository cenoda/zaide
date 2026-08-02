using System;
using System.IO;
using System.Linq;
using Avalonia.Automation;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Binding;

/// <summary>
/// Phase 22.2 M4: reactive Townhall controls, accessibility names/focus, and
/// both backends reachable through the shipped UI surface.
/// </summary>
public sealed class Phase22BackendBindingTownhallTests
{
    [Fact]
    public void Townhall_ReactiveBindUnbind_NativeAndAcpControls()
    {
        using var harness = TownhallHarness.Create();
        var agentId = harness.ViewModel.Agents.First(a => a.Role == "agent").ActorId;
        harness.ViewModel.OpenDirectConversationCommand.Execute(agentId).Subscribe();

        Assert.True(harness.ViewModel.IsBackendBindingStatusVisible);
        Assert.True(harness.ViewModel.CanBindNativeHarness);
        Assert.True(harness.ViewModel.CanBindAcp);

        harness.ViewModel.BindNativeHarnessCommand.Execute().Subscribe();
        Assert.Equal("Native Harness", harness.ViewModel.BackendBindingLabel);
        Assert.True(harness.ViewModel.CanUnbindBackend);
        Assert.False(harness.ViewModel.CanBindNativeHarness);
        Assert.False(harness.ViewModel.ShowAcpConfig);
        Assert.Contains("Settings", harness.ViewModel.BackendSettingsCaption, StringComparison.Ordinal);

        harness.ViewModel.UnbindBackendCommand.Execute().Subscribe();
        Assert.True(harness.ViewModel.CanBindNativeHarness);
        Assert.True(harness.ViewModel.ShowAcpConfig);

        harness.ViewModel.AcpExecutableDraft = "/usr/bin/fake-agent";
        harness.ViewModel.AcpArgumentsDraft = "healthy";
        harness.ViewModel.AcpExpectedNameDraft = "acp-fake-agent";
        harness.ViewModel.AcpExpectedVersionDraft = "phase-20-m3";
        harness.ViewModel.BindAcpCommand.Execute().Subscribe();

        Assert.Equal("ACP", harness.ViewModel.BackendBindingLabel);
        Assert.True(harness.ViewModel.CanProbeAcp);
        Assert.Contains("ACP", harness.ViewModel.BackendSettingsCaption, StringComparison.Ordinal);
        Assert.Contains(
            "owned by the ACP agent",
            harness.ViewModel.BackendSettingsCaption,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-", harness.ViewModel.BackendCapabilityCaption, StringComparison.Ordinal);
    }

    [Fact]
    public void Panel_AccessibilityNames_AndDisabledStates()
    {
        var panel = new AgentBackendBindingPanel();
        Assert.True(panel.Focusable);
        Assert.True(panel.BindNativeHarnessButton.Focusable);
        Assert.True(panel.BindAcpButton.Focusable);
        Assert.True(panel.UnbindButton.Focusable);
        Assert.True(panel.ProbeAcpButton.Focusable);
        Assert.True(panel.AuthenticateAcpButton.Focusable);
        Assert.True(panel.LogoutButton.Focusable);

        Assert.Equal("Bind Native Harness backend", AutomationProperties.GetName(panel.BindNativeHarnessButton));
        Assert.Equal("Bind ACP backend", AutomationProperties.GetName(panel.BindAcpButton));
        Assert.Equal("Unbind agent backend", AutomationProperties.GetName(panel.UnbindButton));
        Assert.Equal("Probe ACP runtime configuration", AutomationProperties.GetName(panel.ProbeAcpButton));
        Assert.Equal("Authenticate ACP with advertised method", AutomationProperties.GetName(panel.AuthenticateAcpButton));
        Assert.Equal("Logout ACP authentication", AutomationProperties.GetName(panel.LogoutButton));

        panel.SetWorkflowProjection(
            backendLabel: "Unbound",
            authStatusCaption: string.Empty,
            isDisconnected: false,
            capabilityCaption: "Unbound",
            settingsCaption: AgentBackendBindingWorkflowProjection.NativeSettingsCaption,
            mutationErrorCaption: null,
            canBindNativeHarness: true,
            canUnbind: false,
            canProbeAcp: false,
            canAuthenticateAcp: false,
            canLogout: false,
            canBindAcp: true);

        Assert.True(panel.BindNativeHarnessButton.IsEnabled);
        Assert.False(panel.UnbindButton.IsEnabled);
        Assert.False(panel.ProbeAcpButton.IsVisible);

        panel.SetWorkflowProjection(
            backendLabel: "Native Harness",
            authStatusCaption: "Auth not required",
            isDisconnected: false,
            capabilityCaption: "provider configured",
            settingsCaption: AgentBackendBindingWorkflowProjection.NativeSettingsCaption,
            mutationErrorCaption: "Cannot unbind while the actor has an active run.",
            canBindNativeHarness: false,
            canUnbind: false,
            canProbeAcp: false,
            canAuthenticateAcp: false,
            canLogout: false,
            canBindAcp: true,
            showAcpConfig: false);

        Assert.False(panel.BindNativeHarnessButton.IsEnabled);
        Assert.False(panel.UnbindButton.IsEnabled);
        Assert.False(panel.IsAcpConfigRowVisible);
    }

    [Fact]
    public void NativeBound_Projection_HidesAcpConfigRow()
    {
        using var harness = TownhallHarness.Create();
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Presenter.TryBindNativeHarness(actorId).IsSuccess);
        var projection = harness.Presenter.BuildProjection(actorId);
        Assert.False(projection.ShowAcpConfig);
        Assert.Equal(AgentBackendIds.NativeHarness, projection.BackendId);

        var panel = new AgentBackendBindingPanel();
        panel.SetWorkflowProjection(
            backendLabel: projection.BackendLabel,
            authStatusCaption: projection.AuthCaption,
            isDisconnected: projection.IsDisconnected,
            capabilityCaption: projection.CapabilityCaption,
            settingsCaption: projection.SettingsCaption,
            mutationErrorCaption: projection.MutationErrorCaption,
            canBindNativeHarness: projection.CanBindNativeHarness,
            canUnbind: projection.CanUnbind,
            canProbeAcp: projection.CanProbeAcp,
            canAuthenticateAcp: projection.CanAuthenticate,
            canLogout: projection.CanLogout,
            canBindAcp: true,
            showAcpConfig: projection.ShowAcpConfig);

        Assert.False(panel.IsAcpConfigRowVisible);

        // Unbound and ACP-bound still show config.
        Assert.True(harness.Presenter.TryUnbind(actorId).IsSuccess);
        Assert.True(harness.Presenter.BuildProjection(actorId).ShowAcpConfig);

        Assert.True(harness.Presenter.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);
        Assert.True(harness.Presenter.BuildProjection(actorId).ShowAcpConfig);
    }

    [Fact]
    public void BothBackends_ReachableThroughPresenterSurface()
    {
        using var harness = TownhallHarness.Create();
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Presenter.TryBindNativeHarness(actorId).IsSuccess);
        Assert.Equal(AgentBackendIds.NativeHarness, harness.Store.GetRequiredBackendId(actorId));

        Assert.True(harness.Presenter.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);
        Assert.Equal(AgentBackendIds.Acp, harness.Store.GetRequiredBackendId(actorId));

        var projection = harness.Presenter.BuildProjection(actorId);
        Assert.True(projection.CanProbeAcp);
        Assert.True(projection.CanUnbind);
        Assert.Contains("ACP agent", projection.SettingsCaption, StringComparison.Ordinal);
    }

    private sealed class NoOpExecutionCoordinator : IAgentExecutionCoordinator
    {
#pragma warning disable CS0067
        public event Action<ConversationId, bool>? ConversationBusyChanged;
#pragma warning restore CS0067

        public bool IsConversationBusy(ConversationId conversationId) => false;

        public System.Threading.Tasks.Task<AgentExecutionCoordinatorResult?> SendAsync(
            string panelId,
            string userMessage,
            System.Threading.CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult<AgentExecutionCoordinatorResult?>(null);
    }

    private sealed class NoOpPolicyService : IAgentContextSessionPolicyService
    {
        public AgentContextSessionPolicyState GetPolicyState(ConversationId conversationId) =>
            AgentContextSessionPolicyState.CreateApplicationDefault(
                conversationId,
                AgentSessionContextPolicyLevel.Standard);

        public bool TrySetSessionOverride(
            ConversationId conversationId,
            AgentSessionContextPolicyLevel level) =>
            false;

        public bool ClearSessionOverride(ConversationId conversationId) => false;
    }

    private sealed class TownhallHarness : IDisposable
    {
        private readonly string _directory;

        private TownhallHarness(
            string directory,
            AgentActorBackendBindingStore store,
            AgentActorBackendSelectionService selection,
            AgentBackendBindingPresenter presenter,
            TownhallViewModel viewModel)
        {
            _directory = directory;
            Store = store;
            Selection = selection;
            Presenter = presenter;
            ViewModel = viewModel;
        }

        public AgentActorBackendBindingStore Store { get; }

        public AgentActorBackendSelectionService Selection { get; }

        public AgentBackendBindingPresenter Presenter { get; }

        public TownhallViewModel ViewModel { get; }

        public static TownhallHarness Create()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "zaide-phase22-townhall-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new AgentActorBackendBindingStore(
                Path.Combine(directory, "agent-backend-bindings.json"),
                Path.Combine(directory, "agent-backend-bindings.json.tmp"),
                Path.Combine(directory, "agent-backend-bindings.json.lastknowngood"));
            var selection = new AgentActorBackendSelectionService(store);
            var presenter = new AgentBackendBindingPresenter(selection, store);
            var conversationStore = ConversationsTestSupport.CreateStore();
            var host = ConversationsTestSupport.CreatePanelHost(store: conversationStore);
            var vm = new TownhallViewModel(
                new TownhallState(),
                ConversationsTestSupport.CreateCatalog(),
                conversationStore,
                host,
                new NoOpExecutionCoordinator(),
                new NoOpPolicyService(),
                new TownhallConversationUiState(),
                persistenceBridge: null,
                persistenceService: null,
                agentRouter: null,
                backendSelectionService: selection,
                backendBindingPresenter: presenter);
            return new TownhallHarness(directory, store, selection, presenter, vm);
        }

        public void Dispose()
        {
            ViewModel.Dispose();
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
