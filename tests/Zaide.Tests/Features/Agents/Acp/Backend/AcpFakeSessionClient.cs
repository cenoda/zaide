using System;
using System.Collections.Generic;
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

    public Task<AcpNegotiatedCapabilities> InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _negotiated = _script.CreateNegotiatedCapabilities();
        return Task.FromResult(_negotiated);
    }

    public Task<string> CreateSessionAsync(string absoluteWorkingDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AcpSessionValidation.RequireAbsoluteWorkingDirectory(absoluteWorkingDirectory);
        _activeSessionId = _script.SessionId;
        return Task.FromResult(_script.SessionId);
    }

    public Task<AcpPromptTurnResult> PromptAsync(
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

            var response = _inboundHandler(inbound.Request, cancellationToken).GetAwaiter().GetResult();
            inbound.ResponseCallback?.Invoke(response);
        }

        return Task.FromResult(_script.CreatePromptTurnResult());
    }

    public Task CancelPromptAsync(string sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task AuthenticateAsync(string methodId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(methodId))
        {
            throw new ArgumentException("Auth method id is required.", nameof(methodId));
        }

        AuthenticateCallCount++;
        LastAuthenticateMethodId = methodId;
        if (_script.AuthenticateShouldFail)
        {
            throw new AcpProtocolException("ACP authenticate failed: simulated failure.");
        }

        return Task.CompletedTask;
    }

    public Task LogoutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogoutCallCount++;
        if (_script.LogoutShouldFail)
        {
            throw new AcpProtocolException("ACP logout failed: simulated failure.");
        }

        return Task.CompletedTask;
    }

    public int AuthenticateCallCount { get; private set; }

    public string? LastAuthenticateMethodId { get; private set; }

    public int LogoutCallCount { get; private set; }

    public void ConfigureActionBridge(
        AcpInboundClientRequestHandler? inboundHandler,
        AcpClientCapabilities advertisedCapabilities)
    {
        _inboundHandler = inboundHandler;
        _advertisedCapabilities = advertisedCapabilities
            ?? throw new ArgumentNullException(nameof(advertisedCapabilities));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class AcpFakeSessionScript
{
    public string SessionId { get; init; } = "fake-session-1";

    public string AgentName { get; init; } = "acp-fake-agent";

    public string AgentVersion { get; init; } = "phase-20-m3";

    public AcpStopReason StopReason { get; init; } = AcpStopReason.EndTurn;

    public string AgentMessageText { get; init; } = "hello from fake acp";

    public IReadOnlyList<AcpAuthMethod> AuthMethods { get; init; } = Array.Empty<AcpAuthMethod>();

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
