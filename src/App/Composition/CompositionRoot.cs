using System;

namespace Zaide.App.Composition;

/// <summary>
/// Composition-boundary store for the ReactiveUI bootstrap provider.
/// Bootstrap resolution remains in <see cref="App"/>; ordered shutdown lives in
/// <see cref="ApplicationShutdown"/>. This type only holds the root.
/// </summary>
internal static class CompositionRoot
{
    internal static IServiceProvider Services { get; set; } = null!;
}
