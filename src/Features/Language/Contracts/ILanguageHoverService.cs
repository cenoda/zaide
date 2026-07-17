using System;
using Zaide.Features.Language.Application;

namespace Zaide.Features.Language.Contracts;

/// <summary>
/// UI-independent active-document hover ownership.
/// </summary>
public interface ILanguageHoverService : IDisposable
{
    /// <summary>Current immutable hover snapshot.</summary>
    LanguageHoverSnapshot Current { get; }

    /// <summary>Emits each new <see cref="LanguageHoverSnapshot"/>.</summary>
    IObservable<LanguageHoverSnapshot> WhenChanged { get; }

    /// <summary>
    /// Schedules a dwell-delayed hover request for the active document at
    /// <paramref name="caretOffset"/>.
    /// </summary>
    void Schedule(string filePath, int caretOffset);

    /// <summary>Dismisses any visible or in-flight hover state.</summary>
    void Dismiss();
}
