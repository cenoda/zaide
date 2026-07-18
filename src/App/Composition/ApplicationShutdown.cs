using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.Language.Contracts;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.App.Composition;

/// <summary>
/// Ordered application-shutdown owner. Resolves fixed teardown participants from
/// the composition root provider and disposes each owner exactly once.
/// Not registered in DI; invoked from the desktop Exit path.
/// </summary>
internal static class ApplicationShutdown
{
    private static readonly TimeSpan ShutdownAsyncTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs the locked shutdown sequence. Callers must not fire-and-forget this
    /// method; desktop Exit and tests invoke it synchronously.
    /// </summary>
    internal static void Run(IServiceProvider services)
    {
        // Resolve workflow projections before any dispose so lazy DI never
        // constructs them against completed workflow subjects.
        var output = services.GetRequiredService<IProjectOutputService>();
        var buildDiagnostics = services.GetRequiredService<IBuildDiagnosticsService>();
        var testResults = services.GetRequiredService<ITestResultsService>();

        // Disconnect the debug adapter before workflow teardown so
        // adapter/debuggee process trees are never orphaned.
        DisposeOwner(services.GetRequiredService<IDebugSessionService>());

        // Tear down debug projection singletons after session disconnect and
        // before workflow disposal (Contract 3 ordering).
        DisposeResolvedService<DebugPanelViewModel>(services);
        DisposeResolvedService<DebugCurrentLocationViewModel>(services);
        DisposeResolvedService<EditorBreakpointViewModel>(services);
        DisposeResolvedService<DebugSessionViewModel>(services);

        // Cancel and kill workflow process trees before language teardown.
        DisposeOwner(services.GetRequiredService<IProjectWorkflowService>());

        // Release workflow subscriptions and complete projection subjects after
        // process kill. Language teardown stays after both.
        DisposeOwner(output);
        DisposeOwner(buildDiagnostics);
        DisposeOwner(testResults);

        // Language features before document sync / session teardown.
        DisposeOwner(services.GetRequiredService<ILanguageFormattingService>());
        DisposeOwner(services.GetRequiredService<ILanguageNavigationService>());
        DisposeOwner(services.GetRequiredService<ILanguageSymbolService>());
        DisposeOwner(services.GetRequiredService<ILanguageCompletionService>());
        DisposeOwner(services.GetRequiredService<ILanguageHoverService>());
        DisposeOwner(services.GetRequiredService<ILanguageDiagnosticsService>());
        DisposeOwner(services.GetRequiredService<ILanguageDocumentBridge>());
        DisposeOwner(services.GetRequiredService<ILanguageSessionService>());
        DisposeOwner(services.GetRequiredService<IProjectContextService>());

        // Optional hosts — may be absent in focused test providers.
        DisposeOwner(services.GetService<IFileTreeService>());
        DisposeOwner(services.GetService<ITerminalHost>());
    }

    private static void DisposeResolvedService<T>(IServiceProvider services)
        where T : class
    {
        // Get by type only — the resolved instance need not be assignable as T
        // in tests (recording doubles implement IDisposable under the service key).
        DisposeOwner(services.GetService(typeof(T)));
    }

    /// <summary>
    /// Exactly-once teardown: prefer <see cref="IAsyncDisposable"/> over
    /// <see cref="IDisposable"/>; never invoke both on the same instance.
    /// </summary>
    private static void DisposeOwner(object? owner)
    {
        if (owner is null)
            return;

        if (owner is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().Wait(ShutdownAsyncTimeout);
            return;
        }

        if (owner is IDisposable disposable)
            disposable.Dispose();
    }
}
