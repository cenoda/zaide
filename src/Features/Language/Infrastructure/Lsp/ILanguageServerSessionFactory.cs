using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Language.Infrastructure.Lsp;

/// <summary>
/// Starts a language-server child process and completes LSP initialize/initialized.
/// </summary>
public interface ILanguageServerSessionFactory
{
    /// <summary>
    /// Launches csharp-ls, negotiates initialize, and sends initialized.
    /// </summary>
    Task<ILanguageServerSession> StartAsync(
        LanguageServerStartOptions options,
        CancellationToken cancellationToken);
}
