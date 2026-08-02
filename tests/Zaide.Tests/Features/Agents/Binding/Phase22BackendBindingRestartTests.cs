using System;
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

/// <summary>
/// Phase 22.2 M4: restart rehydrates durable bindings only; never restores
/// authenticated/runtime zombies; unbind sticks across restart.
/// </summary>
public sealed class Phase22BackendBindingRestartTests
{
    [Fact]
    public void Restart_RehydratesDurableBinding_WithoutRuntimeAuth()
    {
        using var profile = TempProfile.Create();
        var actorId = ActorId.TownhallAgent;

        using (var first = BindingProcess.Create(profile))
        {
            Assert.True(first.Selection.TryBindNativeHarness(actorId).IsSuccess);
            first.Store.SetRuntimeAuthentication(
                actorId,
                selectedAuthMethodId: "should-not-persist",
                AgentAuthenticationConnectionState.Authenticated);
            first.Selection.RecordAdvertisedAuthMethods(actorId, new[] { "should-not-persist" });
            Assert.Equal(
                AgentAuthenticationConnectionState.Authenticated,
                first.Selection.GetSnapshot(actorId).AuthenticationState);
        }

        using var second = BindingProcess.Create(profile);
        Assert.True(second.Store.HasBinding(actorId));
        Assert.Equal(AgentBackendIds.NativeHarness, second.Store.GetRequiredBackendId(actorId));
        Assert.True(second.Store.TryGetBinding(actorId, out var binding));
        // Durable rehydrate never restores authenticated/runtime method state.
        Assert.Null(binding.SelectedAuthMethodId);
        Assert.Equal(AgentAuthenticationConnectionState.NotRequired, binding.AuthenticationState);
        Assert.Empty(second.Selection.GetAdvertisedAuthMethodIds(actorId));
        var snapshot = second.Selection.GetSnapshot(actorId);
        Assert.True(snapshot.IsBound);
        Assert.NotEqual(AgentAuthenticationConnectionState.Authenticated, snapshot.AuthenticationState);
    }

    [Fact]
    public async Task Restart_Acp_DoesNotRestoreAuthenticatedOrMethods()
    {
        using var profile = TempProfile.Create();
        var actorId = ActorId.TownhallAgent;

        using (var first = BindingProcess.Create(profile, withOnboarding: true))
        {
            Assert.True(first.Selection.TryBindAcpRuntime(
                actorId,
                new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
                "acp-fake-agent",
                "phase-20-m3").IsSuccess);

            Assert.True((await first.Onboarding!.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
            Assert.True((await first.Onboarding.AuthenticateAsync(actorId, "oauth", CancellationToken.None)).IsSuccess);
            Assert.Equal(
                AgentAuthenticationConnectionState.Authenticated,
                first.Selection.GetSnapshot(actorId).AuthenticationState);
            Assert.NotEmpty(first.Selection.GetAdvertisedAuthMethodIds(actorId));
        }

        using var second = BindingProcess.Create(profile, withOnboarding: true);
        Assert.True(second.Store.HasBinding(actorId));
        Assert.Equal(AgentBackendIds.Acp, second.Store.GetRequiredBackendId(actorId));
        Assert.True(second.Store.TryGetBinding(actorId, out var binding));
        Assert.Null(binding.SelectedAuthMethodId);
        Assert.Equal(AgentAuthenticationConnectionState.Disconnected, binding.AuthenticationState);
        Assert.Empty(second.Selection.GetAdvertisedAuthMethodIds(actorId));
        Assert.False(second.Onboarding!.IsLogoutSupported(actorId));
    }

    [Fact]
    public void Unbind_SticksAcrossRestart()
    {
        using var profile = TempProfile.Create();
        var actorId = ActorId.TownhallAgent;

        using (var first = BindingProcess.Create(profile))
        {
            Assert.True(first.Selection.TryBindNativeHarness(actorId).IsSuccess);
            Assert.True(first.Selection.TryUnbind(actorId, expectedRevision: 1).IsSuccess);
            Assert.False(first.Store.HasBinding(actorId));
        }

        using var second = BindingProcess.Create(profile);
        Assert.False(second.Store.HasBinding(actorId));
        Assert.False(second.Selection.GetSnapshot(actorId).IsBound);
        Assert.Equal("Unbound", second.Selection.GetSnapshot(actorId).BackendLabel);
    }

    private sealed class TempProfile : IDisposable
    {
        private TempProfile(string root)
        {
            Root = root;
            Primary = Path.Combine(root, "agent-backend-bindings.json");
            Temp = Path.Combine(root, "agent-backend-bindings.json.tmp");
            Lkg = Path.Combine(root, "agent-backend-bindings.json.lastknowngood");
            Workspace = Path.Combine(root, "workspace");
            Directory.CreateDirectory(Workspace);
        }

        public string Root { get; }

        public string Primary { get; }

        public string Temp { get; }

        public string Lkg { get; }

        public string Workspace { get; }

        public static TempProfile Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "zaide-phase22-restart-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempProfile(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
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

    private sealed class BindingProcess : IDisposable
    {
        private BindingProcess(
            AgentActorBackendBindingStore store,
            AgentActorBackendSelectionService selection,
            AcpOnboardingConnectionService? onboarding)
        {
            Store = store;
            Selection = selection;
            Onboarding = onboarding;
        }

        public AgentActorBackendBindingStore Store { get; }

        public AgentActorBackendSelectionService Selection { get; }

        public AcpOnboardingConnectionService? Onboarding { get; }

        public static BindingProcess Create(TempProfile profile, bool withOnboarding = false)
        {
            var store = new AgentActorBackendBindingStore(
                profile.Primary,
                profile.Temp,
                profile.Lkg);

            AcpOnboardingConnectionService? onboarding = null;
            var selection = new AgentActorBackendSelectionService(
                store,
                () => onboarding);

            if (withOnboarding)
            {
                onboarding = new AcpOnboardingConnectionService(
                    store,
                    selection,
                    new FixedWorkspaceAuthority(profile.Workspace),
                    activeRunQuery: null,
                    clientFactory: (_, _, _) => Task.FromResult<IAcpSessionClient>(
                        new AcpFakeSessionClient(new AcpFakeSessionScript
                        {
                            AgentName = "acp-fake-agent",
                            AgentVersion = "phase-20-m3",
                            AuthMethods = new[]
                            {
                                new AcpAuthMethod { Id = "oauth", Name = "OAuth" },
                            },
                        })));
            }

            return new BindingProcess(store, selection, onboarding);
        }

        public void Dispose()
        {
            if (Onboarding is not null)
            {
                Onboarding.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
