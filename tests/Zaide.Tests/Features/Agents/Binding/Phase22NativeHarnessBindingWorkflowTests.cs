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

public sealed class Phase22NativeHarnessBindingWorkflowTests
{
    [Fact]
    public void Workflow_BindAndUnbind_NativeHarness_SurfacesTypedResults()
    {
        using var harness = WorkflowHarness.Create();
        var actorId = ActorId.TownhallAgent;

        var bind = harness.Presenter.TryBindNativeHarness(actorId);
        Assert.True(bind.IsSuccess);
        Assert.Equal(1, bind.Revision);
        Assert.True(harness.Store.HasBinding(actorId));
        Assert.Equal(AgentBackendIds.NativeHarness, harness.Store.GetRequiredBackendId(actorId));

        var projection = harness.Presenter.BuildProjection(actorId);
        Assert.True(projection.IsBound);
        Assert.Equal("Native Harness", projection.BackendLabel);
        Assert.True(projection.CanUnbind);
        Assert.False(projection.CanBindNativeHarness);
        Assert.Contains("provider", projection.CapabilityCaption, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Settings", projection.SettingsCaption, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", projection.CapabilityCaption, StringComparison.Ordinal);
        Assert.DoesNotContain("api_key", projection.CapabilityCaption, StringComparison.OrdinalIgnoreCase);

        var unbind = harness.Presenter.TryUnbind(actorId);
        Assert.True(unbind.IsSuccess);
        Assert.False(harness.Store.HasBinding(actorId));

        var unbound = harness.Presenter.BuildProjection(actorId);
        Assert.False(unbound.IsBound);
        Assert.True(unbound.CanBindNativeHarness);
        Assert.False(unbound.CanUnbind);
    }

    [Fact]
    public void Workflow_DoesNotUseTryBind_WhenAlreadyBound()
    {
        using var harness = WorkflowHarness.Create();
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Presenter.TryBindNativeHarness(actorId).IsSuccess);

