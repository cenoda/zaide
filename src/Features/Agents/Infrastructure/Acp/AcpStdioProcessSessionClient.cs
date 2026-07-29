using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// <see cref="IAcpSessionClient"/> adapter over <see cref="AcpStdioProcessHost"/>.
/// </summary>
internal sealed class AcpStdioProcessSessionClient : IAcpSessionClient
{
    private readonly AcpStdioProcessHost _host;
    private bool _disposed;

    public AcpStdioProcessSessionClient(AcpStdioProcessHost host)
    {
        _host = host ?? throw new System.ArgumentNullException(nameof(host));
    }

    public AcpNegotiatedCapabilities? NegotiatedCapabilities => _host.NegotiatedCapabilities;

    public string? ActiveSessionId => _host.ActiveSessionId;

    public Task<AcpNegotiatedCapabilities> InitializeAsync(CancellationToken cancellationToken) =>
        _host.InitializeAsync(cancellationToken);

    public Task<string> CreateSessionAsync(string absoluteWorkingDirectory, CancellationToken cancellationToken) =>
        _host.CreateSessionAsync(absoluteWorkingDirectory, cancellationToken);

    public Task<AcpPromptTurnResult> PromptAsync(
        string sessionId,
        IReadOnlyList<AcpContentBlock> prompt,
        CancellationToken cancellationToken) =>
        _host.PromptAsync(sessionId, prompt, cancellationToken);

    public Task CancelPromptAsync(string sessionId, CancellationToken cancellationToken) =>
        _host.CancelPromptAsync(sessionId, cancellationToken);

    public Task AuthenticateAsync(string methodId, CancellationToken cancellationToken) =>
        _host.AuthenticateAsync(methodId, cancellationToken);

    public void ConfigureActionBridge(
        AcpInboundClientRequestHandler? inboundHandler,
        AcpClientCapabilities advertisedCapabilities) =>
        _host.ConfigureActionBridge(inboundHandler, advertisedCapabilities);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _host.DisposeAsync().ConfigureAwait(false);
    }
}
