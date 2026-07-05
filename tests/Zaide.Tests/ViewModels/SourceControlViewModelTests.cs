using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class SourceControlViewModelTests
{
    static SourceControlViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void InitialState_HasDemoBranches()
    {
        var state = new SourceControlState();
        var vm = new SourceControlViewModel(state);

        Assert.Equal(3, vm.Branches.Count);
        Assert.Equal("master", vm.CurrentBranchName);
    }

    [Fact]
    public void InitialState_HasUnstagedAndStagedChanges()
    {
        var state = new SourceControlState();
        var vm = new SourceControlViewModel(state);

        Assert.Equal(5, vm.UnstagedCount);
        Assert.Equal(2, vm.StagedCount);
    }

    [Fact]
    public void StageFile_MovesFromUnstagedToStaged()
    {
        var state = new SourceControlState();
        var vm = new SourceControlViewModel(state);

        var file = vm.UnstagedChanges.First();
        vm.StageFileCommand.Execute(file).Wait();

        Assert.DoesNotContain(file, vm.UnstagedChanges);
        Assert.Contains(file, vm.StagedChanges);
        Assert.True(file.IsStaged);
    }

    [Fact]
    public void UnstageFile_MovesFromStagedToUnstaged()
    {
        var state = new SourceControlState();
        var vm = new SourceControlViewModel(state);

        var file = vm.StagedChanges.First();
        vm.UnstageFileCommand.Execute(file).Wait();

        Assert.DoesNotContain(file, vm.StagedChanges);
        Assert.Contains(file, vm.UnstagedChanges);
        Assert.False(file.IsStaged);
    }

    [Fact]
    public void CommitCommand_ClearsStagedAndMessage()
    {
        var state = new SourceControlState();
        var vm = new SourceControlViewModel(state);

        vm.CommitMessage = "test commit";
        vm.CommitCommand.Execute().Wait();

        Assert.Empty(vm.StagedChanges);
        Assert.Equal(0, vm.StagedCount);
        Assert.Empty(vm.CommitMessage);
    }

    [Fact]
    public void SelectBranchCommand_UpdatesCurrentBranch()
    {
        var state = new SourceControlState();
        var vm = new SourceControlViewModel(state);

        var featureBranch = vm.Branches[1];
        vm.SelectBranchCommand.Execute(featureBranch).Wait();

        Assert.Equal(featureBranch, vm.SelectedBranch);
        Assert.Equal("feature/agent-ui", vm.CurrentBranchName);
    }
}