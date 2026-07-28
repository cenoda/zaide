using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Testable ACP session transport boundary used by the backend adapter.
/// </summary>
internal interface IAcpSessionClient : IAsyncDisposable
{
    AcpNegotiatedCapabilities? NegotiatedCapabilities { get; }

    string? ActiveSessionId { get; }

    Task<AcpNegotiatedCapabilities> InitializeAsync(CancellationToken cancellationToken);

    Task<string> CreateSessionAsync(string absoluteWorkingDirectory, CancellationToken cancellationToken);

    Task<AcpPromptTurnResult> PromptAsync(
        string sessionId,
        IReadOnlyList<AcpContentBlock> prompt,
        CancellationToken cancellationToken);

    Task CancelPromptAsync(string sessionId, CancellationToken cancellationToken);

    void ConfigureActionBridge(
        AcpInboundClientRequestHandler? inboundHandler,
        AcpClientCapabilities advertisedCapabilities);
}
