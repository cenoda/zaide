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
/// Phase 22.2 corrective tests for the remaining ACP epoch/cache TOCTOU gaps:
/// probe-start fingerprint+epoch preservation across an exact unbind/rebind,
/// conditional invalid-method failure, advertised-method cache lost-update
/// races, conditional cache invalidation, and genuine fingerprint snapshots.
/// </summary>
public sealed class Phase22AcpEpochCacheTocTouTests
{
    [Fact]
    public async Task UnbindRebindSameFingerprintDuringProbe_DoesNotAttachPreMutationClient()
    {
        var publicationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePublication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var harness = EpochTocHarness.Create(
            probePublicationDelayAsync: async ct =>
            {
                publicationStarted.TrySetResult();
                await releasePublication.Task.WaitAsync(ct).ConfigureAwait(false);
            });
        var actorId = ActorId.TownhallAgent;

        const string executable = "/usr/bin/fake-agent";
        string[] arguments = new[] { "--mode", "epoch-toc" };
        const string expectedName = "acp-fake-agent";
        const string expectedVersion = "phase-22.2-epoch-toc";

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity(executable, arguments),
            expectedName,
            expectedVersion).IsSuccess);

        var probeTask = harness.Onboarding.ProbeAsync(actorId, CancellationToken.None);
        await publicationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Exact unbind/rebind with the same durable fields and the same
        // reset revision. The epoch must advance (unbind clears it; rebind
        // re-allocates it), and the in-flight probe must invalidate the
        // pre-mutation client.
        Assert.True(harness.Selection.TryUnbind(actorId, expectedRevision: 1).IsSuccess);
        var rebind = harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity(executable, arguments),
            expectedName,
            expectedVersion);
        Assert.True(rebind.IsSuccess);

        releasePublication.TrySetResult();
        var probe = await probeTask;
        Assert.False(probe.IsSuccess);
        Assert.Contains("changed", probe.Message!, StringComparison.OrdinalIgnoreCase);

        // The pre-mutation client must be disposed; no connection or
        // advertised methods may leak into the replacement binding.
        var originalClient = harness.AllClients[0];
        await WaitUntilAsync(() => originalClient.DisposeCallCount == 1, TimeSpan.FromSeconds(2));
        Assert.Equal(1, originalClient.DisposeCallCount);

        Assert.False(harness.Onboarding.IsLogoutSupported(actorId));
        Assert.Empty(harness.Onboarding.GetNegotiatedAuthMethodIds(actorId));
        Assert.Empty(harness.Selection.GetAdvertisedAuthMethodIds(actorId));

        // The replacement binding remains Disconnected (no successful probe
        // for it has run).
        Assert.True(harness.Store.TryGetBinding(actorId, out var binding));
        Assert.Equal(AgentAuthenticationConnectionState.Disconnected, binding.AuthenticationState);
    }

    [Fact]
    public async Task InvalidMethodPublication_ConcurrentMutation_DoesNotMarkReplacementFailed()
    {
        // Pause AFTER the old advertised cache is captured, BEFORE the
        // invalid-method path's conditional mutation runs.
        var invalidMethodPathReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInvalidMethod = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var harness = EpochTocHarness.Create(
            invalidMethodDelayAsync: async ct =>
            {
                invalidMethodPathReached.TrySetResult();
                await releaseInvalidMethod.Task.WaitAsync(ct).ConfigureAwait(false);
            });
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-a", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-22.2-epoch-toc").IsSuccess);

        Assert.True((await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
        Assert.True(harness.Store.TryGetBinding(actorId, out _));

        // Start the invalid-method path. It captures the cache, sees the
        // method is invalid, and pauses before the conditional mutation.
        var requestTask = harness.Selection.RequestAuthenticateAsync(
            actorId,
            "definitely-not-advertised",
            CancellationToken.None);
        await invalidMethodPathReached.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Rebind while the invalid-method publication is paused. The
        // replacement binding has a different fingerprint+epoch.
        Assert.True(harness.Selection.TryUnbind(actorId, expectedRevision: 1).IsSuccess);
        var rebind = harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-b", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-22.2-epoch-toc");
        Assert.True(rebind.IsSuccess);

        releaseInvalidMethod.TrySetResult();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => requestTask);
        Assert.Contains("changed", ex.Message, StringComparison.OrdinalIgnoreCase);

        // The replacement binding must not be marked Failed.
        Assert.True(harness.Store.TryGetBinding(actorId, out var binding));
        Assert.Equal(AgentAuthenticationConnectionState.Disconnected, binding.AuthenticationState);
        Assert.NotEqual(AgentAuthenticationConnectionState.Failed, binding.AuthenticationState);
    }

    [Fact]
    public async Task StaleAdvertisedMethodRecord_CannotOverwriteNewerBindingCache()
    {
        using var harness = EpochTocHarness.Create();
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-a", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-22.2-epoch-toc").IsSuccess);

        // Establish a valid cache entry on binding revision 1.
        Assert.True((await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
        Assert.True(harness.Store.TryCaptureAcpBindingFingerprint(actorId, out var oldFingerprint, out var oldEpoch));
        string[] oldMethods = new[] { "old-method" };
        harness.Selection.RecordAdvertisedAuthMethodsIfFingerprintMatches(
            actorId,
            oldFingerprint,
            oldEpoch,
            oldMethods);
        Assert.Equal(oldMethods, harness.Selection.GetAdvertisedAuthMethodIds(actorId));

        // Update the binding to revision 2 with a different executable.
        Assert.True(harness.Selection.TryUpdateAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-b", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-22.2-epoch-toc",
            expectedRevision: 1).IsSuccess);
        Assert.Empty(harness.Selection.GetAdvertisedAuthMethodIds(actorId));

        // Publish a valid cache for the new binding.
        Assert.True(harness.Store.TryCaptureAcpBindingFingerprint(actorId, out var newFingerprint, out var newEpoch));
        string[] newMethods = new[] { "new-method" };
        harness.Selection.RecordAdvertisedAuthMethodsIfFingerprintMatches(
            actorId,
            newFingerprint,
            newEpoch,
            newMethods);
        Assert.Equal(newMethods, harness.Selection.GetAdvertisedAuthMethodIds(actorId));

        // Now have the stale probe attempt to overwrite the newer entry
        // using the old fingerprint+epoch. The validation must fail and
        // the newer entry must remain intact.
        harness.Selection.RecordAdvertisedAuthMethodsIfFingerprintMatches(
            actorId,
            oldFingerprint,
            oldEpoch,
            new[] { "stale-method" });

        Assert.Equal(newMethods, harness.Selection.GetAdvertisedAuthMethodIds(actorId));
    }

    [Fact]
    public async Task StaleConditionalClear_CannotRemoveNewerBindingCache()
    {
        using var harness = EpochTocHarness.Create();
        var actorId = ActorId.TownhallAgent;

        Assert.True(harness.Selection.TryBindAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-a", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-22.2-epoch-toc").IsSuccess);

        // Establish a valid cache entry on the original binding.
        Assert.True((await harness.Onboarding.ProbeAsync(actorId, CancellationToken.None)).IsSuccess);
        Assert.True(harness.Store.TryCaptureAcpBindingFingerprint(actorId, out var oldFingerprint, out var oldEpoch));

        // Update the binding to a new executable (revision 2).
        Assert.True(harness.Selection.TryUpdateAcpRuntime(
            actorId,
            new AcpRuntimeIdentity("/usr/bin/fake-agent-b", Array.Empty<string>()),
            "acp-fake-agent",
            "phase-22.2-epoch-toc",
            expectedRevision: 1).IsSuccess);

        // Publish a valid cache for the new binding.
        Assert.True(harness.Store.TryCaptureAcpBindingFingerprint(actorId, out var newFingerprint, out var newEpoch));
        harness.Selection.RecordAdvertisedAuthMethodsIfFingerprintMatches(
            actorId,
            newFingerprint,
            newEpoch,
            new[] { "new-method" });
        Assert.Equal(new[] { "new-method" }, harness.Selection.GetAdvertisedAuthMethodIds(actorId));

        // A stale clear (using the old fingerprint+epoch) must not remove
        // the newer entry.
        harness.Selection.ClearAdvertisedAuthMethodsIfFingerprintMatches(
            actorId,
            oldFingerprint,
            oldEpoch);
        Assert.Equal(new[] { "new-method" }, harness.Selection.GetAdvertisedAuthMethodIds(actorId));

        // A matching clear removes the entry.
        harness.Selection.ClearAdvertisedAuthMethodsIfFingerprintMatches(
            actorId,
            newFingerprint,
            newEpoch);
        Assert.Empty(harness.Selection.GetAdvertisedAuthMethodIds(actorId));
    }

    [Fact]
    public void MutatingSourceArguments_AfterConstruction_DoesNotChangeCapturedIdentity()
    {
        var sourceArguments = new List<string> { "--initial", "value" };
        var runtime = new AcpRuntimeIdentity(
            "/usr/bin/fake-agent",
            sourceArguments,
            registryId: "test",
            distributionProvenance: "snapshot-test");

        // Mutate the source collection after construction.
        sourceArguments.Clear();
        sourceArguments.Add("--mutated");

        // The captured identity must remain the original snapshot.
        Assert.Equal(new[] { "--initial", "value" }, runtime.Arguments);

        var fingerprint = new AcpRuntimeBindingFingerprint(
            revision: 1,
            runtimeIdentity: runtime,
            expectedAgentName: "acp-fake-agent",
            expectedAgentVersion: "phase-22.2-snapshot");

        // Mutate the source again — fingerprint must not change.
        sourceArguments.Clear();
        sourceArguments.Add("--further-mutated");
        Assert.Equal(new[] { "--initial", "value" }, fingerprint.Arguments);

        // Equality must hold against another fingerprint built from the
        // same captured snapshot, even after a post-construction mutation.
        var equalityFingerprint = new AcpRuntimeBindingFingerprint(
            revision: 1,
            runtimeIdentity: new AcpRuntimeIdentity(
                "/usr/bin/fake-agent",
                new[] { "--initial", "value" }),
            expectedAgentName: "acp-fake-agent",
            expectedAgentVersion: "phase-22.2-snapshot");
        Assert.Equal(fingerprint, equalityFingerprint);
        Assert.Equal(fingerprint.GetHashCode(), equalityFingerprint.GetHashCode());
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10).ConfigureAwait(false);
        }

        Assert.True(condition(), "Timed out waiting for expected condition.");
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

    private sealed class EpochTocHarness : IDisposable
    {
        private readonly string _directory;

        private EpochTocHarness(
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

        public static EpochTocHarness Create(
            Func<CancellationToken, Task>? probePublicationDelayAsync = null,
            Func<CancellationToken, Task>? invalidMethodDelayAsync = null)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "zaide-phase22-acp-epoch-toc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var workspace = Path.Combine(directory, "workspace");
            Directory.CreateDirectory(workspace);

            var store = new AgentActorBackendBindingStore(
                Path.Combine(directory, "agent-backend-bindings.json"),
                Path.Combine(directory, "agent-backend-bindings.json.tmp"),
                Path.Combine(directory, "agent-backend-bindings.json.lastknowngood"));

            var allClients = new List<AcpFakeSessionClient>();
            AcpOnboardingConnectionService? onboarding = null;
            var selection = new AgentActorBackendSelectionService(store, () => onboarding);

            var methods = new List<AcpAuthMethod>
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
                    var client = new AcpFakeSessionClient(new AcpFakeSessionScript
                    {
                        AgentName = "acp-fake-agent",
                        AgentVersion = "phase-22.2-epoch-toc",
                        AuthMethods = methods,
                    });
                    allClients.Add(client);
                    return Task.FromResult<IAcpSessionClient>(client);
                });

            onboarding.ProbePublicationDelayForTestAsync = probePublicationDelayAsync;
            selection.InvalidMethodPublicationDelayForTestAsync = invalidMethodDelayAsync;

            return new EpochTocHarness(directory, store, selection, onboarding, allClients);
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