        // Direct store re-bind must fail closed; workflow re-bind uses update.
        var directRebind = harness.Store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness));
        Assert.Equal(AgentActorBackendBindingMutationStatus.ValidationFailed, directRebind.Status);
        Assert.Contains("already bound", directRebind.Message, StringComparison.OrdinalIgnoreCase);

        var workflowRebind = harness.Presenter.TryBindNativeHarness(actorId);
        Assert.True(workflowRebind.IsSuccess);
        Assert.Equal(2, workflowRebind.Revision);
        Assert.Equal(AgentActorBackendBindingMutationKind.Update, workflowRebind.Kind);
    }

    [Fact]
    public void CapabilityProjection_Distinguishes_ConfiguredWorkspaceAndBinding()
    {
        using var harness = WorkflowHarness.Create(providerConfigured: false);
        var actorId = ActorId.TownhallAgent;

        var unbound = harness.Presenter.BuildProjection(actorId);
        Assert.False(unbound.IsBound);
        Assert.False(unbound.ProviderConfigured);
        Assert.Contains("Unbound", unbound.CapabilityCaption, StringComparison.OrdinalIgnoreCase);

        Assert.True(harness.Presenter.TryBindNativeHarness(actorId).IsSuccess);
        var boundUnconfigured = harness.Presenter.BuildProjection(actorId);
        Assert.True(boundUnconfigured.IsBound);
        Assert.False(boundUnconfigured.ProviderConfigured);
        Assert.Contains("provider not configured", boundUnconfigured.CapabilityCaption, StringComparison.Ordinal);
        Assert.Contains("workspace not captured", boundUnconfigured.CapabilityCaption, StringComparison.Ordinal);
        Assert.Contains("context-manifest absent", boundUnconfigured.CapabilityCaption, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", boundUnconfigured.CapabilityCaption, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", boundUnconfigured.CapabilityCaption, StringComparison.OrdinalIgnoreCase);

        using var configured = WorkflowHarness.Create(providerConfigured: true);
        Assert.True(configured.Presenter.TryBindNativeHarness(actorId).IsSuccess);
        var boundConfigured = configured.Presenter.BuildProjection(actorId);
        Assert.True(boundConfigured.ProviderConfigured);
        Assert.Contains("provider configured", boundConfigured.CapabilityCaption, StringComparison.Ordinal);
        // Binding alone is not entitlement or network success.
        Assert.DoesNotContain("network", boundConfigured.CapabilityCaption, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("entitlement", boundConfigured.CapabilityCaption, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BusyAndConflict_SurfaceActionableErrors()
    {
        var busy = new FixedActiveRunQuery(isBusy: false);
        using var harness = WorkflowHarness.Create(activeRunQuery: busy);
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Presenter.TryBindNativeHarness(actorId).IsSuccess);

        busy.IsBusy = true;
        var busyUnbind = harness.Presenter.TryUnbind(actorId);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Busy, busyUnbind.Status);
        var projection = harness.Presenter.BuildProjection(actorId);
        Assert.NotNull(projection.MutationErrorCaption);
        Assert.True(
            projection.MutationErrorCaption!.Contains("busy", StringComparison.OrdinalIgnoreCase)
            || projection.MutationErrorCaption.Contains("active run", StringComparison.OrdinalIgnoreCase),
            projection.MutationErrorCaption);

        busy.IsBusy = false;
        // Force conflict via stale revision path on selection.
        var conflict = harness.Selection.TryUnbind(actorId, expectedRevision: 99);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Conflict, conflict.Status);
    }

    [Fact]
    public void AdvertisedMethods_ClearOnAnySuccessfulDurableMutation()
    {
        using var harness = WorkflowHarness.Create();
        var actorId = ActorId.TownhallAgent;
        var runtime = new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>());
        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            runtime,
            "acp-fake-agent",
            "1.0.0").IsSuccess);

        harness.Selection.RecordAdvertisedAuthMethods(actorId, new[] { "oauth" });
        Assert.Equal(new[] { "oauth" }, harness.Selection.GetAdvertisedAuthMethodIds(actorId));

        // Rebind to Native via workflow update path must clear advertised methods.
        Assert.True(harness.Presenter.TryBindNativeHarness(actorId).IsSuccess);
        Assert.Empty(harness.Selection.GetAdvertisedAuthMethodIds(actorId));
    }

    [Fact]
    public void Composition_RegistersPresenterWithSelectionAndStore()
    {
        var modulePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "App", "Composition", "Registration", "AgentsServiceCollectionExtensions.cs"));
        Assert.True(File.Exists(modulePath), modulePath);
        var source = File.ReadAllText(modulePath);
        Assert.Contains("AgentBackendBindingPresenter", source, StringComparison.Ordinal);
        Assert.Contains("INativeHarnessProviderOptionsSource", source, StringComparison.Ordinal);
        Assert.Contains("IWorkspaceActionAuthority", source, StringComparison.Ordinal);

        var townhallPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "App", "Composition", "Registration", "TownhallServiceCollectionExtensions.cs"));
        Assert.True(File.Exists(townhallPath), townhallPath);
        var townhallSource = File.ReadAllText(townhallPath);
        Assert.Contains("AgentBackendBindingPresenter", townhallSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Panel_ExposesFocusableAutomationHooks()
    {
        var panel = new AgentBackendBindingPanel();
        Assert.True(panel.BindNativeHarnessButton.Focusable);
        Assert.True(panel.UnbindButton.Focusable);
        Assert.Equal(
            "Bind Native Harness backend",
            AutomationProperties.GetName(panel.BindNativeHarnessButton));
        Assert.Equal(
            "Unbind agent backend",
            AutomationProperties.GetName(panel.UnbindButton));

        panel.SetWorkflowProjection(
            backendLabel: "Native Harness",
            authStatusCaption: "Auth not required",
            isDisconnected: false,
            capabilityCaption: "provider configured · workspace not captured",
            settingsCaption: AgentBackendBindingWorkflowProjection.NativeSettingsCaption,
            mutationErrorCaption: null,
            canBindNativeHarness: false,
            canUnbind: true);

        Assert.False(panel.BindNativeHarnessButton.IsEnabled);
        Assert.True(panel.UnbindButton.IsEnabled);
    }

    [Fact]
    public void TownhallViewModel_BindUnbindCommands_UsePresenter()
    {
        using var harness = WorkflowHarness.Create(providerConfigured: true);
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var vm = new TownhallViewModel(
            new TownhallState(),
            ConversationsTestSupport.CreateCatalog(),
            store,
            host,
            new NoOpExecutionCoordinator(),
            new NoOpPolicyService(),
            new TownhallConversationUiState(),
            persistenceBridge: null,
            persistenceService: null,
            agentRouter: null,
            backendSelectionService: harness.Selection,
            backendBindingPresenter: harness.Presenter);

        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();

        Assert.True(vm.IsBackendBindingStatusVisible);
        Assert.True(vm.CanBindNativeHarness);

        vm.BindNativeHarnessCommand.Execute().Subscribe();
        Assert.True(harness.Store.HasBinding(agentId));
        Assert.Equal("Native Harness", vm.BackendBindingLabel);
        Assert.True(vm.CanUnbindBackend);
        Assert.False(vm.CanBindNativeHarness);
        Assert.Contains("Settings", vm.BackendSettingsCaption, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", vm.BackendCapabilityCaption, StringComparison.Ordinal);

        vm.UnbindBackendCommand.Execute().Subscribe();
        Assert.False(harness.Store.HasBinding(agentId));
        Assert.True(vm.CanBindNativeHarness);
    }

    private sealed class FixedActiveRunQuery : IAgentActorActiveRunQuery
    {
        public FixedActiveRunQuery(bool isBusy) => IsBusy = isBusy;

        public bool IsBusy { get; set; }

        public bool HasActiveRun(ActorId actorId) => IsBusy;
    }

    private sealed class FixedOptionsSource : INativeHarnessProviderOptionsSource
    {
        private readonly bool _configured;

        public FixedOptionsSource(bool configured) => _configured = configured;

        public AgentExecutionOptions? ResolveOptions() =>
            _configured
                ? new AgentExecutionOptions
                {
                    BaseUrl = "https://example.invalid/v1",
                    Model = "test-model",
                    ApiKey = "sk-test-not-for-display",
                }
                : new AgentExecutionOptions
                {
                    BaseUrl = string.Empty,
                    Model = string.Empty,
                    ApiKey = string.Empty,
                };
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

    private sealed class WorkflowHarness : IDisposable
    {
        private readonly string _directory;

        private WorkflowHarness(
            string directory,
            AgentActorBackendBindingStore store,
            AgentActorBackendSelectionService selection,
            AgentBackendBindingPresenter presenter)
        {
            _directory = directory;
            Store = store;
            Selection = selection;
            Presenter = presenter;
        }

        public AgentActorBackendBindingStore Store { get; }

        public AgentActorBackendSelectionService Selection { get; }

        public AgentBackendBindingPresenter Presenter { get; }

        public static WorkflowHarness Create(
            bool providerConfigured = false,
            IAgentActorActiveRunQuery? activeRunQuery = null)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "zaide-phase22-native-workflow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new AgentActorBackendBindingStore(
                Path.Combine(directory, "agent-backend-bindings.json"),
                Path.Combine(directory, "agent-backend-bindings.json.tmp"),
                Path.Combine(directory, "agent-backend-bindings.json.lastknowngood"),
                activeRunQuery);
            var selection = new AgentActorBackendSelectionService(store);
            var presenter = new AgentBackendBindingPresenter(
                selection,
                store,
                new FixedOptionsSource(providerConfigured),
                workspaceAuthority: null);
            return new WorkflowHarness(directory, store, selection, presenter);
        }

        public void Dispose()
        {
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
