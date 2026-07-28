using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// <see cref="IAcpSessionClient"/> adapter over <see cref="AcpProtocolSession"/>.
/// </summary>
internal sealed class AcpProtocolSessionClient : IAcpSessionClient
{
    private readonly AcpProtocolSession _session;
    private bool _disposed;

    public AcpProtocolSessionClient(AcpProtocolSession session)
    {
        _session = session ?? throw new System.ArgumentNullException(nameof(session));
        _session.Start();
    }

    public AcpNegotiatedCapabilities? NegotiatedCapabilities => _session.NegotiatedCapabilities;

    public string? ActiveSessionId => _session.ActiveSessionId;

    public Task<AcpNegotiatedCapabilities> InitializeAsync(CancellationToken cancellationToken) =>
        _session.InitializeAsync(cancellationToken);

    public Task<string> CreateSessionAsync(string absoluteWorkingDirectory, CancellationToken cancellationToken) =>
        _session.CreateSessionAsync(absoluteWorkingDirectory, cancellationToken);

    public Task<AcpPromptTurnResult> PromptAsync(
        string sessionId,
        IReadOnlyList<AcpContentBlock> prompt,
        CancellationToken cancellationToken) =>
        _session.PromptAsync(sessionId, prompt, cancellationToken);

    public Task CancelPromptAsync(string sessionId, CancellationToken cancellationToken) =>
        _session.CancelPromptAsync(sessionId, cancellationToken);

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        return _session.DisposeAsync();
    }
}
