using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Production factory that launches csharp-ls and completes initialize/initialized.
/// </summary>
public sealed class CsharpLsSessionFactory : ILanguageServerSessionFactory
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
