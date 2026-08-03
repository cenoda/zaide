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

/// <summary>
/// Phase 22.2 corrective tests for ACP onboarding connection invalidation on
/// durable binding mutation and fail-closed authenticate/logout validation.
/// </summary>
public sealed class Phase22AcpRuntimeInvalidationTests
{
    [Fact]
    public async Task ProbeThenUnbind_DetachesAndDisposesConnection()
    {
        using var harness = InvalidationHarness.Create();
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        var probe = await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None);
        Assert.True(probe.IsSuccess, probe.Message);
        Assert.True(harness.Onboarding.IsLogoutSupported(actorId));
        Assert.NotEmpty(harness.Onboarding.GetNegotiatedAuthMethodIds(actorId));

        var probedClient = harness.AllClients[^1];
        Assert.Equal(0, probedClient.DisposeCallCount);

        var unbind = harness.Selection.TryUnbind(actorId, expectedRevision: 1);
        Assert.True(unbind.IsSuccess);
        Assert.False(harness.Store.HasBinding(actorId));

        await harness.FlushDisposalsForTestAsync(actorId);

        Assert.Equal(1, probedClient.DisposeCallCount);
        Assert.False(harness.Onboarding.IsLogoutSupported(actorId));
        Assert.Empty(harness.Onboarding.GetNegotiatedAuthMethodIds(actorId));

        var auth = await harness.Onboarding.AuthenticateAsync(actorId, "oauth", CancellationToken.None);
        Assert.False(auth.IsSuccess);
        Assert.Contains("unavailable", auth.Message!, StringComparison.OrdinalIgnoreCase);

