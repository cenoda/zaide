using Xunit;
using Zaide.Features.Agents.Presentation.Transparency;

namespace Zaide.Tests.Features.Townhall.Presentation;

public sealed class Phase21TownhallAccessibilityTests
{
    [Fact]
    public void TransparencyManagement_ExposesScreenReaderAndKeyboardMetadata()
    {
        Assert.Equal("Agent transparency and memory management", AgentTransparencyManagementViewModel.AutomationName);
        Assert.Contains(
            "Keyboard navigation",
            AgentTransparencyManagementViewModel.AutomationHelpText,
            System.StringComparison.Ordinal);
        Assert.Contains(
            "screen-reader",
            AgentTransparencyManagementViewModel.AutomationHelpText,
            System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TransparencyManagement_BoundedPagingDefaultsAreStable()
    {
        Assert.Equal(64, AgentTransparencyManagementViewModel.DefaultPageSize);
        Assert.Equal(256, AgentTransparencyManagementViewModel.MaxPageSize);
    }
}
