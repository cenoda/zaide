using System.Linq;
using Xunit;
using Zaide.ViewModels;
using Zaide.Views;

namespace Zaide.Tests.ViewModels;

public class TownhallViewModelTests
{
    [Fact]
    public void SelectChannel_UpdatesActiveChannel_AndActiveFlags()
    {
        var vm = new TownhallViewModel();
        vm.LoadDemoData();

        vm.SelectChannel("ai-status");

        Assert.Equal("ai-status", vm.ActiveChannelId);
        Assert.True(vm.Channels.Single(c => c.Id == "ai-status").IsActive);
        Assert.False(vm.Channels.Single(c => c.Id == "townhall-main").IsActive);
    }

    [Fact]
    public void SendMessage_AppendsNewestAtBottom_AndClearsDraft()
    {
        var vm = new TownhallViewModel();
        vm.LoadDemoData();
        vm.SelectChannel("townhall-main");

        var beforeCount = vm.Messages.Count;
        vm.DraftText = "new update";
        vm.SendMessage();

        Assert.Equal(beforeCount + 1, vm.Messages.Count);
        Assert.Equal("new update", vm.Messages.Last().Content);
        Assert.Equal(string.Empty, vm.DraftText);
    }

    [Fact]
    public void SwitchingChannels_PreservesDraft_PerChannel()
    {
        var vm = new TownhallViewModel();
        vm.LoadDemoData();

        vm.SelectChannel("townhall-main");
        vm.DraftText = "draft in main";

        vm.SelectChannel("ai-status");
        Assert.Equal(string.Empty, vm.DraftText);

        vm.DraftText = "draft in ai";
        vm.SelectChannel("townhall-main");
        Assert.Equal("draft in main", vm.DraftText);

        vm.SelectChannel("ai-status");
        Assert.Equal("draft in ai", vm.DraftText);
    }

    [Fact]
    public void Constructor_DoesNotSeedDemoData_ByDefault()
    {
        var vm = new TownhallViewModel();

        Assert.Empty(vm.Channels);
        Assert.Empty(vm.Messages);
        Assert.Empty(vm.Agents);
        Assert.Equal(string.Empty, vm.ActiveChannelId);
    }
}
