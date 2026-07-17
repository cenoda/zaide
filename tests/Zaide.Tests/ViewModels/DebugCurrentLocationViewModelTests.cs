using System;
using System.IO;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 12 M5 tests for selected-frame current execution location projection.
/// </summary>
public sealed class DebugCurrentLocationViewModelTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase12-m5-location-" + Guid.NewGuid().ToString("N"));

    static DebugCurrentLocationViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Directory.CreateDirectory(TempRoot);
    }

    [Fact]
    public async Task SelectedFrame_OpensSourceAndProjectsMarker()
    {
        var path = Path.Combine(TempRoot, "Program.cs");
        await File.WriteAllTextAsync(path, "line1\nline2\n");

        var (location, stack, subject) = CreateHarness(path);
        stack.SelectFrameCommand.Execute(new DebugStackFrameViewModel(10, "Main", path, 2)).Subscribe();
        await WaitForAsync(() => location.Marker?.Line == 2);

        Assert.Equal(path, location.ActiveDocumentPath);
        Assert.Equal(2, location.Marker?.Line);
        Assert.Null(location.StatusMessage);
        location.Dispose();
    }

    [Fact]
    public void MissingSource_ShowsUnavailableStatus()
    {
        var (location, stack, _) = CreateHarness(Path.Combine(TempRoot, "missing.cs"));
        stack.SelectFrameCommand.Execute(new DebugStackFrameViewModel(10, "Main", null, 1)).Subscribe();

        Assert.Null(location.Marker);
        Assert.Contains("unavailable", location.StatusMessage, StringComparison.OrdinalIgnoreCase);
        location.Dispose();
    }

    [Fact]
    public void Continue_ClearsMarker()
    {
        var path = Path.Combine(TempRoot, "clear.cs");
        File.WriteAllText(path, "only");

        var (location, _, subject) = CreateHarness(path);
        subject.OnNext(new DebugSessionSnapshot(
            DebugSessionState.Running,
            Generation: 1,
            ProgramPath: "/tmp/App.dll",
            WorkingDirectory: "/tmp",
            AdapterProcessId: 42,
            StopInfo: null,
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications));

        Assert.Null(location.Marker);
        Assert.Null(location.ActiveDocumentPath);
        location.Dispose();
    }

    private static (DebugCurrentLocationViewModel Location, DebugStackProjectionViewModel Stack, Subject<DebugSessionSnapshot> Subject)
        CreateHarness(string sourcePath)
    {
        var subject = new Subject<DebugSessionSnapshot>();
        var debug = new Moq.Mock<IDebugSessionService>();
        var stopped = new DebugSessionSnapshot(
            DebugSessionState.Stopped,
            Generation: 1,
            ProgramPath: "/tmp/App.dll",
            WorkingDirectory: "/tmp",
            AdapterProcessId: 42,
            StopInfo: new DapStoppedInfo("breakpoint", 1),
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications);
        var current = stopped;
        debug.SetupGet(s => s.Current).Returns(() => current);
        debug.SetupGet(s => s.WhenChanged).Returns(subject);
        subject.Subscribe(snapshot => current = snapshot);

        var services = new ServiceCollection();
        var workspace = new Workspace();
        services.AddSingleton(workspace);
        services.AddSingleton<IFileService>(new FileService());
        var sp = services.BuildServiceProvider();
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), workspace);

        var stack = new DebugStackProjectionViewModel(debug.Object);
        var location = new DebugCurrentLocationViewModel(debug.Object, stack, editorTabs);
        location.Activate();
        return (location, stack, subject);
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
                return;

            await Task.Delay(20).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for current-location projection.");
    }
}