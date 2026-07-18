using System;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.Features.Debugging.Presentation;

namespace Zaide.App.Composition.Registration;

internal static class DebuggingServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideDebugging(
        this IServiceCollection services)
    {
        // Phase 12 M1: UI-independent DAP adapter locator and session lifecycle core.
        services.AddSingleton<IDebugAdapterLocator>(_ =>
            new DebugAdapterLocator(Environment.GetEnvironmentVariable("ZAIDE_NETCOREDBG_PATH")));
        services.AddSingleton<IDebugAdapterSessionFactory, DebugAdapterSessionFactory>();
        services.AddSingleton<DebugSessionTimeoutPolicy>();
        services.AddSingleton<IDebugSessionService, DebugSessionService>();

        // Phase 12 M2: workspace-scoped persistent breakpoint storage.
        services.AddSingleton<IBreakpointService, BreakpointService>();

        // Debugging session/stack/location/panel/editor-breakpoint projections.
        services.AddSingleton<DebugSessionViewModel>();
        services.AddSingleton<DebugStackProjectionViewModel>();
        services.AddSingleton<DebugCurrentLocationViewModel>();
        services.AddSingleton<DebugPanelViewModel>();
        services.AddSingleton<EditorBreakpointViewModel>();

        return services;
    }
}
