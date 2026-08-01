using System;
using System.Collections.Generic;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Marshals language-intelligence snapshot observers onto the editor UI thread.
/// Uses non-blocking <see cref="IEditorUiDispatcher.Post"/> with dual-slot
/// coalescing so rapid snapshot floods (scroll, caret, Idle churn) do not stall
/// publishers or backlog the UI thread with obsolete applies, while still
/// delivering a predecessor terminal snapshot that is immediately followed by a
/// newer one (e.g. Empty/Failed feedback then Idle from PublishTerminal).
/// </summary>
internal static class EditorLanguageUiProjection
{
    internal static IDisposable Subscribe<T>(
        IObservable<T> source,
        IEditorUiDispatcher dispatcher,
        Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(apply);

        var gate = new object();
        // Dual-slot coalesce: at most one superseded predecessor plus the latest.
        // Pure single-slot latest-wins would drop Empty/Failed when PublishTerminal
        // immediately follows with Idle before the UI thread drains.
        T? predecessor = default;
        var hasPredecessor = false;
        T? pending = default;
        var hasPending = false;
        var isPosted = false;
        var comparer = EqualityComparer<T>.Default;

        return source.Subscribe(snapshot =>
        {
            var shouldPost = false;
            lock (gate)
            {
                if (!hasPending)
                {
                    pending = snapshot;
                    hasPending = true;
                }
                else if (comparer.Equals(pending, snapshot))
                {
                    // Identical flood (e.g. repeated Idle) — keep one slot.
                    pending = snapshot;
                }
                else
                {
                    // Supersede: prior latest becomes the sole predecessor (drop older).
                    predecessor = pending;
                    hasPredecessor = true;
                    pending = snapshot;
                }

                if (!isPosted)
                {
                    isPosted = true;
                    shouldPost = true;
                }
            }

            if (!shouldPost)
                return;

            dispatcher.Post(() =>
            {
                while (true)
                {
                    T? first = default;
                    var deliverFirst = false;
                    T? second = default;
                    var deliverSecond = false;

                    lock (gate)
                    {
                        if (!hasPending && !hasPredecessor)
                        {
                            isPosted = false;
                            return;
                        }

                        if (hasPredecessor)
                        {
                            first = predecessor;
                            deliverFirst = true;
                            predecessor = default;
                            hasPredecessor = false;
                        }

                        if (hasPending)
                        {
                            second = pending;
                            deliverSecond = true;
                            pending = default;
                            hasPending = false;
                        }
                    }

                    if (deliverFirst)
                        apply(first!);

                    if (deliverSecond)
                        apply(second!);

                    // Loop if more snapshots arrived while applying.
                }
            });
        });
    }
}
