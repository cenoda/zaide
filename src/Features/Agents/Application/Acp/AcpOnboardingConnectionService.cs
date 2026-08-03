using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Production ACP onboarding connection service: probe (launch + initialize +
/// identity verify without prompt session), real authenticate bridge, and
/// capability-gated logout. Runtime-only state; durable binding unchanged.
/// </summary>
internal sealed class AcpOnboardingConnectionService : IAcpOnboardingConnectionService, IAsyncDisposable, IDisposable
{
    private readonly IAgentActorBackendBindingStore _bindingStore;
    private readonly IAgentActorBackendSelectionService _selectionService;
    private readonly IAcpProcessLauncher? _processLauncher;
    private readonly IWorkspaceActionAuthority? _workspaceAuthority;
    private readonly IAgentActorActiveRunQuery? _activeRunQuery;
    private readonly Func<AcpRuntimeIdentity, string, CancellationToken, Task<IAcpSessionClient>>? _clientFactory;
    private readonly Dictionary<ActorId, RuntimeConnection> _connections = new();
    private readonly List<Task> _trackedDisposalTasks = new();
    private readonly object _sync = new();
    private bool _disposed;

    /// <summary>Test seam: delay between probe identity validation and publication.</summary>
    internal Func<CancellationToken, Task>? ProbePublicationDelayForTestAsync;

    /// <summary>Test seam: delay between protocol authenticate and runtime publication.</summary>
    internal Func<CancellationToken, Task>? AuthenticatePublicationDelayForTestAsync;

    public AcpOnboardingConnectionService(
        IAgentActorBackendBindingStore bindingStore,
        IAgentActorBackendSelectionService selectionService,
        IAcpProcessLauncher processLauncher,
        IWorkspaceActionAuthority? workspaceAuthority = null,
        IAgentActorActiveRunQuery? activeRunQuery = null)
        : this(
            bindingStore,
            selectionService,
            processLauncher,
            workspaceAuthority,
            activeRunQuery,
            clientFactory: null)
    {
    }

    /// <summary>
    /// Test constructor that injects a session client factory (no process launch).
    /// </summary>
    internal AcpOnboardingConnectionService(
        IAgentActorBackendBindingStore bindingStore,
        IAgentActorBackendSelectionService selectionService,
        IWorkspaceActionAuthority? workspaceAuthority,
        IAgentActorActiveRunQuery? activeRunQuery,
        Func<AcpRuntimeIdentity, string, CancellationToken, Task<IAcpSessionClient>> clientFactory)
        : this(
            bindingStore,
            selectionService,
            processLauncher: null,
            workspaceAuthority,
            activeRunQuery,
            clientFactory)
    {
    }

    private AcpOnboardingConnectionService(
        IAgentActorBackendBindingStore bindingStore,
        IAgentActorBackendSelectionService selectionService,
        IAcpProcessLauncher? processLauncher,
        IWorkspaceActionAuthority? workspaceAuthority,
        IAgentActorActiveRunQuery? activeRunQuery,
        Func<AcpRuntimeIdentity, string, CancellationToken, Task<IAcpSessionClient>>? clientFactory)
    {
        _bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
        _selectionService = selectionService
            ?? throw new ArgumentNullException(nameof(selectionService));
        _processLauncher = processLauncher;
        _workspaceAuthority = workspaceAuthority;
        _activeRunQuery = activeRunQuery;
        _clientFactory = clientFactory;
        if (_processLauncher is null && _clientFactory is null)
        {
            throw new ArgumentException("Process launcher or client factory is required.");
        }

        _bindingStore.BindingChanged += OnBindingChanged;
    }

    public async Task<AcpOnboardingProbeResult> ProbeAsync(
        ActorId actorId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await AwaitTrackedDisposalsAsync().ConfigureAwait(false);

        if (!_bindingStore.TryGetBinding(actorId, out var binding)
            || binding.BackendId != AgentBackendIds.Acp
            || binding.AcpRuntime is null)
        {
            return AcpOnboardingProbeResult.Failed(
                actorId,
                "ACP probe requires a durable ACP binding.");
        }

        // Atomically capture the probe-start fingerprint and epoch before
        // launching the client. The pair is locked for the lifetime of the
        // probe: any later bind/update/unbind that bumps the epoch or rewrites
        // fingerprint fields must invalidate this in-flight probe, including
        // exact unbind/rebind cycles that reset the revision to 1 with the same
        // durable fields.
        if (!_bindingStore.TryCaptureAcpBindingFingerprint(
                actorId,
                out var identityAtProbe,
                out var epochAtProbe))
        {
            return AcpOnboardingProbeResult.Failed(
                actorId,
                "ACP probe requires a durable ACP binding.");
        }

        if (!AcpWorkspaceWorkingDirectory.TryResolve(_workspaceAuthority, out var workspaceRoot))
        {
            MarkStaleIfFingerprintMatches(
                actorId,
                identityAtProbe,
                epochAtProbe,
                "No valid workspace is available for ACP configuration.");
            return AcpOnboardingProbeResult.Failed(
                actorId,
                "No valid workspace is available for ACP configuration.");
        }

        var runtime = binding.AcpRuntime;
        // Production process launch requires the executable on disk. Test client
        // factories may bind synthetic paths without creating a real binary.
        if (_clientFactory is null && !File.Exists(runtime.ExecutablePath))
        {
            MarkStaleIfFingerprintMatches(
                actorId,
                identityAtProbe,
                epochAtProbe,
                "ACP executable was not found.");
            return AcpOnboardingProbeResult.Failed(
                actorId,
                $"ACP executable was not found at '{runtime.ExecutablePath}'.");
        }

        await DisposeConnectionAsync(actorId).ConfigureAwait(false);

        IAcpSessionClient? client = null;
        try
        {
            if (_clientFactory is not null)
            {
                client = await _clientFactory(runtime, workspaceRoot, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var options = new AcpProcessLaunchOptions(runtime.ExecutablePath, runtime.Arguments)
                {
                    WorkingDirectory = workspaceRoot,
                    AllowlistedEnvironment = AcpProcessEnvironmentPolicy.CreateAllowlistedEnvironment(),
                };

                var host = await AcpStdioProcessHost.StartAsync(
                        options,
                        _processLauncher!,
                        cancellationToken)
                    .ConfigureAwait(false);
                client = new AcpStdioProcessSessionClient(host);
            }

            // Configuration probe: initialize only — never create a prompt session.
            var negotiated = await client.InitializeAsync(cancellationToken).ConfigureAwait(false);
            var observedName = negotiated.AgentInfo?.Name ?? string.Empty;
            var observedVersion = negotiated.AgentInfo?.Version ?? string.Empty;

            // Validate the *original* probe-start pair — never re-capture the
            // current fingerprint or epoch. A concurrent bind/update/unbind
            // (including exact unbind/rebind) bumps the epoch, and the
            // subsequent probe must be invalidated even if the new binding's
            // durable fields happen to match the originals byte-for-byte.
            if (!_bindingStore.TryValidateAcpBindingFingerprint(
                    actorId,
                    identityAtProbe,
                    epochAtProbe))
            {
                await DisposeClientSafelyAsync(client).ConfigureAwait(false);
                client = null;
                return AcpOnboardingProbeResult.Failed(
                    actorId,
                    "ACP binding changed during configuration probe.");
            }

            // Compare observed name/version against the original fingerprint,
            // not against a re-captured current binding.
            if (!string.Equals(identityAtProbe.ExpectedAgentName, observedName, StringComparison.Ordinal)
                || !string.Equals(identityAtProbe.ExpectedAgentVersion, observedVersion, StringComparison.Ordinal))
            {
                await DisposeClientSafelyAsync(client).ConfigureAwait(false);
                client = null;
                MarkStaleIfFingerprintMatches(
                    actorId,
                    identityAtProbe,
                    epochAtProbe,
                    "ACP agent identity mismatch.");
                return AcpOnboardingProbeResult.Failed(
                    actorId,
                    "ACP agent identity mismatch for the durable binding.");
            }

            if (ProbePublicationDelayForTestAsync is not null)
            {
                await ProbePublicationDelayForTestAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!_bindingStore.TryValidateAcpBindingFingerprint(
                    actorId,
                    identityAtProbe,
                    epochAtProbe))
            {
                await DisposeClientSafelyAsync(client).ConfigureAwait(false);
                client = null;
                return AcpOnboardingProbeResult.Failed(
                    actorId,
                    "ACP binding changed during configuration probe.");
            }

            var methodIds = negotiated.AuthMethods
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            // Prefer explicit agentCapabilities.auth.logout when present on initialize;
            // otherwise fall back to non-empty negotiated auth methods. Logout UI also
            // requires a live post-probe connection (see IsLogoutSupported).
            var logoutSupported = ResolveLogoutSupported(
                negotiated.AgentCapabilities,
                methodIds);

            var authState = methodIds.Length == 0
                ? AgentAuthenticationConnectionState.NotRequired
                : AgentAuthenticationConnectionState.PendingUserAction;

            if (!TryPublishProbeOutcome(
                    actorId,
                    identityAtProbe,
                    epochAtProbe,
                    client,
                    methodIds,
                    logoutSupported,
                    workspaceRoot,
                    authState))
            {
                await DisposeClientSafelyAsync(client).ConfigureAwait(false);
                client = null;
                return AcpOnboardingProbeResult.Failed(
                    actorId,
                    "ACP binding changed during configuration probe.");
            }

            client = null; // ownership transferred to cache
            return AcpOnboardingProbeResult.Succeeded(
                actorId,
                methodIds,
                logoutSupported,
                observedName,
                observedVersion);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (client is not null)
            {
                await DisposeClientSafelyAsync(client).ConfigureAwait(false);
            }

            var redacted = Redact(ex.Message);
            MarkStaleIfFingerprintMatches(
                actorId,
                identityAtProbe,
                epochAtProbe,
                redacted);
            return AcpOnboardingProbeResult.Failed(actorId, redacted);
        }
    }

    public async Task<AcpOnboardingAuthResult> AuthenticateAsync(
        ActorId actorId,
        string methodId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await AwaitTrackedDisposalsAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(methodId))
        {
            return AcpOnboardingAuthResult.Failed(actorId, "Authentication method id is required.");
        }

        methodId = methodId.Trim();

        if (_activeRunQuery?.HasActiveRun(actorId) == true)
        {
            return AcpOnboardingAuthResult.Failed(
                actorId,
                "Cannot authenticate while the actor has an active run.",
                methodId);
        }

        RuntimeConnection? connection;
        lock (_sync)
        {
            _connections.TryGetValue(actorId, out connection);
        }

        if (connection is null)
        {
            return AcpOnboardingAuthResult.Failed(
                actorId,
                "ACP runtime connection is unavailable for authenticate.",
                methodId);
        }

        if (!_bindingStore.TryGetBinding(actorId, out var binding)
            || binding.BackendId != AgentBackendIds.Acp
            || !connection.Fingerprint.Matches(binding)
            || !_bindingStore.TryValidateAcpBindingFingerprint(
                actorId,
                connection.Fingerprint,
                connection.BindingEpoch))
        {
            await InvalidateConnectionAsync(actorId).ConfigureAwait(false);
            return AcpOnboardingAuthResult.Failed(
                actorId,
                "ACP binding no longer matches the cached runtime connection.",
                methodId);
        }

        if (connection.AuthMethodIds.Count == 0)
        {
            return AcpOnboardingAuthResult.Failed(
                actorId,
                "ACP authentication is unavailable because no methods were advertised.",
                methodId);
        }

        if (!connection.AuthMethodIds.Any(m => string.Equals(m, methodId, StringComparison.Ordinal)))
        {
            _bindingStore.TrySetRuntimeAuthenticationIfFingerprintMatches(
                actorId,
                connection.Fingerprint,
                connection.BindingEpoch,
                methodId,
                AgentAuthenticationConnectionState.Failed);
            return AcpOnboardingAuthResult.Failed(
                actorId,
                "Authentication method is not advertised by the agent.",
                methodId);
        }

        var fingerprintAtStart = connection.Fingerprint;
        var epochAtStart = connection.BindingEpoch;

        try
        {
            // Real protocol authenticate — not a local-only rewrite.
            await connection.Client.AuthenticateAsync(methodId, cancellationToken)
                .ConfigureAwait(false);

            if (AuthenticatePublicationDelayForTestAsync is not null)
            {
                await AuthenticatePublicationDelayForTestAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!_bindingStore.TrySetRuntimeAuthenticationIfFingerprintMatches(
                    actorId,
                    fingerprintAtStart,
                    epochAtStart,
                    methodId,
                    AgentAuthenticationConnectionState.Authenticated))
            {
                await InvalidateConnectionAsync(actorId).ConfigureAwait(false);
                return AcpOnboardingAuthResult.Failed(
                    actorId,
                    "ACP binding changed during authenticate.",
                    methodId);
            }

            return AcpOnboardingAuthResult.Succeeded(actorId, methodId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var redacted = Redact(ex.Message);
            _bindingStore.TrySetRuntimeAuthenticationIfFingerprintMatches(
                actorId,
                fingerprintAtStart,
                epochAtStart,
                methodId,
                AgentAuthenticationConnectionState.Failed);
            await InvalidateConnectionAsync(actorId).ConfigureAwait(false);
            return AcpOnboardingAuthResult.Failed(actorId, redacted, methodId);
        }
    }

    public async Task<AcpOnboardingLogoutResult> LogoutAsync(
        ActorId actorId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await AwaitTrackedDisposalsAsync().ConfigureAwait(false);

        if (_activeRunQuery?.HasActiveRun(actorId) == true)
        {
            return AcpOnboardingLogoutResult.Failed(
                actorId,
                "Cannot logout while the actor has an active run.");
        }

        if (!IsLogoutSupported(actorId))
        {
            return AcpOnboardingLogoutResult.Failed(
                actorId,
                "ACP logout is not advertised/supported for this agent.");
        }

        RuntimeConnection? connection;
        lock (_sync)
        {
            _connections.TryGetValue(actorId, out connection);
        }

        if (connection is null)
        {
            return AcpOnboardingLogoutResult.Failed(
                actorId,
                "ACP runtime connection is unavailable for logout.");
        }

        if (!_bindingStore.TryGetBinding(actorId, out var binding)
            || binding.BackendId != AgentBackendIds.Acp
            || !connection.Fingerprint.Matches(binding)
            || !_bindingStore.TryValidateAcpBindingFingerprint(
                actorId,
                connection.Fingerprint,
                connection.BindingEpoch))
        {
            await InvalidateConnectionAsync(actorId).ConfigureAwait(false);
            return AcpOnboardingLogoutResult.Failed(
                actorId,
                "ACP binding no longer matches the cached runtime connection.");
        }

        var fingerprintAtStart = connection.Fingerprint;
        var epochAtStart = connection.BindingEpoch;

        try
        {
            await connection.Client.LogoutAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ClearRuntimeIfFingerprintMatches(actorId, fingerprintAtStart, epochAtStart);
            await InvalidateConnectionAsync(actorId).ConfigureAwait(false);
            return AcpOnboardingLogoutResult.Failed(actorId, Redact(ex.Message));
        }

        if (!_bindingStore.TryValidateAcpBindingFingerprint(actorId, fingerprintAtStart, epochAtStart))
        {
            await InvalidateConnectionAsync(actorId).ConfigureAwait(false);
            return AcpOnboardingLogoutResult.Failed(
                actorId,
                "ACP binding changed during logout.");
        }

        ClearRuntimeIfFingerprintMatches(actorId, fingerprintAtStart, epochAtStart);
        await InvalidateConnectionAsync(actorId).ConfigureAwait(false);
        return AcpOnboardingLogoutResult.Succeeded(actorId);
    }

    public bool IsLogoutSupported(ActorId actorId)
    {
        lock (_sync)
        {
            return _connections.TryGetValue(actorId, out var connection)
                   && connection.LogoutSupported;
        }
    }

    public IReadOnlyList<string> GetNegotiatedAuthMethodIds(ActorId actorId)
    {
        lock (_sync)
        {
            return _connections.TryGetValue(actorId, out var connection)
                ? connection.AuthMethodIds
                : Array.Empty<string>();
        }
    }

    /// <summary>
    /// Test seam: awaits in-flight client disposals without performing onboarding work.
    /// </summary>
    internal Task AwaitTrackedDisposalsForTestAsync() =>
        AwaitTrackedDisposalsAsync();

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _bindingStore.BindingChanged -= OnBindingChanged;

        ActorId[] actors;
        lock (_sync)
        {
            actors = _connections.Keys.ToArray();
        }

        foreach (var actorId in actors)
        {
            await DisposeConnectionAsync(actorId).ConfigureAwait(false);
        }

        await AwaitTrackedDisposalsAsync().ConfigureAwait(false);
    }

    private void OnBindingChanged(AgentActorBackendBindingChangedEvent change)
    {
        if (change.Kind is AgentActorBackendBindingMutationKind.Bind
            or AgentActorBackendBindingMutationKind.Update
            or AgentActorBackendBindingMutationKind.Unbind)
        {
            DetachConnection(change.ActorId);
        }
    }

    private void DetachConnection(ActorId actorId)
    {
        IAcpSessionClient? client;
        lock (_sync)
        {
            if (!_connections.Remove(actorId, out var connection))
            {
                return;
            }

            client = connection.Client;
        }

        StartTrackedDisposal(client);
    }

    private void StartTrackedDisposal(IAcpSessionClient client)
    {
        var disposalTask = DisposeClientSafelyAsync(client);
        lock (_sync)
        {
            _trackedDisposalTasks.Add(disposalTask);
        }

        disposalTask.ContinueWith(
            task =>
            {
                lock (_sync)
                {
                    _trackedDisposalTasks.Remove(task);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task InvalidateConnectionAsync(ActorId actorId)
    {
        DetachConnection(actorId);
        await AwaitTrackedDisposalsAsync().ConfigureAwait(false);
    }

    private async Task AwaitTrackedDisposalsAsync()
    {
        Task[] pending;
        lock (_sync)
        {
            pending = _trackedDisposalTasks.ToArray();
        }

        foreach (var task in pending)
        {
            await task.ConfigureAwait(false);
        }
    }

    private bool TryPublishProbeOutcome(
        ActorId actorId,
        AcpRuntimeBindingFingerprint fingerprint,
        long bindingEpoch,
        IAcpSessionClient client,
        IReadOnlyList<string> methodIds,
        bool logoutSupported,
        string workspaceRoot,
        AgentAuthenticationConnectionState authState)
    {
        if (!_bindingStore.TryCommitAcpProbeRuntimeState(
                actorId,
                fingerprint,
                bindingEpoch,
                authState))
        {
            return false;
        }

        if (_selectionService is AgentActorBackendSelectionService concrete)
        {
            concrete.RecordAdvertisedAuthMethodsIfFingerprintMatches(
                actorId,
                fingerprint,
                bindingEpoch,
                methodIds);
        }

        lock (_sync)
        {
            if (!_bindingStore.TryValidateAcpBindingFingerprint(actorId, fingerprint, bindingEpoch))
            {
                return false;
            }

            _connections[actorId] = new RuntimeConnection(
                client,
                methodIds,
                logoutSupported,
                workspaceRoot,
                fingerprint,
                bindingEpoch);
            return true;
        }
    }

    private void MarkStaleIfFingerprintMatches(
        ActorId actorId,
        AcpRuntimeBindingFingerprint fingerprint,
        long epoch,
        string reason)
    {
        _ = reason;
        _bindingStore.TrySetRuntimeAuthenticationIfFingerprintMatches(
            actorId,
            fingerprint,
            epoch,
            selectedAuthMethodId: null,
            AgentAuthenticationConnectionState.Failed);

        if (_selectionService is AgentActorBackendSelectionService concrete)
        {
            concrete.ClearAdvertisedAuthMethodsIfFingerprintMatches(actorId, fingerprint, epoch);
        }
    }

    private void ClearRuntimeIfFingerprintMatches(
        ActorId actorId,
        AcpRuntimeBindingFingerprint fingerprint,
        long epoch)
    {
        _bindingStore.TrySetRuntimeAuthenticationIfFingerprintMatches(
            actorId,
            fingerprint,
            epoch,
            selectedAuthMethodId: null,
            AgentAuthenticationConnectionState.Disconnected);

        if (_selectionService is AgentActorBackendSelectionService concrete)
        {
            concrete.ClearAdvertisedAuthMethodsIfFingerprintMatches(actorId, fingerprint, epoch);
        }
    }

    private async Task DisposeConnectionAsync(ActorId actorId)
    {
        DetachConnection(actorId);
        await AwaitTrackedDisposalsAsync().ConfigureAwait(false);
    }

    private static async Task DisposeClientSafelyAsync(IAcpSessionClient client)
    {
        try
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _ = ex;
        }
    }

    private static string Redact(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return "ACP onboarding failed.";
        }

        // Reuse the transport redactor so presentation/application sources never
        // embed secret-shape patterns that adversarial gates forbid.
        var redacted = AcpStderrRedactor.Redact(message);
        if (redacted.Length > 400)
        {
            redacted = redacted[..400] + "…";
        }

        return redacted;
    }

    /// <summary>
    /// ACP schema: <c>agentCapabilities.auth.logout</c> as an object (including
    /// <c>{}</c>) advertises logout; null means not supported. When the property
    /// is absent, fall back to non-empty negotiated auth methods.
    /// </summary>
    private static bool ResolveLogoutSupported(
        AcpAgentCapabilities agentCapabilities,
        IReadOnlyList<string> methodIds)
    {
        var explicitLogout = TryResolveExplicitLogoutAdvertisement(agentCapabilities);
        if (explicitLogout.HasValue)
        {
            return explicitLogout.Value;
        }

        return methodIds.Count > 0;
    }

    private static bool? TryResolveExplicitLogoutAdvertisement(AcpAgentCapabilities agentCapabilities)
    {
        if (agentCapabilities.Auth is not { } auth
            || auth.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (auth.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!auth.TryGetProperty("logout", out var logout))
        {
            // auth object present but logout omitted — no explicit signal.
            return null;
        }

        if (logout.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        // Schema: supplying {} (or an object) means the agent supports logout.
        return logout.ValueKind == JsonValueKind.Object;
    }

    private sealed class RuntimeConnection
    {
        public RuntimeConnection(
            IAcpSessionClient client,
            IReadOnlyList<string> authMethodIds,
            bool logoutSupported,
            string workspaceRoot,
            AcpRuntimeBindingFingerprint fingerprint,
            long bindingEpoch)
        {
            Client = client;
            AuthMethodIds = authMethodIds;
            LogoutSupported = logoutSupported;
            WorkspaceRoot = workspaceRoot;
            Fingerprint = fingerprint;
            BindingEpoch = bindingEpoch;
        }

        public IAcpSessionClient Client { get; }

        public IReadOnlyList<string> AuthMethodIds { get; }

        public bool LogoutSupported { get; }

        public string WorkspaceRoot { get; }

        public AcpRuntimeBindingFingerprint Fingerprint { get; }

        public long BindingEpoch { get; }
    }
}
