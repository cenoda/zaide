using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Language.Infrastructure.Lsp;

/// <summary>
/// Production factory that launches csharp-ls and completes initialize/initialized.
/// </summary>
internal sealed class CsharpLsSessionFactory : ILanguageServerSessionFactory
{
    /// <inheritdoc />
    public async Task<ILanguageServerSession> StartAsync(
        LanguageServerStartOptions options,
        CancellationToken cancellationToken)
    {
        var session = new CsharpLsSession(options);
        await session.StartAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }
}
