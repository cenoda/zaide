using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents.Acp.Backend;

namespace Zaide.Tests.Features.Agents.Binding;

public sealed class Phase22AcpAuthenticationBridgeTests
{
    [Fact]
    public async Task Selection_RequestAuthenticate_UsesOnboardingBridge_NotLocalRewriteAlone()
    {
        using var harness = BridgeHarness.Create();
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        var probe = await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None);
        Assert.True(probe.IsSuccess, probe.Message);
        Assert.Contains("oauth", probe.AuthMethodIds);

        await harness.Selection.RequestAuthenticateAsync(actorId, "oauth", CancellationToken.None);

        Assert.NotNull(harness.LastClient);
        Assert.Equal(1, harness.LastClient!.AuthenticateCallCount);
        Assert.Equal("oauth", harness.LastClient.LastAuthenticateMethodId);
        var snapshot = harness.Selection.GetSnapshot(actorId);
        Assert.Equal(AgentAuthenticationConnectionState.Authenticated, snapshot.AuthenticationState);
        Assert.DoesNotContain("secret", snapshot.StatusCaption, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Authenticate_Failure_SurfacesFailedState_WithoutDurableAuth()
    {
        using var harness = BridgeHarness.Create(authenticateShouldFail: true);
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        Assert.True((await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
        var auth = await harness.Onboarding.AuthenticateAsync(actorId, "oauth", CancellationToken.None);
        Assert.False(auth.IsSuccess);
        Assert.NotNull(auth.Message);
        Assert.DoesNotContain("api_key=", auth.Message!, StringComparison.OrdinalIgnoreCase);

        Assert.True(harness.Store.TryGetBinding(actorId, out var binding));
        Assert.Equal(AgentAuthenticationConnectionState.Failed, binding.AuthenticationState);
        Assert.Equal(1, binding.Revision);
    }

    [Fact]
    public async Task Logout_IsCapabilityGated_AndClearsRuntimeOnly()
    {
        using var harness = BridgeHarness.Create();
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        Assert.True((await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
        Assert.True(harness.Onboarding.IsLogoutSupported(actorId));
        Assert.True((await harness.Onboarding.AuthenticateAsync(actorId, "oauth", CancellationToken.None)).IsSuccess);

        var logout = await harness.Onboarding.LogoutAsync(actorId, CancellationToken.None);
        Assert.True(logout.IsSuccess, logout.Message);
        Assert.Equal(1, harness.LastClient!.LogoutCallCount);

        Assert.True(harness.Store.HasBinding(actorId));
        var snapshot = harness.Selection.GetSnapshot(actorId);
        Assert.Equal(AgentAuthenticationConnectionState.Disconnected, snapshot.AuthenticationState);
        Assert.Empty(harness.Selection.GetAdvertisedAuthMethodIds(actorId));
    }

    [Fact]
    public async Task Logout_Rejected_WhenNotAdvertised()
    {
        using var harness = BridgeHarness.Create(authMethods: Array.Empty<AcpAuthMethod>());
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        Assert.True((await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
        Assert.False(harness.Onboarding.IsLogoutSupported(actorId));
        var logout = await harness.Onboarding.LogoutAsync(actorId, CancellationToken.None);
        Assert.False(logout.IsSuccess);
        Assert.Contains("not advertised", logout.Message!, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixedWorkspaceAuthority : IWorkspaceActionAuthority
    {
        private readonly string _root;

        public FixedWorkspaceAuthority(string root) => _root = root;

#pragma warning disable CS0067
        public event Action? ScopeInvalidated;
#pragma warning restore CS0067

        public bool TryCaptureCurrentScope(out WorkspaceActionScope scope)
        {
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

    private sealed class BridgeHarness : IDisposable
    {
        private readonly string _directory;
        private readonly ClientHolder _clientHolder;

        private BridgeHarness(
            string directory,
            ClientHolder clientHolder,
            AgentActorBackendBindingStore store,
            AgentActorBackendSelectionService selection,
            AcpOnboardingConnectionService onboarding)
        {
            _directory = directory;
            _clientHolder = clientHolder;
            Store = store;
            Selection = selection;
            Onboarding = onboarding;
        }

        public AgentActorBackendBindingStore Store { get; }

        public AgentActorBackendSelectionService Selection { get; }

        public AcpOnboardingConnectionService Onboarding { get; }

        public AcpFakeSessionClient? LastClient => _clientHolder.LastClient;

        public static BridgeHarness Create(
            bool authenticateShouldFail = false,
            IReadOnlyList<AcpAuthMethod>? authMethods = null)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "zaide-phase22-acp-bridge-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var workspace = Path.Combine(directory, "workspace");
            Directory.CreateDirectory(workspace);

            var store = new AgentActorBackendBindingStore(
                Path.Combine(directory, "agent-backend-bindings.json"),
                Path.Combine(directory, "agent-backend-bindings.json.tmp"),
                Path.Combine(directory, "agent-backend-bindings.json.lastknowngood"));

            var clientHolder = new ClientHolder();
            AcpOnboardingConnectionService? onboarding = null;
            var selection = new AgentActorBackendSelectionService(
                store,
                () => onboarding);

            var methods = authMethods ?? new[]
            {
                new AcpAuthMethod { Id = "oauth", Name = "OAuth" },
            };

            onboarding = new AcpOnboardingConnectionService(
                store,
                selection,
                new FixedWorkspaceAuthority(workspace),
                activeRunQuery: null,
                clientFactory: (_, _, _) =>
                {
                    clientHolder.LastClient = new AcpFakeSessionClient(new AcpFakeSessionScript
                    {
                        AgentName = "acp-fake-agent",
                        AgentVersion = "phase-20-m3",
                        AuthMethods = methods,
                        AuthenticateShouldFail = authenticateShouldFail,
                    });
                    return Task.FromResult<IAcpSessionClient>(clientHolder.LastClient);
                });

            return new BridgeHarness(directory, clientHolder, store, selection, onboarding);
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
