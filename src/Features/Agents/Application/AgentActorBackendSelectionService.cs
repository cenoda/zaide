using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Application service for explicit actor/backend selection and bounded auth state.
/// Durable mutations go through the typed binding store; runtime auth/method
/// caches are cleared on successful update/unbind.
/// </summary>
internal sealed class AgentActorBackendSelectionService : IAgentActorBackendSelectionService
{
    private readonly IAgentActorBackendBindingStore _bindingStore;
    private readonly Func<IAcpOnboardingConnectionService?>? _onboardingResolver;
    private readonly Dictionary<ActorId, AdvertisedAuthMethodCache> _advertisedAuthMethods = new();
    private readonly object _sync = new();
    private readonly List<Action<AgentActorBackendBindingChangedEvent>> _changeHandlers = new();

    /// <summary>
    /// Test seam: invoked after the advertised-method cache entry is captured
    /// and the requested method is determined to be invalid, but before the
    /// conditional runtime-auth mutation. Used to deterministically exercise
    /// the invalid-method path under concurrent binding mutation.
    /// </summary>
    internal Func<CancellationToken, Task>? InvalidMethodPublicationDelayForTestAsync;

    public AgentActorBackendSelectionService(IAgentActorBackendBindingStore bindingStore)
        : this(bindingStore, onboardingResolver: null)
    {
    }

    public AgentActorBackendSelectionService(
        IAgentActorBackendBindingStore bindingStore,
        Func<IAcpOnboardingConnectionService?>? onboardingResolver)
    {
        _bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
        _onboardingResolver = onboardingResolver;
        _bindingStore.BindingChanged += OnStoreBindingChanged;
    }