        var logout = await harness.Onboarding.LogoutAsync(actorId, CancellationToken.None);
        Assert.False(logout.IsSuccess);
        Assert.Equal(0, probedClient.AuthenticateCallCount);
    }

    [Fact]
    public async Task ProbeThenUpdate_DetachesAndDisposesOldConnection()
    {
        using var harness = InvalidationHarness.Create();
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-a", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        var probe = await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None);
        Assert.True(probe.IsSuccess, probe.Message);
        var clientA = harness.AllClients[^1];
        Assert.True(harness.Onboarding.IsLogoutSupported(actorId));
        Assert.NotEmpty(harness.Onboarding.GetNegotiatedAuthMethodIds(actorId));

        var update = harness.Selection.TryUpdateAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-b", new[] { "--mode", "healthy" }),
            "acp-fake-agent",
            "phase-20-m4",
            expectedRevision: 1);
        Assert.True(update.IsSuccess);

        await harness.FlushDisposalsForTestAsync(actorId);

        Assert.Equal(1, clientA.DisposeCallCount);
        Assert.False(harness.Onboarding.IsLogoutSupported(actorId));
        Assert.Empty(harness.Onboarding.GetNegotiatedAuthMethodIds(actorId));

        var snapshot = harness.Selection.GetSnapshot(actorId);
        Assert.Equal(AgentAuthenticationConnectionState.Disconnected, snapshot.AuthenticationState);
        Assert.Empty(harness.Selection.GetAdvertisedAuthMethodIds(actorId));
    }

    [Fact]
    public async Task RebindCannotAuthenticateThroughPreviousRuntime()
    {
        using var harness = InvalidationHarness.Create();
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-a", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        Assert.True((await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
        var clientA = harness.AllClients[^1];

        var update = harness.Selection.TryUpdateAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-b", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m4",
            expectedRevision: 1);
        Assert.True(update.IsSuccess);
        await harness.FlushDisposalsForTestAsync(actorId);

        var auth = await harness.Onboarding.AuthenticateAsync(actorId, "oauth", CancellationToken.None);
        Assert.False(auth.IsSuccess);
        Assert.Equal(0, clientA.AuthenticateCallCount);

        Assert.True(harness.Store.TryGetBinding(actorId, out var binding));
        Assert.NotEqual(AgentAuthenticationConnectionState.Authenticated, binding.AuthenticationState);
    }

    [Fact]
    public async Task BindingChangesDuringAuthenticate_DoesNotMarkReplacementAuthenticated()
    {
        var authenticateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAuthenticate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var harness = InvalidationHarness.Create(
            authenticateDelayAsync: async ct =>
            {
                authenticateStarted.TrySetResult();
                await releaseAuthenticate.Task.WaitAsync(ct).ConfigureAwait(false);
            });
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-a", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        Assert.True((await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
        var client = harness.AllClients[^1];

        var authenticateTask = harness.Onboarding.AuthenticateAsync(actorId, "oauth", CancellationToken.None);
        await authenticateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var update = harness.Selection.TryUpdateAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-b", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m4",
            expectedRevision: 1);
        Assert.True(update.IsSuccess);

        releaseAuthenticate.TrySetResult();
        var auth = await authenticateTask;
        Assert.False(auth.IsSuccess);
        Assert.Contains("changed", auth.Message!, StringComparison.OrdinalIgnoreCase);

        Assert.True(harness.Store.TryGetBinding(actorId, out var binding));
        Assert.NotEqual(AgentAuthenticationConnectionState.Authenticated, binding.AuthenticationState);
        Assert.Equal(1, client.AuthenticateCallCount);
    }

    [Fact]
    public async Task EmptyAdvertisedMethods_AuthenticateFailsClosed()
    {
        using var harness = InvalidationHarness.Create(authMethods: Array.Empty<AcpAuthMethod>());
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        Assert.True((await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
        var client = harness.AllClients[^1];

        var auth = await harness.Onboarding.AuthenticateAsync(actorId, "oauth", CancellationToken.None);
        Assert.False(auth.IsSuccess);
        Assert.Contains("no methods", auth.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, client.AuthenticateCallCount);

        Assert.True(harness.Store.TryGetBinding(actorId, out var binding));
        Assert.NotEqual(AgentAuthenticationConnectionState.Authenticated, binding.AuthenticationState);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Selection.RequestAuthenticateAsync(actorId, "oauth", CancellationToken.None));
        Assert.Contains("no methods", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedMutation_DoesNotInvalidateCurrentConnection()
    {
        var busy = new FixedActiveRunQuery(isBusy: false);
        using var harness = InvalidationHarness.Create(activeRunQuery: busy);
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m3").IsSuccess);

        Assert.True((await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
        var client = harness.AllClients[^1];
        Assert.True(harness.Onboarding.IsLogoutSupported(actorId));

        busy.IsBusy = true;
        var busyUnbind = harness.Selection.TryUnbind(actorId, expectedRevision: 1);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Busy, busyUnbind.Status);

        await harness.FlushDisposalsForTestAsync(actorId);
        Assert.Equal(0, client.DisposeCallCount);
        Assert.True(harness.Onboarding.IsLogoutSupported(actorId));
        Assert.NotEmpty(harness.Onboarding.GetNegotiatedAuthMethodIds(actorId));

        var conflict = harness.Selection.TryUpdateAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-b", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-20-m4",
            expectedRevision: 99);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Conflict, conflict.Status);
        Assert.Equal(0, client.DisposeCallCount);
        Assert.True(harness.Onboarding.IsLogoutSupported(actorId));
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

    private sealed class FixedActiveRunQuery : IAgentActorActiveRunQuery
    {
        public FixedActiveRunQuery(bool isBusy) => IsBusy = isBusy;

        public bool IsBusy { get; set; }

        public bool HasActiveRun(ActorId actorId) => IsBusy;
    }

    private sealed class InvalidationHarness : IDisposable
    {
        private readonly string _directory;

        private InvalidationHarness(
            string directory,
            AgentActorBackendBindingStore store,
            AgentActorBackendSelectionService selection,
            AcpOnboardingConnectionService onboarding,
            List<AcpFakeSessionClient> allClients)
        {
            _directory = directory;
            Store = store;
            Selection = selection;
            Onboarding = onboarding;
            AllClients = allClients;
        }

        public AgentActorBackendBindingStore Store { get; }

        public AgentActorBackendSelectionService Selection { get; }

        public AcpOnboardingConnectionService Onboarding { get; }

        public List<AcpFakeSessionClient> AllClients { get; }

        public static InvalidationHarness Create(
            IReadOnlyList<AcpAuthMethod>? authMethods = null,
            IAgentActorActiveRunQuery? activeRunQuery = null,
            Func<CancellationToken, Task>? authenticateDelayAsync = null)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "zaide-phase22-acp-invalidation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var workspace = Path.Combine(directory, "workspace");
            Directory.CreateDirectory(workspace);

            var store = new AgentActorBackendBindingStore(
                Path.Combine(directory, "agent-backend-bindings.json"),
                Path.Combine(directory, "agent-backend-bindings.json.tmp"),
                Path.Combine(directory, "agent-backend-bindings.json.lastknowngood"),
                activeRunQuery);

            var allClients = new List<AcpFakeSessionClient>();
            AcpOnboardingConnectionService? onboarding = null;
            var selection = new AgentActorBackendSelectionService(store, () => onboarding);

            var methods = authMethods ?? new[]
            {
                new AcpAuthMethod { Id = "oauth", Name = "OAuth" },
            };

            onboarding = new AcpOnboardingConnectionService(
                store,
                selection,
                new FixedWorkspaceAuthority(workspace),
                activeRunQuery,
                clientFactory: (_, _, _) =>
                {
                    var client = new AcpFakeSessionClient(new AcpFakeSessionScript
                    {
                        AgentName = "acp-fake-agent",
                        AgentVersion = "phase-20-m3",
                        AuthMethods = methods,
                        AuthenticateShouldFail = false,
                    })
                    {
                        AuthenticateDelayAsync = authenticateDelayAsync,
                    };
                    allClients.Add(client);
                    return Task.FromResult<IAcpSessionClient>(client);
                });

            return new InvalidationHarness(directory, store, selection, onboarding, allClients);
        }

        public Task FlushDisposalsForTestAsync(ActorId actorId) =>
            Onboarding.FlushPendingDisposalsForTestAsync();

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
