using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Language.Application;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.App.Composition.Registration;

internal static class LanguageServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideLanguage(
        this IServiceCollection services)
    {
        // Phase 10 M1: C# language session (process + StreamJsonRpc transport).
        services.AddSingleton<ILanguageServerBinaryLocator, LanguageServerBinaryLocator>();
        services.AddSingleton<ILanguageServerSessionFactory, CsharpLsSessionFactory>();
        services.AddSingleton<ILanguageSessionService, LanguageSessionService>();
        services.AddSingleton<ILanguageDocumentBridge, LanguageDocumentBridge>();

        // Phase 10 M3: structured diagnostics + Problems projection.
        services.AddSingleton<ILanguageDiagnosticsService, LanguageDiagnosticsService>();

        // Phase 10 M4: active-document completion and hover.
        services.AddSingleton<ILanguageCompletionService, LanguageCompletionService>();
        services.AddSingleton<ILanguageHoverService, LanguageHoverService>();

        // Phase 10 M5: Go to Definition + document/workspace symbols.
        services.AddSingleton<ILanguageNavigationService, LanguageNavigationService>();
        services.AddSingleton<ILanguageSymbolService, LanguageSymbolService>();

        // Phase 10 M6: whole-document formatting + Format on Save coordination.
        services.AddSingleton<ILanguageFormattingService, LanguageFormattingService>();

        return services;
    }
}
