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
    private readonly object _sync = new();
    private bool _disposed;

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
    }

    public async Task<AcpOnboardingProbeResult> ProbeAsync(
        ActorId actorId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_bindingStore.TryGetBinding(actorId, out var binding)
            || binding.BackendId != AgentBackendIds.Acp
            || binding.AcpRuntime is null)
        {
            return AcpOnboardingProbeResult.Failed(
                actorId,
                "ACP probe requires a durable ACP binding.");
        }

        if (!AcpWorkspaceWorkingDirectory.TryResolve(_workspaceAuthority, out var workspaceRoot))
        {
            MarkStale(actorId, "No valid workspace is available for ACP configuration.");
            return AcpOnboardingProbeResult.Failed(
                actorId,
                "No valid workspace is available for ACP configuration.");
        }

        var runtime = binding.AcpRuntime;
        // Production process launch requires the executable on disk. Test client
        // factories may bind synthetic paths without creating a real binary.
        if (_clientFactory is null && !File.Exists(runtime.ExecutablePath))
        {
            MarkStale(actorId, "ACP executable was not found.");
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
            if (!string.Equals(binding.ExpectedAgentName, observedName, StringComparison.Ordinal)
                || !string.Equals(binding.ExpectedAgentVersion, observedVersion, StringComparison.Ordinal))
            {
                await client.DisposeAsync().ConfigureAwait(false);
                client = null;
                MarkStale(actorId, "ACP agent identity mismatch.");
                return AcpOnboardingProbeResult.Failed(
                    actorId,
                    "ACP agent identity mismatch for the durable binding.");
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

            if (_selectionService is AgentActorBackendSelectionService concrete)
            {
                concrete.RecordAdvertisedAuthMethods(actorId, methodIds);
            }

            var authState = methodIds.Length == 0
                ? AgentAuthenticationConnectionState.NotRequired
                : AgentAuthenticationConnectionState.PendingUserAction;
            _bindingStore.SetRuntimeAuthentication(
                actorId,
                selectedAuthMethodId: null,
                authState);

            lock (_sync)
            {
                _connections[actorId] = new RuntimeConnection(
                    client,
                    methodIds,
                    logoutSupported,
                    workspaceRoot);
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
                await client.DisposeAsync().ConfigureAwait(false);
            }

            var redacted = Redact(ex.Message);
            MarkStale(actorId, redacted);
            return AcpOnboardingProbeResult.Failed(actorId, redacted);
        }
    }

    public async Task<AcpOnboardingAuthResult> AuthenticateAsync(
        ActorId actorId,
        string methodId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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

        if (!_bindingStore.TryGetBinding(actorId, out var binding)
            || binding.BackendId != AgentBackendIds.Acp)
        {
            return AcpOnboardingAuthResult.Failed(
                actorId,
                "ACP authentication requires an explicit ACP binding.",
                methodId);
        }

        RuntimeConnection? connection;
        lock (_sync)
        {
            _connections.TryGetValue(actorId, out connection);
        }

        if (connection is null)
        {
            var probe = await ProbeAsync(actorId, cancellationToken).ConfigureAwait(false);
            if (!probe.IsSuccess)
            {
                return AcpOnboardingAuthResult.Failed(
                    actorId,
                    probe.Message ?? "ACP configuration probe failed before authenticate.",
                    methodId);
            }

            lock (_sync)
            {
                _connections.TryGetValue(actorId, out connection);
            }
        }

        if (connection is null)
        {
            return AcpOnboardingAuthResult.Failed(
                actorId,
                "ACP runtime connection is unavailable for authenticate.",
                methodId);
        }

        if (connection.AuthMethodIds.Count > 0
            && !connection.AuthMethodIds.Any(m => string.Equals(m, methodId, StringComparison.Ordinal)))
        {
            _bindingStore.SetRuntimeAuthentication(
                actorId,
                methodId,
                AgentAuthenticationConnectionState.Failed);
            return AcpOnboardingAuthResult.Failed(
                actorId,
                "Authentication method is not advertised by the agent.",
                methodId);
        }

        try
        {
            // Real protocol authenticate — not a local-only rewrite.
            await connection.Client.AuthenticateAsync(methodId, cancellationToken)
                .ConfigureAwait(false);

            _bindingStore.SetRuntimeAuthentication(
                actorId,
                methodId,
                AgentAuthenticationConnectionState.Authenticated);
            return AcpOnboardingAuthResult.Succeeded(actorId, methodId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var redacted = Redact(ex.Message);
            _bindingStore.SetRuntimeAuthentication(
                actorId,
                methodId,
                AgentAuthenticationConnectionState.Failed);
            return AcpOnboardingAuthResult.Failed(actorId, redacted, methodId);
        }
    }

    public async Task<AcpOnboardingLogoutResult> LogoutAsync(
        ActorId actorId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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

        try
        {
            await connection.Client.LogoutAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Still clear local runtime state; durable binding remains.
            ClearRuntime(actorId);
            await DisposeConnectionAsync(actorId).ConfigureAwait(false);
            return AcpOnboardingLogoutResult.Failed(actorId, Redact(ex.Message));
        }

        ClearRuntime(actorId);
        await DisposeConnectionAsync(actorId).ConfigureAwait(false);
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
        ActorId[] actors;
        lock (_sync)
        {
            actors = _connections.Keys.ToArray();
        }

        foreach (var actorId in actors)
        {
            await DisposeConnectionAsync(actorId).ConfigureAwait(false);
        }
    }

    private void MarkStale(ActorId actorId, string reason)
    {
        _ = reason;
        if (_bindingStore.TryGetBinding(actorId, out _))
        {
            _bindingStore.SetRuntimeAuthentication(
                actorId,
                selectedAuthMethodId: null,
                AgentAuthenticationConnectionState.Failed);
        }

        if (_selectionService is AgentActorBackendSelectionService concrete)
        {
            concrete.RecordAdvertisedAuthMethods(actorId, Array.Empty<string>());
        }
    }

    private void ClearRuntime(ActorId actorId)
    {
        if (_bindingStore.TryGetBinding(actorId, out _))
        {
            _bindingStore.SetRuntimeAuthentication(
                actorId,
                selectedAuthMethodId: null,
                AgentAuthenticationConnectionState.Disconnected);
        }

        if (_selectionService is AgentActorBackendSelectionService concrete)
        {
            concrete.RecordAdvertisedAuthMethods(actorId, Array.Empty<string>());
        }
    }

    private async Task DisposeConnectionAsync(ActorId actorId)
    {
        RuntimeConnection? connection;
        lock (_sync)
        {
            if (!_connections.Remove(actorId, out connection))
            {
                return;
            }
        }

        await connection.Client.DisposeAsync().ConfigureAwait(false);
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
            string workspaceRoot)
        {
            Client = client;
            AuthMethodIds = authMethodIds;
            LogoutSupported = logoutSupported;
            WorkspaceRoot = workspaceRoot;
        }

        public IAcpSessionClient Client { get; }

        public IReadOnlyList<string> AuthMethodIds { get; }

        public bool LogoutSupported { get; }

        public string WorkspaceRoot { get; }
    }
}
