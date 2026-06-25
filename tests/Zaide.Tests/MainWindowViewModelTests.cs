using Xunit;
using Zaide.ViewModels;

namespace Zaide.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void InitialState_IsBottomPanelHidden()
    {
        var vm = new MainWindowViewModel();
        Assert.False(vm.IsBottomPanelVisible);
    }
}
