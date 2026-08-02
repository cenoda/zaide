using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Binding;

public sealed class Phase22BackendBindingStoreTests
{
    [Fact]
    public void Bind_NativeHarness_PersistsRevisionAndNotifies()
    {
        using var harness = DurableHarness.Create();
        var changes = new List<AgentActorBackendBindingChangedEvent>();
        harness.Store.BindingChanged += changes.Add;

        var actorId = ActorId.TownhallAgent;
        var result = harness.Store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Revision);
        Assert.True(harness.Store.HasBinding(actorId));
        Assert.True(harness.Store.TryGetBinding(actorId, out var binding));
        Assert.Equal(AgentBackendIds.NativeHarness, binding.BackendId);
        Assert.Equal(1, binding.Revision);
        Assert.Null(binding.AcpRuntime);
        Assert.Single(changes);
        Assert.Equal(AgentActorBackendBindingMutationKind.Bind, changes[0].Kind);
        Assert.Equal(1, changes[0].Revision);
    }

    [Fact]
    public void Bind_Acp_RequiresRuntimeAndDoesNotStoreAuth()
    {
        using var harness = DurableHarness.Create();
        var actorId = ActorId.TownhallAgent;
        var runtime = new AcpRuntimeIdentity(
            "/usr/bin/fake-agent",
            new[] { "--mode", "healthy" },
            registryId: "reg-1",
            distributionProvenance: "fixture");

        var result = harness.Store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.Acp,
            runtime,
            expectedAgentName: "acp-fake-agent",
            expectedAgentVersion: "1.0.0",
            selectedAuthMethodId: "oauth",
            authenticationState: AgentAuthenticationConnectionState.Authenticated));

        Assert.True(result.IsSuccess);
        Assert.True(harness.Store.TryGetBinding(actorId, out var binding));
        Assert.Equal(AgentBackendIds.Acp, binding.BackendId);
        Assert.NotNull(binding.AcpRuntime);
        Assert.Equal("/usr/bin/fake-agent", binding.AcpRuntime!.ExecutablePath);
        Assert.Equal(new[] { "--mode", "healthy" }, binding.AcpRuntime.Arguments);
        // Durable bind clears runtime auth; never treats auth as durable truth.
        Assert.Null(binding.SelectedAuthMethodId);
        Assert.Equal(AgentAuthenticationConnectionState.Disconnected, binding.AuthenticationState);
        Assert.Equal(1, binding.Revision);

        Assert.Throws<ArgumentException>(() => new AgentActorBackendBinding(
            ActorId.HumanUser,
            AgentBackendIds.NativeHarness,
            runtime));
    }

    [Fact]
    public void Update_RejectsStaleRevision_AndBusyActor()
    {
        var busy = new FixedActiveRunQuery(isBusy: true);
        using var harness = DurableHarness.Create(busy);
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness)).IsSuccess);

        var busyResult = harness.Store.TryUpdate(
            actorId,
            new AgentActorBackendBinding(actorId, AgentBackendIds.NativeHarness),
            expectedRevision: 1);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Busy, busyResult.Status);
        Assert.Equal(1, harness.Store.GetRevision(actorId));

        busy.IsBusy = false;
        var conflict = harness.Store.TryUpdate(
            actorId,
            new AgentActorBackendBinding(actorId, AgentBackendIds.NativeHarness),
            expectedRevision: 99);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Conflict, conflict.Status);
        Assert.Equal(1, conflict.Revision);
    }

    [Fact]
    public void Update_Idle_AdvancesRevision_ClearsRuntimeAuth()
    {
        using var harness = DurableHarness.Create();
        var actorId = ActorId.TownhallAgent;
        var runtime = new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>());

        Assert.True(harness.Store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.Acp,
            runtime,
            "acp-fake-agent",
            "1.0.0")).IsSuccess);

        harness.Store.SetRuntimeAuthentication(
            actorId,
            "oauth",
            AgentAuthenticationConnectionState.Authenticated);
        Assert.True(harness.Store.TryGetBinding(actorId, out var authenticated));
        Assert.Equal(AgentAuthenticationConnectionState.Authenticated, authenticated.AuthenticationState);

        var changes = new List<AgentActorBackendBindingChangedEvent>();
        harness.Store.BindingChanged += changes.Add;

        var updatedRuntime = new AcpRuntimeIdentity(
            "/usr/bin/fake-agent",
            new[] { "--updated" });
        var result = harness.Store.TryUpdate(
            actorId,
            new AgentActorBackendBinding(
                actorId,
                AgentBackendIds.Acp,
                updatedRuntime,
                "acp-fake-agent",
                "1.1.0"),
            expectedRevision: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Revision);
        Assert.True(harness.Store.TryGetBinding(actorId, out var updated));
        Assert.Equal(2, updated.Revision);
        Assert.Equal(new[] { "--updated" }, updated.AcpRuntime!.Arguments);
        Assert.Null(updated.SelectedAuthMethodId);
        Assert.Equal(AgentAuthenticationConnectionState.Disconnected, updated.AuthenticationState);
        Assert.Single(changes);
        Assert.Equal(AgentActorBackendBindingMutationKind.Update, changes[0].Kind);
    }

    [Fact]
    public void Unbind_Idle_RemovesBinding_BusyRejected()
    {
        var busy = new FixedActiveRunQuery(isBusy: false);
        using var harness = DurableHarness.Create(busy);
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness)).IsSuccess);

        busy.IsBusy = true;
        var busyResult = harness.Store.TryUnbind(actorId, expectedRevision: 1);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Busy, busyResult.Status);
        Assert.True(harness.Store.HasBinding(actorId));

        busy.IsBusy = false;
        var changes = new List<AgentActorBackendBindingChangedEvent>();
        harness.Store.BindingChanged += changes.Add;

        var unbind = harness.Store.TryUnbind(actorId, expectedRevision: 1);
        Assert.True(unbind.IsSuccess);
        Assert.False(harness.Store.HasBinding(actorId));
        Assert.Equal(0, harness.Store.GetRevision(actorId));
        Assert.Single(changes);
        Assert.Equal(AgentActorBackendBindingMutationKind.Unbind, changes[0].Kind);
        Assert.False(changes[0].IsBound);
    }

    [Fact]
    public void Bindings_AreIsolatedPerActorId_AndIgnoreWorkspace()
    {
        using var harness = DurableHarness.Create();
        var actorA = ActorId.TownhallAgent;
        var actorB = ActorId.HumanUser;

        Assert.True(harness.Store.TryBind(new AgentActorBackendBinding(
            actorA,
            AgentBackendIds.NativeHarness)).IsSuccess);
        Assert.True(harness.Store.TryBind(new AgentActorBackendBinding(
            actorB,
            AgentBackendIds.Acp,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "1.0.0")).IsSuccess);

        Assert.Equal(AgentBackendIds.NativeHarness, harness.Store.GetRequiredBackendId(actorA));
        Assert.Equal(AgentBackendIds.Acp, harness.Store.GetRequiredBackendId(actorB));
        Assert.Equal(1, harness.Store.GetRevision(actorA));
        Assert.Equal(1, harness.Store.GetRevision(actorB));

        Assert.True(harness.Store.TryUnbind(actorA, expectedRevision: 1).IsSuccess);
        Assert.False(harness.Store.HasBinding(actorA));
        Assert.True(harness.Store.HasBinding(actorB));
    }

    [Fact]
    public void ChangeNotification_DoesNotFire_OnFailedMutation()
    {
        using var harness = DurableHarness.Create();
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Store.TryBind(new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness)).IsSuccess);

        var changes = new List<AgentActorBackendBindingChangedEvent>();
        harness.Store.BindingChanged += changes.Add;

        var conflict = harness.Store.TryUpdate(
            actorId,
            new AgentActorBackendBinding(actorId, AgentBackendIds.NativeHarness),
            expectedRevision: 42);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Conflict, conflict.Status);
        Assert.Empty(changes);
    }

    private sealed class FixedActiveRunQuery : IAgentActorActiveRunQuery
    {
        public FixedActiveRunQuery(bool isBusy) => IsBusy = isBusy;

        public bool IsBusy { get; set; }

        public bool HasActiveRun(ActorId actorId) => IsBusy;
    }

    private sealed class DurableHarness : IDisposable
    {
        private readonly string _directory;

        private DurableHarness(string directory, AgentActorBackendBindingStore store)
        {
            _directory = directory;
            Store = store;
        }

        public AgentActorBackendBindingStore Store { get; }

        public static DurableHarness Create(IAgentActorActiveRunQuery? activeRunQuery = null)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "zaide-phase22-binding-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new AgentActorBackendBindingStore(
                Path.Combine(directory, "agent-backend-bindings.json"),
                Path.Combine(directory, "agent-backend-bindings.json.tmp"),
                Path.Combine(directory, "agent-backend-bindings.json.lastknowngood"),
                activeRunQuery);
            return new DurableHarness(directory, store);
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
