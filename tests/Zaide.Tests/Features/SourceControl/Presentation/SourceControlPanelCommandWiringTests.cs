using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Interactivity;
using Moq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Infrastructure;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.SourceControl.Presentation;

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
        var workspace = new global::Zaide.Features.Workspace.Domain.Workspace();
        workspace.SetProjectFromPath("/ws");
        return new SourceControlViewModel(
            orchestrator,
            workspace,
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

    [Fact]
    public void SelectedBranchPipeline_ReappliesSelectionAfterRefresh()
    {
        // Mirrors the panel's WhenAnyValue(SelectedBranch) subscription: after
        // Branches.Clear() the ComboBox must receive the refreshed SelectedBranch.
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.Setup(g => g.ReadStatus(It.IsAny<string>()))
            .Returns(new RepositoryStatusSnapshot
            {
                CurrentBranchName = "main",
                Branches = new[] { new GitBranch("main", true), new GitBranch("dev") },
                Changes = Array.Empty<FileChange>(),
            });
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);
        var workspace = new global::Zaide.Features.Workspace.Domain.Workspace();
        workspace.SetProjectFromPath("/ws");
        var vm = new SourceControlViewModel(
            orchestrator,
            workspace,
            Mock.Of<IGitMutationService>(),
            git.Object);

        GitBranch? selected = vm.SelectedBranch;
        using var sub = vm.WhenAnyValue(x => x.SelectedBranch).Subscribe(b => selected = b);

        Assert.Same(vm.Branches[0], selected);

        vm.RefreshCommand.Execute().Wait();

        Assert.Same(vm.Branches[0], vm.SelectedBranch);
        Assert.Same(vm.Branches[0], selected);
    }

    [Fact]
    public void StageAllButtonPipeline_WithUnitProjection_DoesNotThrowWhenCanExecuteFalse()
    {
        // With no unstaged changes StageAllCommand cannot execute; InvokeCommand
        // must still accept the Unit-projected click stream without throwing.
        var vm = CreateViewModel();
        var clicks = new Subject<EventPattern<RoutedEventArgs>>();
        using var sub = clicks.Select(_ => Unit.Default).InvokeCommand(vm, x => x.StageAllCommand);

        var ex = Record.Exception(() => clicks.OnNext(Click()));

        Assert.Null(ex);
        Assert.False(vm.StageAllCommand.CanExecute.FirstAsync().Wait());
    }
}
