using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Automation;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents.Acp.Backend;

namespace Zaide.Tests.Features.Agents.Binding;

public sealed class Phase22AcpBindingWorkflowTests
{
    [Fact]
    public async Task Workflow_BindProbeUnbind_DoesNotCreatePromptSession()
    {
        using var harness = WorkflowHarness.Create();
        var actorId = ActorId.TownhallAgent;

        var bind = harness.Presenter.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", new[] { "healthy" }),
            "acp-fake-agent",
            "phase-20-m3");
        Assert.True(bind.IsSuccess);

        var probe = await harness.Presenter.ProbeAcpAsync(actorId);
        Assert.True(probe.IsSuccess, probe.Message);
        Assert.Equal("acp-fake-agent", probe.ObservedAgentName);
        Assert.False(harness.LastClient!.ActiveSessionId is not null); // no session/new during probe

        var projection = harness.Presenter.BuildProjection(actorId);
        Assert.True(projection.IsBound);
        Assert.Equal(AgentBackendIds.Acp, projection.BackendId);
        Assert.Contains("/usr/bin/fake-agent", projection.AcpRuntimeCaption, StringComparison.Ordinal);
        Assert.Contains(
            AgentBackendBindingWorkflowProjection.AcpSecretsCaption,
            projection.SettingsCaption,
            StringComparison.Ordinal);
        Assert.DoesNotContain("token", projection.SettingsCaption, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key", projection.CapabilityCaption, StringComparison.OrdinalIgnoreCase);

        var unbind = harness.Presenter.TryUnbind(actorId);
        Assert.True(unbind.IsSuccess);
        Assert.False(harness.Store.HasBinding(actorId));
    }