    public event Action<AgentActorBackendBindingChangedEvent>? BindingChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_sync)
            {
                _changeHandlers.Add(value);
            }
        }
        remove
        {
            if (value is null)
            {
                return;
            }

            lock (_sync)
            {
                _changeHandlers.Remove(value);
            }
        }
    }

    public AgentActorBackendBindingSnapshot GetSnapshot(ActorId actorId)
    {
        if (!_bindingStore.TryGetBinding(actorId, out var binding))
        {
            return new AgentActorBackendBindingSnapshot(
                actorId,
                isBound: false,
                default,
                backendLabel: "Unbound",
                statusCaption: "Select a backend before sending.",
                isDisconnected: false,
                AgentAuthenticationConnectionState.Disconnected,
                Array.Empty<string>());
        }

        var backendLabel = ResolveBackendLabel(binding.BackendId);
        var authMethods = GetAdvertisedAuthMethodIds(actorId);
        var statusCaption = BuildStatusCaption(binding, authMethods);
        var isDisconnected = binding.BackendId == AgentBackendIds.Acp
                             && binding.AuthenticationState == AgentAuthenticationConnectionState.Failed;

        return new AgentActorBackendBindingSnapshot(
            actorId,
            isBound: true,
            binding.BackendId,
            backendLabel,
            statusCaption,
            isDisconnected,
            binding.AuthenticationState,
            authMethods);
    }

    public void BindNativeHarness(ActorId actorId) =>
        _ = TryBindNativeHarness(actorId);

    public void BindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion) =>
        _ = TryBindAcpRuntime(actorId, runtimeIdentity, expectedAgentName, expectedAgentVersion);

    public AgentActorBackendBindingMutationResult TryBindNativeHarness(ActorId actorId)
    {
        var candidate = new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness,
            authenticationState: AgentAuthenticationConnectionState.NotRequired);

        // Use TryBind only when unbound; existing bindings must update.
        if (_bindingStore.TryGetBinding(actorId, out var existing))
        {
            return _bindingStore.TryUpdate(actorId, candidate, existing.Revision);
        }

        return _bindingStore.TryBind(candidate);
    }

    public AgentActorBackendBindingMutationResult TryBindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion)
    {
        ArgumentNullException.ThrowIfNull(runtimeIdentity);

        try
        {
            var candidate = new AgentActorBackendBinding(
                actorId,
                AgentBackendIds.Acp,
                runtimeIdentity,
                expectedAgentName,
                expectedAgentVersion,
                authenticationState: AgentAuthenticationConnectionState.Disconnected);

            // Use TryBind only when unbound; existing bindings must update.
            AgentActorBackendBindingMutationResult result;
            if (_bindingStore.TryGetBinding(actorId, out var existing))
            {
                result = _bindingStore.TryUpdate(actorId, candidate, existing.Revision);
            }
            else
            {
                result = _bindingStore.TryBind(candidate);
            }

            // Store/selection clear advertised methods on any successful durable mutation.
            return result;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return AgentActorBackendBindingMutationResult.ValidationFailed(
                AgentActorBackendBindingMutationKind.Bind,
                actorId,
                _bindingStore.GetRevision(actorId),
                ex.Message);
        }
    }

    public AgentActorBackendBindingMutationResult TryUpdateNativeHarness(
        ActorId actorId,
        long expectedRevision)
    {
        var candidate = new AgentActorBackendBinding(
            actorId,
            AgentBackendIds.NativeHarness,
            authenticationState: AgentAuthenticationConnectionState.NotRequired);

        var result = _bindingStore.TryUpdate(actorId, candidate, expectedRevision);
        if (result.IsSuccess)
        {
            ClearAdvertisedAuthMethods(actorId);
        }

        return result;
    }

    public AgentActorBackendBindingMutationResult TryUpdateAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion,
        long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(runtimeIdentity);

        try
        {
            var candidate = new AgentActorBackendBinding(
                actorId,
                AgentBackendIds.Acp,
                runtimeIdentity,
                expectedAgentName,
                expectedAgentVersion,
                authenticationState: AgentAuthenticationConnectionState.Disconnected);

            var result = _bindingStore.TryUpdate(actorId, candidate, expectedRevision);
            if (result.IsSuccess)
            {
                // Idle durable update invalidates cached runtime auth/capability state.
                ClearAdvertisedAuthMethods(actorId);
            }

            return result;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return AgentActorBackendBindingMutationResult.ValidationFailed(
                AgentActorBackendBindingMutationKind.Update,
                actorId,
                _bindingStore.GetRevision(actorId),
                ex.Message);
        }
    }

    public AgentActorBackendBindingMutationResult TryUnbind(ActorId actorId, long expectedRevision)
    {
        var result = _bindingStore.TryUnbind(actorId, expectedRevision);
        if (result.IsSuccess)
        {
            ClearAdvertisedAuthMethods(actorId);
        }

        return result;
    }

    public IReadOnlyList<string> GetAdvertisedAuthMethodIds(ActorId actorId)
    {
        lock (_sync)
        {
            if (!_advertisedAuthMethods.TryGetValue(actorId, out var cache))
            {
                return Array.Empty<string>();
            }

            if (!_bindingStore.TryValidateAcpBindingFingerprint(
                    actorId,
                    cache.Fingerprint,
                    cache.Epoch))
            {
                return Array.Empty<string>();
            }

            return cache.MethodIds;
        }
    }

    /// <summary>
    /// Capture the advertised-method cache entry together with its fingerprint
    /// and epoch. The capture is atomic with the cache read so a concurrent
    /// bind/update/unbind cannot rewrite the entry between the capture and
    /// the caller using the pair.
    /// </summary>
    internal bool TryCaptureAdvertisedAuthMethodCache(
        ActorId actorId,
        out AcpRuntimeBindingFingerprint fingerprint,
        out long epoch,
        out IReadOnlyList<string> methodIds)
    {
        lock (_sync)
        {
            if (!_advertisedAuthMethods.TryGetValue(actorId, out var cache))
            {
                fingerprint = null!;
                epoch = 0;
                methodIds = Array.Empty<string>();
                return false;
            }

            fingerprint = cache.Fingerprint;
            epoch = cache.Epoch;
            methodIds = cache.MethodIds;
            return true;
        }
    }

    internal void RecordAdvertisedAuthMethods(ActorId actorId, IReadOnlyList<string> methodIds)
    {
        if (!_bindingStore.TryCaptureAcpBindingFingerprint(actorId, out var fingerprint, out var epoch))
        {
            lock (_sync)
            {
                _advertisedAuthMethods.Remove(actorId);
            }

            return;
        }

        RecordAdvertisedAuthMethodsIfFingerprintMatches(actorId, fingerprint, epoch, methodIds);
    }

    internal void RecordAdvertisedAuthMethodsIfFingerprintMatches(
        ActorId actorId,
        AcpRuntimeBindingFingerprint fingerprint,
        long epoch,
        IReadOnlyList<string> methodIds)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(methodIds);

        // Atomic validate-and-publish under the selection lock. Holding the
        // selection lock while calling into the store's single-lock validate
        // API produces a deadlock-safe acquisition order: selection -> store.
        // The store never acquires the selection lock; its BindingChanged
        // notification is published after the store lock is released, so no
        // inverse order is ever introduced.
        lock (_sync)
        {
            if (!_bindingStore.TryValidateAcpBindingFingerprint(actorId, fingerprint, epoch))
            {
                return;
            }

            _advertisedAuthMethods[actorId] = new AdvertisedAuthMethodCache(
                fingerprint,
                epoch,
                methodIds);
        }
    }

    public async Task RequestAuthenticateAsync(
        ActorId actorId,
        string methodId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(methodId))
        {
            throw new ArgumentException("Authentication method id is required.", nameof(methodId));
        }

        if (!_bindingStore.TryGetBinding(actorId, out var binding)
            || binding.BackendId != AgentBackendIds.Acp)
        {
            throw new InvalidOperationException("ACP authentication requires an explicit ACP binding.");
        }

        // Capture the advertised-method cache entry atomically with the
        // fingerprint/epoch it was published for. The pair is required for
        // the conditional runtime-auth mutation below.
        if (!TryCaptureAdvertisedAuthMethodCache(
                actorId,
                out var advertisedFingerprint,
                out var advertisedEpoch,
                out var advertised))
        {
            throw new InvalidOperationException(
                "ACP authentication is unavailable because no methods were advertised.");
        }

        if (!advertised.Any(method => string.Equals(method, methodId, StringComparison.Ordinal)))
        {
            if (InvalidMethodPublicationDelayForTestAsync is not null)
            {
                await InvalidMethodPublicationDelayForTestAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            // Conditional runtime-only mutation: the caller's identity (the
            // fingerprint/epoch the advertised methods were published for)
            // must still match the current binding. A binding change between
            // the cache capture and this call — including exact
            // unbind/rebind cycles that reset the revision with the same
            // durable fields — must not rewrite the replacement binding's
            // runtime authentication state.
            if (!_bindingStore.TrySetRuntimeAuthenticationIfFingerprintMatches(
                    actorId,
                    advertisedFingerprint,
                    advertisedEpoch,
                    methodId,
                    AgentAuthenticationConnectionState.Failed))
            {
                throw new InvalidOperationException(
                    "ACP authentication rejected: the binding changed while the requested method was being validated.");
            }

            throw new InvalidOperationException("Authentication method is not advertised by the agent.");
        }

        // Authenticate always requires the onboarding connection bridge. Production
        // DI registers it; unit tests must inject a double rather than relying on a
        // local rewrite that could silently claim protocol success.
        var onboarding = _onboardingResolver?.Invoke();
        if (onboarding is null)
        {
            throw new InvalidOperationException(
                "ACP authenticate requires the onboarding connection service.");
        }

        var result = await onboarding.AuthenticateAsync(actorId, methodId, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                result.Message ?? "ACP authenticate failed.");
        }
    }

    private void ClearAdvertisedAuthMethods(ActorId actorId)
    {
        lock (_sync)
        {
            _advertisedAuthMethods.Remove(actorId);
        }
    }

    internal void ClearAdvertisedAuthMethodsIfFingerprintMatches(
        ActorId actorId,
        AcpRuntimeBindingFingerprint fingerprint,
        long epoch)
    {
        // Remove only the cache entry whose stored fingerprint+epoch match
        // the caller. A stale cleanup from an old (unbound/rebound)
        // fingerprint+epoch must never erase a newer binding's advertised
        // methods. Returning empty for a stale cache is necessary but
        // insufficient: stale work must not overwrite or erase a newer valid
        // cache entry, so the compare-and-remove is performed under the
        // selection lock and against the stored entry's own pair.
        lock (_sync)
        {
            if (!_advertisedAuthMethods.TryGetValue(actorId, out var existing))
            {
                return;
            }

            if (existing.Epoch != epoch || !existing.Fingerprint.Equals(fingerprint))
            {
                return;
            }

            _advertisedAuthMethods.Remove(actorId);
        }
    }

    private void OnStoreBindingChanged(AgentActorBackendBindingChangedEvent change)
    {
        // Any successful durable mutation invalidates cached advertised methods
        // (bind, rebind/update, and unbind). Runtime auth is never durable.
        if (change.Kind is AgentActorBackendBindingMutationKind.Bind
            or AgentActorBackendBindingMutationKind.Update
            or AgentActorBackendBindingMutationKind.Unbind)
        {
            ClearAdvertisedAuthMethods(change.ActorId);
        }

        Action<AgentActorBackendBindingChangedEvent>[] handlers;
        lock (_sync)
        {
            handlers = _changeHandlers.ToArray();
        }

        foreach (var handler in handlers)
        {
            handler(change);
        }
    }

    private static string ResolveBackendLabel(AgentBackendId backendId) =>
        backendId == AgentBackendIds.Acp
            ? "ACP"
            : backendId == AgentBackendIds.NativeHarness
                ? "Native Harness"
                : backendId.Value;

    private static string BuildStatusCaption(
        AgentActorBackendBinding binding,
        IReadOnlyList<string> authMethods)
    {
        if (binding.BackendId == AgentBackendIds.NativeHarness)
        {
            return "Backend: Native Harness";
        }

        if (binding.BackendId != AgentBackendIds.Acp)
        {
            return $"Backend: {binding.BackendId.Value}";
        }

        var runtime = binding.AcpRuntime!;
        var caption = $"Backend: ACP ({runtime.ExecutablePath})";
        if (authMethods.Count == 0)
        {
            return $"{caption} · Auth: not required";
        }

        return binding.AuthenticationState switch
        {
            AgentAuthenticationConnectionState.Authenticated =>
                $"{caption} · Auth: {binding.SelectedAuthMethodId}",
            AgentAuthenticationConnectionState.Failed =>
                $"{caption} · Auth: failed",
            AgentAuthenticationConnectionState.PendingUserAction =>
                $"{caption} · Auth: select method",
            _ => $"{caption} · Auth: disconnected",
        };
    }

    private sealed class AdvertisedAuthMethodCache
    {
        public AdvertisedAuthMethodCache(
            AcpRuntimeBindingFingerprint fingerprint,
            long epoch,
            IReadOnlyList<string> methodIds)
        {
            Fingerprint = fingerprint;
            Epoch = epoch;
            // Defensive snapshot: a caller may still be holding the source
            // collection. Without the copy, a later mutation could rewrite
            // the cache's contents without a fresh publication.
            MethodIds = methodIds.ToArray();
        }

        public AcpRuntimeBindingFingerprint Fingerprint { get; }

        public long Epoch { get; }

        public IReadOnlyList<string> MethodIds { get; }
    }
}
