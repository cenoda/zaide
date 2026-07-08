using Zaide.ViewModels;

namespace Zaide.Views;

internal static class TerminalTabCloseBehavior
{
    public static bool ShouldHideBottomPanelInsteadOfClosing(ITerminalHost? host, TerminalTabViewModel tab)
    {
        return host is not null
            && host.Tabs.Count == 1
            && ReferenceEquals(host.Tabs[0], tab);
    }
}
