using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Interactivity;
using Moq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.Views;

/// <summary>
/// Regression guard for the Source Control panel button wiring. The refresh and
/// commit buttons feed a <see cref="RoutedEventArgs"/> event stream into the
/// Unit-parameterized <c>RefreshCommand</c>/<c>CommitCommand</c> via
/// <c>InvokeCommand</c>. The event value must be projected to
/// <see cref="Unit.Default"/> first; otherwise <c>InvokeCommand</c> forwards the
/// <see cref="EventPattern{T}"/> as the command parameter and throws
/// <see cref="InvalidOperationException"/> at click time (observed as an
/// unhandled crash on Button.OnClick). These tests exercise the exact observable
/// pipelines the panel uses against the real ViewModel commands.
/// </summary>
public class SourceControlPanelCommandWiringTests
{
    static SourceControlPanelCommandWiringTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static SourceControlViewModel CreateViewModel()
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.Setup(g => g.ReadStatus(It.IsAny<string>()))
            .Returns(new RepositoryStatusSnapshot
            {
                CurrentBranchName = "main",
                Branches = Array.Empty<GitBranch>(),
                Changes = Array.Empty<FileChange>(),
            });
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);
        var workspace = new Workspace();
        workspace.SetProjectFromPath("/ws");
        return new SourceControlViewModel(
            orchestrator,
            workspace,
            Mock.Of<IFileDiffService>(),
            Mock.Of<IGitMutationService>(),
            git.Object);
    }

    private static EventPattern<RoutedEventArgs> Click() =>
        new(new object(), new RoutedEventArgs());

    [Fact]
    public void RefreshButtonPipeline_WithoutUnitProjection_ThrowsParameterTypeMismatch()
    {
        // Documents the original crash: the RoutedEventArgs EventPattern is
        // forwarded as the command parameter, but RefreshCommand expects Unit.
        var vm = CreateViewModel();
        var clicks = new Subject<EventPattern<RoutedEventArgs>>();
        using var sub = clicks.InvokeCommand(vm, x => x.RefreshCommand);

        Assert.Throws<InvalidOperationException>(() => clicks.OnNext(Click()));
    }

    [Fact]
    public void RefreshButtonPipeline_WithUnitProjection_ExecutesWithoutThrowing()
    {
        var vm = CreateViewModel();
        var clicks = new Subject<EventPattern<RoutedEventArgs>>();
        using var sub = clicks.Select(_ => Unit.Default).InvokeCommand(vm, x => x.RefreshCommand);

        var ex = Record.Exception(() => clicks.OnNext(Click()));

        Assert.Null(ex);
        Assert.Equal(SnapshotRefreshStatus.Success, vm.LastRefreshStatus);
    }

    [Fact]
    public void CommitButtonPipeline_WithUnitProjection_ExecutesWithoutThrowing()
    {
        var vm = CreateViewModel();
        var clicks = new Subject<EventPattern<RoutedEventArgs>>();
        using var sub = clicks.Select(_ => Unit.Default).InvokeCommand(vm, x => x.PrimaryActionCommand);

        // Empty-message path runs synchronously to the CommitError guard and must
        // not throw when the click event fires.
        var ex = Record.Exception(() => clicks.OnNext(Click()));

        Assert.Null(ex);
        Assert.Equal("Commit message cannot be empty.", vm.CommitError);
    }
}
