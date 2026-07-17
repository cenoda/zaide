using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Xunit;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.App.Composition;

namespace Zaide.Tests.Features.Townhall.Presentation;

public class TownhallChatPanelGroupingTests
{
    static TownhallChatPanelGroupingTests()
    {
        EnsureApplication();
    }

    [Fact]
    public void ThreeConsecutiveSameSender_RendersOneHeader()
    {
        var panel = new TownhallChatPanel();
        panel.SetMessages(new ObservableCollection<TownhallMessage>
        {
            CreateMessage("user-1", "User", "First", 0),
            CreateMessage("user-1", "User", "Second", 1),
            CreateMessage("user-1", "User", "Third", 2)
        });

        Assert.Equal(1, CountHeaders(panel));
        Assert.Equal(3, GetRenderedRows(panel).Count);
    }

    [Fact]
    public void SenderSwitch_RendersNewHeader()
    {
        var panel = new TownhallChatPanel();
        panel.SetMessages(new ObservableCollection<TownhallMessage>
        {
            CreateMessage("user-1", "User", "First", 0),
            CreateMessage("agent-1", "Agent", "Second", 1)
        });

        Assert.Equal(2, CountHeaders(panel));
    }

    [Fact]
    public void FiveMinuteGap_StillRendersNewHeader()
    {
        var panel = new TownhallChatPanel();
        panel.SetMessages(new ObservableCollection<TownhallMessage>
        {
            CreateMessage("user-1", "User", "First", 0),
            CreateMessage("user-1", "User", "Second", 5)
        });

        Assert.Equal(2, CountHeaders(panel));
    }

    [Fact]
    public void DifferentSenders_AlwaysRenderHeaders()
    {
        var panel = new TownhallChatPanel();
        panel.SetMessages(new ObservableCollection<TownhallMessage>
        {
            CreateMessage("user-1", "User", "First", 0),
            CreateMessage("agent-1", "Agent", "Second", 1),
            CreateMessage("agent-2", "Reviewer", "Third", 2)
        });

        Assert.Equal(3, CountHeaders(panel));
    }

    private static TownhallMessage CreateMessage(string senderId, string senderName, string content, int minuteOffset)
    {
        var timestamp = new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero).AddMinutes(minuteOffset);
        return new TownhallMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = senderId,
            SenderName = senderName,
            Content = content,
            Timestamp = timestamp
        };
    }

    private static int CountHeaders(TownhallChatPanel panel)
    {
        return GetRenderedRows(panel)
            .Count(row =>
                row.Child is StackPanel container &&
                container.Children.Count > 0 &&
                container.Children[0] is Grid { Name: "MessageHeaderRow" });
    }

    private static System.Collections.Generic.IReadOnlyList<Border> GetRenderedRows(TownhallChatPanel panel)
    {
        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(panel.Children));
        var messageList = Assert.IsType<StackPanel>(scrollViewer.Content);
        return messageList.Children.OfType<Border>().ToList();
    }

    private static void EnsureApplication()
    {
        if (Application.Current is global::Zaide.App.Composition.App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
            {
                app.Initialize();
            }

            return;
        }

        var createdApp = new global::Zaide.App.Composition.App();
        createdApp.Initialize();
    }
}
