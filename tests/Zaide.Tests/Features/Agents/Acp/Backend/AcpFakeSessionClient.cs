using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Backend;

/// <summary>
/// Deterministic in-memory ACP session transport for Phase 20 backend tests.
/// </summary>
internal sealed class AcpFakeSessionClient : IAcpSessionClient
{
    private readonly AcpFakeSessionScript _script;
    private AcpNegotiatedCapabilities? _negotiated;
    private string? _activeSessionId;
    private AcpInboundClientRequestHandler? _inboundHandler;
    private AcpClientCapabilities _advertisedCapabilities = AcpClientCapabilityAdvertisement.CreateM1Profile();

    public AcpFakeSessionClient(AcpFakeSessionScript script)
    {
        _script = script ?? throw new ArgumentNullException(nameof(script));
    }

    public AcpClientCapabilities AdvertisedCapabilities => _advertisedCapabilities;

    public AcpNegotiatedCapabilities? NegotiatedCapabilities => _negotiated;

    public string? ActiveSessionId => _activeSessionId;

    /// <summary>
    /// Optional delay invoked before initialize completes.
    /// </summary>
    public Func<CancellationToken, Task>? InitializeDelayAsync { get; init; }

    /// <summary>
    /// Optional hold invoked inside PromptAsync (e.g. wait until cancelled).
    /// </summary>
    public Func<CancellationToken, Task>? PromptHoldAsync { get; init; }

    public async Task<AcpNegotiatedCapabilities> InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (InitializeDelayAsync is not null)
        {
            await InitializeDelayAsync(cancellationToken).ConfigureAwait(false);
        }

        _negotiated = _script.CreateNegotiatedCapabilities();
        return _negotiated;
    }

    public Task<string> CreateSessionAsync(string absoluteWorkingDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AcpSessionValidation.RequireAbsoluteWorkingDirectory(absoluteWorkingDirectory);
        _activeSessionId = _script.SessionId;
        return Task.FromResult(_script.SessionId);
    }

    public async Task<AcpPromptTurnResult> PromptAsync(
        string sessionId,
        IReadOnlyList<AcpContentBlock> prompt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(sessionId, _script.SessionId, StringComparison.Ordinal))
        {
            throw new AcpProtocolException("ACP session id mismatch.");
        }

        _script.CapturePrompt?.Invoke(prompt);

        foreach (var inbound in _script.InboundRequestsDuringPrompt)
        {
            if (_inboundHandler is null)
            {
                throw new AcpProtocolException("ACP inbound handler is not configured.");
            }

            var response = await _inboundHandler(inbound.Request, cancellationToken).ConfigureAwait(false);
            inbound.ResponseCallback?.Invoke(response);
        }

        if (PromptHoldAsync is not null)
        {
            await PromptHoldAsync(cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _script.CreatePromptTurnResult();
    }

    public int CancelPromptCallCount { get; private set; }

    public string? LastCancelSessionId { get; private set; }

    public bool? LastCancelTokenWasCancellationRequested { get; private set; }

    public Task CancelPromptAsync(string sessionId, CancellationToken cancellationToken)
    {
        CancelPromptCallCount++;
        LastCancelSessionId = sessionId;
        LastCancelTokenWasCancellationRequested = cancellationToken.IsCancellationRequested;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task AuthenticateAsync(string methodId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(methodId))
        {
            throw new ArgumentException("Auth method id is required.", nameof(methodId));
        }

        if (AuthenticateDelayAsync is not null)
        {
            await AuthenticateDelayAsync(cancellationToken).ConfigureAwait(false);
        }

        AuthenticateCallCount++;
        LastAuthenticateMethodId = methodId;
        if (_script.AuthenticateShouldFail)
        {
            throw new AcpProtocolException("ACP authenticate failed: simulated failure.");
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (LogoutDelayAsync is not null)
        {
            await LogoutDelayAsync(cancellationToken).ConfigureAwait(false);
        }

        LogoutCallCount++;
        if (_script.LogoutShouldFail)
        {
            throw new AcpProtocolException("ACP logout failed: simulated failure.");
        }
    }

    public int AuthenticateCallCount { get; private set; }

    public string? LastAuthenticateMethodId { get; private set; }

    public int LogoutCallCount { get; private set; }

    public int DisposeCallCount { get; private set; }

    /// <summary>
    /// Optional delay invoked before the simulated authenticate completes.
    /// </summary>
    public Func<CancellationToken, Task>? AuthenticateDelayAsync { get; init; }

    /// <summary>
    /// Optional delay invoked before dispose completes.
    /// </summary>
    public Func<Task>? DisposeDelayAsync { get; init; }

    /// <summary>
    /// Optional delay invoked before the simulated logout completes.
    /// </summary>
    public Func<CancellationToken, Task>? LogoutDelayAsync { get; init; }

    public void ConfigureActionBridge(
        AcpInboundClientRequestHandler? inboundHandler,
        AcpClientCapabilities advertisedCapabilities)
    {
        _inboundHandler = inboundHandler;
        _advertisedCapabilities = advertisedCapabilities
            ?? throw new ArgumentNullException(nameof(advertisedCapabilities));
    }

    public async ValueTask DisposeAsync()
    {
        if (DisposeDelayAsync is not null)
        {
            await DisposeDelayAsync().ConfigureAwait(false);
        }

        DisposeCallCount++;
    }
}

internal sealed class AcpFakeSessionScript
{
    public string SessionId { get; init; } = "fake-session-1";

    public string AgentName { get; init; } = "acp-fake-agent";

    public string AgentVersion { get; init; } = "phase-20-m3";

    public AcpStopReason StopReason { get; init; } = AcpStopReason.EndTurn;

    public string AgentMessageText { get; init; } = "hello from fake acp";

    public IReadOnlyList<AcpAuthMethod> AuthMethods { get; init; } = Array.Empty<AcpAuthMethod>();

    /// <summary>
    /// Optional <c>agentCapabilities.auth</c> wire fragment. When set, enables
    /// explicit logout advertisement tests via <c>auth.logout</c>.
    /// </summary>
    public JsonElement? AgentAuthCapabilities { get; init; }

    public bool AuthenticateShouldFail { get; init; }

    public bool LogoutShouldFail { get; init; }

    public IReadOnlyList<AcpSessionUpdate> Updates { get; init; } = Array.Empty<AcpSessionUpdate>();

    public Action<IReadOnlyList<AcpContentBlock>>? CapturePrompt { get; init; }

    public IReadOnlyList<AcpFakeInboundRequest> InboundRequestsDuringPrompt { get; init; } =
        Array.Empty<AcpFakeInboundRequest>();

    public AcpNegotiatedCapabilities CreateNegotiatedCapabilities() =>
        new(
            AcpSchemaProfile.WireProtocolVersion,
            new AcpAgentCapabilities
            {
                PromptCapabilities = new AcpPromptCapabilities
                {
                    EmbeddedContext = false,
                },
                Auth = AgentAuthCapabilities,
            },
            AuthMethods,
            new AcpImplementationInfo
            {
                Name = AgentName,
                Version = AgentVersion,
            });

    public AcpPromptTurnResult CreatePromptTurnResult() =>
        new(StopReason, Updates, AgentMessageText);
}

internal sealed class AcpFakeInboundRequest
{
    public AcpJsonRpcRequest Request { get; init; } = new();

    public Action<AcpJsonRpcResponse>? ResponseCallback { get; init; }
}