    [Fact]
    public async Task Probe_FailsClosed_OnIdentityMismatch_KeepsDurableBinding()
    {
        using var harness = WorkflowHarness.Create(
            agentName: "wrong-agent",
            agentVersion: "0.0.0");
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Presenter.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        var probe = await harness.Presenter.ProbeAcpAsync(actorId);
        Assert.False(probe.IsSuccess);
        Assert.Contains("identity", probe.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.True(harness.Store.HasBinding(actorId));
        Assert.Equal(1, harness.Store.GetRevision(actorId));
    }

    [Fact]
    public async Task Probe_FailsClosed_WithoutWorkspace()
    {
        using var harness = WorkflowHarness.Create(workspaceAvailable: false);
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Presenter.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        var probe = await harness.Presenter.ProbeAcpAsync(actorId);
        Assert.False(probe.IsSuccess);
        Assert.Contains("workspace", probe.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bind_RejectsSecretsInCaptionsAndGuidance()
    {
        using var harness = WorkflowHarness.Create();
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Presenter.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", new[] { "--mode", "healthy" }),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        var projection = harness.Presenter.BuildProjection(actorId);
        Assert.Contains("owned by the ACP agent", projection.SettingsCaption, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-", projection.SettingsCaption, StringComparison.Ordinal);
        Assert.DoesNotContain("password", projection.StatusCaption, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", projection.AcpRuntimeCaption ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Panel_ExposesAcpAutomationHooks()
    {
        var panel = new AgentBackendBindingPanel();
        Assert.True(panel.BindAcpButton.Focusable);
        Assert.True(panel.ProbeAcpButton.Focusable);
        Assert.Equal("Bind ACP backend", AutomationProperties.GetName(panel.BindAcpButton));
        Assert.Equal("Probe ACP runtime configuration", AutomationProperties.GetName(panel.ProbeAcpButton));
        Assert.Equal("Authenticate ACP with advertised method", AutomationProperties.GetName(panel.AuthenticateAcpButton));
        Assert.Equal("Logout ACP authentication", AutomationProperties.GetName(panel.LogoutButton));

        panel.SetWorkflowProjection(
            "ACP",
            "Disconnected",
            isDisconnected: true,
            capabilityCaption: "Backend: ACP",
            settingsCaption: AgentBackendBindingWorkflowProjection.AcpSecretsCaption,
            mutationErrorCaption: null,
            canBindNativeHarness: true,
            canUnbind: true,
            acpRuntimeCaption: "/usr/bin/fake-agent",
            canProbeAcp: true,
            canAuthenticateAcp: false,
            canLogout: false,
            canBindAcp: false);

        Assert.True(panel.ProbeAcpButton.IsEnabled);
        Assert.True(panel.ProbeAcpButton.IsVisible);
        Assert.False(panel.BindAcpButton.IsEnabled);
    }

    [Fact]
    public void Composition_RegistersOnboardingAndWorkspaceCwd()
    {
        var modulePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "App", "Composition", "Registration", "AgentsServiceCollectionExtensions.cs"));
        Assert.True(File.Exists(modulePath), modulePath);
        var source = File.ReadAllText(modulePath);
        Assert.Contains("IAcpOnboardingConnectionService", source, StringComparison.Ordinal);
        Assert.Contains("AcpWorkspaceWorkingDirectory.CreateProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "() => Environment.CurrentDirectory",
            source,
            StringComparison.Ordinal);
    }

    private sealed class FixedWorkspaceAuthority : IWorkspaceActionAuthority
    {
        private readonly string? _root;

        public FixedWorkspaceAuthority(string? root) => _root = root;

#pragma warning disable CS0067
        public event Action? ScopeInvalidated;
#pragma warning restore CS0067

        public bool TryCaptureCurrentScope(out WorkspaceActionScope scope)
        {
            if (_root is null)
            {
                scope = null!;
                return false;
            }

            scope = new WorkspaceActionScope(
                WorkspaceIdentity.New(),
                WorkspaceGeneration.Initial,
                _root,
                capturedCanonicalRoot: _root,
                capturedRootDevice: 1,
                capturedRootInode: 1);
            return true;
        }

        public bool IsCurrent(WorkspaceActionScope scope) => true;
    }

    private sealed class ClientHolder
    {
        public AcpFakeSessionClient? LastClient { get; set; }
    }

    private sealed class WorkflowHarness : IDisposable
    {
        private readonly string _directory;
        private readonly ClientHolder _clientHolder;

        private WorkflowHarness(
            string directory,
            ClientHolder clientHolder,
            AgentActorBackendBindingStore store,
            AgentActorBackendSelectionService selection,
            AcpOnboardingConnectionService onboarding,
            AgentBackendBindingPresenter presenter)
        {
            _directory = directory;
            _clientHolder = clientHolder;
            Store = store;
            Selection = selection;
            Onboarding = onboarding;
            Presenter = presenter;
        }

        public AgentActorBackendBindingStore Store { get; }

        public AgentActorBackendSelectionService Selection { get; }

        public AcpOnboardingConnectionService Onboarding { get; }

        public AgentBackendBindingPresenter Presenter { get; }

        public AcpFakeSessionClient? LastClient => _clientHolder.LastClient;

        public static WorkflowHarness Create(
            bool workspaceAvailable = true,
            string agentName = "acp-fake-agent",
            string agentVersion = "phase-20-m3")
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "zaide-phase22-acp-workflow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var workspace = Path.Combine(directory, "workspace");
            Directory.CreateDirectory(workspace);

            var store = new AgentActorBackendBindingStore(
                Path.Combine(directory, "agent-backend-bindings.json"),
                Path.Combine(directory, "agent-backend-bindings.json.tmp"),
                Path.Combine(directory, "agent-backend-bindings.json.lastknowngood"));

            var clientHolder = new ClientHolder();
            AcpOnboardingConnectionService? onboarding = null;
            var selection = new AgentActorBackendSelectionService(store, () => onboarding);
            var authority = new FixedWorkspaceAuthority(workspaceAvailable ? workspace : null);
            onboarding = new AcpOnboardingConnectionService(
                store,
                selection,
                authority,
                activeRunQuery: null,
                clientFactory: (_, _, _) =>
                {
                    clientHolder.LastClient = new AcpFakeSessionClient(new AcpFakeSessionScript
                    {
                        AgentName = agentName,
                        AgentVersion = agentVersion,
                        AuthMethods = new[]
                        {
                            new AcpAuthMethod { Id = "oauth", Name = "OAuth" },
                        },
                    });
                    return Task.FromResult<IAcpSessionClient>(clientHolder.LastClient);
                });

            var presenter = new AgentBackendBindingPresenter(
                selection,
                store,
                optionsSource: null,
                workspaceAuthority: authority,
                onboarding: onboarding);

            return new WorkflowHarness(directory, clientHolder, store, selection, onboarding, presenter);
        }

        public void Dispose()
        {
            Onboarding.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
