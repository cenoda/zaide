using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zaide.Features.Editor.Application;

namespace Zaide.Tests.Features.Editor.Application;

public sealed class EditorStateSnapshotServiceTests
{
    [Fact]
    public void InitialSnapshot_IsEmpty()
    {
        using var service = new EditorStateSnapshotService();

        Assert.Equal(0, service.Current.Generation);
        Assert.Null(service.Current.ActiveFilePath);
        Assert.Empty(service.Current.OpenFilePaths);
    }

    [Fact]
    public void TryPublish_UpdatesCurrentAndEmitsWhenChanged()
    {
        using var service = new EditorStateSnapshotService();
        var observed = new List<EditorStateSnapshot>();
        using var subscription = service.WhenChanged.Subscribe(observed.Add);

        var published = service.TryPublish(new EditorStateSnapshot(
            generation: 0,
            activeFilePath: "/workspace/Program.cs",
            activeFileContent: "class Program {}",
            openFilePaths: new[] { "/workspace/Program.cs" }));

        Assert.True(published);
        Assert.Equal(1, service.Current.Generation);
        Assert.Equal("/workspace/Program.cs", service.Current.ActiveFilePath);
        Assert.Single(observed);
        Assert.Equal(1, observed[0].Generation);
    }

    [Fact]
    public void TryPublish_IncreasesGenerationMonotonically()
    {
        using var service = new EditorStateSnapshotService();

        Assert.True(service.TryPublish(new EditorStateSnapshot(generation: 0, activeFilePath: "a.cs")));
        Assert.True(service.TryPublish(new EditorStateSnapshot(generation: 0, activeFilePath: "b.cs")));

        Assert.Equal(2, service.Current.Generation);
        Assert.Equal("b.cs", service.Current.ActiveFilePath);
    }

    [Fact]
    public void TryPublish_DefensivelyCopiesOpenFilePaths()
    {
        using var service = new EditorStateSnapshotService();
        var openPaths = new List<string> { "/workspace/Program.cs" };

        Assert.True(service.TryPublish(new EditorStateSnapshot(
            generation: 0,
            openFilePaths: openPaths)));

        openPaths.Add("/workspace/Other.cs");

        Assert.Single(service.Current.OpenFilePaths);
        Assert.Equal("/workspace/Program.cs", service.Current.OpenFilePaths[0]);
    }

    [Fact]
    public void TryPublish_RejectsStaleGeneration()
    {
        using var service = new EditorStateSnapshotService();

        Assert.True(service.TryPublish(new EditorStateSnapshot(generation: 0, activeFilePath: "first.cs")));
        Assert.False(service.TryPublish(new EditorStateSnapshot(generation: 1, activeFilePath: "stale.cs")));

        Assert.Equal("first.cs", service.Current.ActiveFilePath);
        Assert.Equal(1, service.Current.Generation);
    }

    [Fact]
    public void TryPublish_AfterDispose_ReturnsFalse()
    {
        var service = new EditorStateSnapshotService();
        service.Dispose();

        Assert.False(service.TryPublish(new EditorStateSnapshot(generation: 0, activeFilePath: "after.cs")));
    }

    [Fact]
    public void Dispose_CompletesWhenChanged()
    {
        var service = new EditorStateSnapshotService();
        var completed = false;
        using var subscription = service.WhenChanged.Subscribe(
            _ => { },
            _ => { },
            () => completed = true);

        service.Dispose();

        Assert.True(completed);
    }
}
