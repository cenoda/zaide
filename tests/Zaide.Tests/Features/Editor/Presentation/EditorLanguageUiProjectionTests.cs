using System;
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
    public void Subscribe_DispatchesApplyThroughUiDispatcher()
    {
        var subject = new Subject<int>();
        var dispatcher = new RecordingEditorUiDispatcher();
        var applied = 0;

        using var subscription = EditorLanguageUiProjection.Subscribe(
            subject,
            dispatcher,
            _ => applied++);

        subject.OnNext(1);
        subject.OnNext(2);

        Assert.Equal(2, dispatcher.InvokeCount);
        Assert.Equal(2, applied);
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

    private sealed class RecordingEditorUiDispatcher : IEditorUiDispatcher
    {
        public int InvokeCount { get; private set; }

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
    }
}
