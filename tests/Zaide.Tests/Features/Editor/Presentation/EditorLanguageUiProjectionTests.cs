using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Xunit;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Phase 22.1 regression tests for language snapshot UI-thread projection.
/// </summary>
public sealed class EditorLanguageUiProjectionTests
{
    [Fact]
    public void Subscribe_DispatchesApplyThroughUiDispatcherPost()
    {
        var subject = new Subject<int>();
        var dispatcher = new RecordingEditorUiDispatcher();
        var applied = new List<int>();

        using var subscription = EditorLanguageUiProjection.Subscribe(
            subject,
            dispatcher,
            value => applied.Add(value));

        subject.OnNext(1);
        Assert.Empty(applied);
        Assert.Equal(1, dispatcher.PostCount);
        Assert.Equal(0, dispatcher.InvokeCount);

        dispatcher.Drain();

        Assert.Equal(new[] { 1 }, applied);
        Assert.Equal(0, dispatcher.InvokeCount);
    }

    [Fact]
    public void Subscribe_CoalescesRapidSnapshotsWithDualSlot()
    {
        var subject = new Subject<int>();
        var dispatcher = new RecordingEditorUiDispatcher();
        var applied = new List<int>();

        using var subscription = EditorLanguageUiProjection.Subscribe(
            subject,
            dispatcher,
            value => applied.Add(value));

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        // One Post while flooded; dual-slot keeps predecessor(2)+latest(3).
        Assert.Equal(1, dispatcher.PostCount);
        Assert.Empty(applied);

        dispatcher.Drain();

        Assert.Equal(new[] { 2, 3 }, applied);
        Assert.Equal(0, dispatcher.InvokeCount);
    }

    [Fact]
    public void Subscribe_CoalescesIdenticalFloodToSingleApply()
    {
        var subject = new Subject<int>();
        var dispatcher = new RecordingEditorUiDispatcher();
        var applied = new List<int>();

        using var subscription = EditorLanguageUiProjection.Subscribe(
            subject,
            dispatcher,
            value => applied.Add(value));

        subject.OnNext(7);
        subject.OnNext(7);
        subject.OnNext(7);

        Assert.Equal(1, dispatcher.PostCount);
        dispatcher.Drain();

        Assert.Equal(new[] { 7 }, applied);
    }

    [Fact]
    public void Subscribe_DeliversPredecessorThenLatest_ForTerminalThenIdlePattern()
    {
        // Mirrors LanguageNavigationService.PublishTerminal: Empty-like then Idle-like.
        var subject = new Subject<string>();
        var dispatcher = new RecordingEditorUiDispatcher();
        var applied = new List<string>();

        using var subscription = EditorLanguageUiProjection.Subscribe(
            subject,
            dispatcher,
            value => applied.Add(value));

        subject.OnNext("Empty");
        subject.OnNext("Idle");

        Assert.Equal(1, dispatcher.PostCount);
        dispatcher.Drain();

        Assert.Equal(new[] { "Empty", "Idle" }, applied);
    }

    [Fact]
    public void Subscribe_PostsAgainAfterDrainForNewSnapshots()
    {
        var subject = new Subject<int>();
        var dispatcher = new RecordingEditorUiDispatcher();
        var applied = new List<int>();

        using var subscription = EditorLanguageUiProjection.Subscribe(
            subject,
            dispatcher,
            value => applied.Add(value));

        subject.OnNext(1);
        dispatcher.Drain();
        subject.OnNext(2);
        dispatcher.Drain();

        Assert.Equal(2, dispatcher.PostCount);
        Assert.Equal(new[] { 1, 2 }, applied);
    }

    [Fact]
    public void Subscribe_RejectsNullArguments()
    {
        var subject = new Subject<int>();
        var dispatcher = new RecordingEditorUiDispatcher();

        Assert.Throws<ArgumentNullException>(() =>
            EditorLanguageUiProjection.Subscribe<int>(null!, dispatcher, _ => { }));
        Assert.Throws<ArgumentNullException>(() =>
            EditorLanguageUiProjection.Subscribe(subject, null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() =>
            EditorLanguageUiProjection.Subscribe(subject, dispatcher, null!));
    }

    /// <summary>
    /// Queues <see cref="IEditorUiDispatcher.Post"/> work so tests can assert
    /// coalescing before the UI-thread drain runs apply.
    /// </summary>
    private sealed class RecordingEditorUiDispatcher : IEditorUiDispatcher
    {
        private readonly Queue<Action> _posted = new();

        public int InvokeCount { get; private set; }

        public int PostCount { get; private set; }

        public void Invoke(Action action)
        {
            InvokeCount++;
            action();
        }

        public T Invoke<T>(Func<T> func)
        {
            InvokeCount++;
            return func();
        }

        public void Post(Action action)
        {
            PostCount++;
            _posted.Enqueue(action);
        }

        public void Drain()
        {
            while (_posted.Count > 0)
                _posted.Dequeue()();
        }
    }
}
