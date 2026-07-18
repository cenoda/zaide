using System;

namespace Zaide.App.Composition;

/// <summary>
/// Composition-boundary store for the ReactiveUI bootstrap provider.
/// Resolution and shutdown remain in <see cref="App"/>; this type only holds the root.
/// </summary>
internal static class CompositionRoot
{
    internal static IServiceProvider Services { get; set; } = null!;
}
