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

    public AcpFakeSessionClient(AcpFakeSessionScript script)
    {
        _script = script ?? throw new ArgumentNullException(nameof(script));
    }

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
        return Task.FromResult(_script.CreatePromptTurnResult());
    }

    public Task CancelPromptAsync(string sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
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

    public IReadOnlyList<AcpSessionUpdate> Updates { get; init; } = Array.Empty<AcpSessionUpdate>();

    public Action<IReadOnlyList<AcpContentBlock>>? CapturePrompt { get; init; }

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
            Array.Empty<AcpAuthMethod>(),
            new AcpImplementationInfo
            {
                Name = AgentName,
                Version = AgentVersion,
            });

    public AcpPromptTurnResult CreatePromptTurnResult() =>
        new(StopReason, Updates, AgentMessageText);
}
