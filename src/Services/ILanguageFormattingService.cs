using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// UI-independent whole-document formatting ownership via
/// <c>textDocument/formatting</c>. Never mutates editor text; callers apply
/// accepted results through the EditorView/Document path only.
/// </summary>
public interface ILanguageFormattingService : IDisposable
{
    /// <summary>Current immutable formatting snapshot.</summary>
    LanguageFormattingSnapshot Current { get; }

    /// <summary>Emits each new <see cref="LanguageFormattingSnapshot"/>.</summary>
    IObservable<LanguageFormattingSnapshot> WhenChanged { get; }

    /// <summary>
    /// Requests whole-document formatting for <paramref name="filePath"/> when
    /// it is the active, open, version-matched document on a ready session.
    /// Returns an outcome that is safe to apply only when
    /// <see cref="LanguageFormattingOutcome.IsAccepted"/> is true.
    /// </summary>
    Task<LanguageFormattingOutcome> FormatDocumentAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
