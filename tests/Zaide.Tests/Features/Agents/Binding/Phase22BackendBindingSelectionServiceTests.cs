using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Binding;

public sealed class Phase22BackendBindingSelectionServiceTests
{
    [Fact]
    public void TryBindNativeHarness_MapsToStoreAndSnapshot()
    {
        using var harness = SelectionHarness.Create();
        var actorId = ActorId.TownhallAgent;
        var changes = new List<AgentActorBackendBindingChangedEvent>();
        harness.Selection.BindingChanged += changes.Add;

        var result = harness.Selection.TryBindNativeHarness(actorId);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Revision);

        var snapshot = harness.Selection.GetSnapshot(actorId);
        Assert.True(snapshot.IsBound);
        Assert.Equal(AgentBackendIds.NativeHarness, snapshot.BackendId);
        Assert.Equal("Native Harness", snapshot.BackendLabel);
        Assert.Equal(AgentAuthenticationConnectionState.NotRequired, snapshot.AuthenticationState);
        Assert.Single(changes);
        Assert.Equal(AgentActorBackendBindingMutationKind.Bind, changes[0].Kind);
    }

    [Fact]
    public void TryBindAcpRuntime_MapsToStore()
    {
        using var harness = SelectionHarness.Create();
        var actorId = ActorId.TownhallAgent;
        var runtime = new AcpRuntimeIdentity("/usr/bin/fake-agent", new[] { "healthy" });

        var result = harness.Selection.TryBindAcpRuntime(
            actorId,
            runtime,
            "acp-fake-agent",
            "1.0.0");

        Assert.True(result.IsSuccess);
        Assert.True(harness.Store.TryGetBinding(actorId, out var binding));
        Assert.Equal(AgentBackendIds.Acp, binding.BackendId);
        Assert.Equal("/usr/bin/fake-agent", binding.AcpRuntime!.ExecutablePath);
        Assert.Equal(new[] { "healthy" }, binding.AcpRuntime.Arguments);
        Assert.Equal("acp-fake-agent", binding.ExpectedAgentName);

        var snapshot = harness.Selection.GetSnapshot(actorId);
        Assert.True(snapshot.IsBound);
        Assert.Equal("ACP", snapshot.BackendLabel);
        Assert.Contains("/usr/bin/fake-agent", snapshot.StatusCaption, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_ClearsAdvertisedAuthRuntimeCache()
    {
        using var harness = SelectionHarness.Create();
        var actorId = ActorId.TownhallAgent;
        var runtime = new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>());

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            runtime,
            "acp-fake-agent",
            "1.0.0").IsSuccess);

        harness.Selection.RecordAdvertisedAuthMethods(actorId, new[] { "oauth", "token" });
        Assert.Equal(new[] { "oauth", "token" }, harness.Selection.GetAdvertisedAuthMethodIds(actorId));

        await harness.Selection.RequestAuthenticateAsync(actorId, "oauth", CancellationToken.None);
        var authenticated = harness.Selection.GetSnapshot(actorId);
        Assert.Equal(AgentAuthenticationConnectionState.Authenticated, authenticated.AuthenticationState);

        var update = harness.Selection.TryUpdateAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", new[] { "--new" }),
            "acp-fake-agent",
            "1.1.0",
            expectedRevision: 1);

        Assert.True(update.IsSuccess);
        Assert.Equal(2, update.Revision);
        Assert.Empty(harness.Selection.GetAdvertisedAuthMethodIds(actorId));

        var snapshot = harness.Selection.GetSnapshot(actorId);
        Assert.Equal(AgentAuthenticationConnectionState.Disconnected, snapshot.AuthenticationState);
        Assert.DoesNotContain("Auth: oauth", snapshot.StatusCaption, StringComparison.Ordinal);
    }

    [Fact]
    public void Unbind_ClearsRuntimeAndSnapshotBecomesUnbound()
    {
        using var harness = SelectionHarness.Create();
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Selection.TryBindNativeHarness(actorId).IsSuccess);
        harness.Selection.RecordAdvertisedAuthMethods(actorId, new[] { "x" });

        var unbind = harness.Selection.TryUnbind(actorId, expectedRevision: 1);
        Assert.True(unbind.IsSuccess);
        Assert.False(harness.Store.HasBinding(actorId));
        Assert.Empty(harness.Selection.GetAdvertisedAuthMethodIds(actorId));

        var snapshot = harness.Selection.GetSnapshot(actorId);
        Assert.False(snapshot.IsBound);
        Assert.Equal("Unbound", snapshot.BackendLabel);
    }

    [Fact]
    public void ConflictAndBusy_SurfaceThroughSelection()
    {
        var busy = new FixedActiveRunQuery(isBusy: false);
        using var harness = SelectionHarness.Create(busy);
        var actorId = ActorId.TownhallAgent;
        Assert.True(harness.Selection.TryBindNativeHarness(actorId).IsSuccess);

        var conflict = harness.Selection.TryUpdateNativeHarness(actorId, expectedRevision: 99);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Conflict, conflict.Status);

        busy.IsBusy = true;
        var busyUpdate = harness.Selection.TryUpdateNativeHarness(actorId, expectedRevision: 1);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Busy, busyUpdate.Status);

        var busyUnbind = harness.Selection.TryUnbind(actorId, expectedRevision: 1);
        Assert.Equal(AgentActorBackendBindingMutationStatus.Busy, busyUnbind.Status);

        // Snapshot remains bound at previous revision after rejections.
        var snapshot = harness.Selection.GetSnapshot(actorId);
        Assert.True(snapshot.IsBound);
        Assert.Equal(1, harness.Store.GetRevision(actorId));
    }

    [Fact]
    public void CompatibilityBindMethods_StillWriteThroughTypedPath()
    {
        using var harness = SelectionHarness.Create();
        var actorId = ActorId.TownhallAgent;
        harness.Selection.BindNativeHarness(actorId);
        Assert.True(harness.Store.HasBinding(actorId));
        Assert.Equal(1, harness.Store.GetRevision(actorId));

        harness.Selection.BindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent", Array.Empty<string>()),
            "acp-fake-agent",
            "1.0.0");
        Assert.Equal(AgentBackendIds.Acp, harness.Store.GetRequiredBackendId(actorId));
        Assert.Equal(2, harness.Store.GetRevision(actorId));
    }

    private sealed class FixedActiveRunQuery : IAgentActorActiveRunQuery
    {
        public FixedActiveRunQuery(bool isBusy) => IsBusy = isBusy;

        public bool IsBusy { get; set; }

        public bool HasActiveRun(ActorId actorId) => IsBusy;
    }

    private sealed class SelectionHarness : IDisposable
    {
        private readonly string _directory;

        private SelectionHarness(
            string directory,
            AgentActorBackendBindingStore store,
            AgentActorBackendSelectionService selection)
        {
            _directory = directory;
            Store = store;
            Selection = selection;
        }

        public AgentActorBackendBindingStore Store { get; }

        public AgentActorBackendSelectionService Selection { get; }

        public static SelectionHarness Create(IAgentActorActiveRunQuery? activeRunQuery = null)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "zaide-phase22-binding-selection-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new AgentActorBackendBindingStore(
                Path.Combine(directory, "agent-backend-bindings.json"),
                Path.Combine(directory, "agent-backend-bindings.json.tmp"),
                Path.Combine(directory, "agent-backend-bindings.json.lastknowngood"),
                activeRunQuery);
            var selection = new AgentActorBackendSelectionService(store);
            return new SelectionHarness(directory, store, selection);
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
