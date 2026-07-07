using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Xunit;
using Zaide.Models;
using Zaide.Views;

namespace Zaide.Tests.Views;

public class TownhallChatPanelKindTests
{
    static TownhallChatPanelKindTests()
    {
        EnsureApplication();
    }

    [Fact]
    public void ChatKind_RendersFullBubblePath_WithPossibleHeader()
    {
        var panel = new TownhallChatPanel();
        panel.SetMessages(new ObservableCollection<TownhallMessage>
        {
            CreateChatMessage("user-1", "User", "Hello", 0)
        });

        var rows = GetRenderedRows(panel);
        Assert.Single(rows);
        // Full path: contains header Grid or content indented for avatar
        var row = rows[0];
        var hasHeader = row.Child is StackPanel sp && sp.Children.OfType<Grid>().Any(g => g.Name == "MessageHeaderRow");
        Assert.True(hasHeader || row.Child is StackPanel);
    }

    [Fact]
    public void NonChatKinds_RenderCompactPath()
    {
        var kinds = new[]
        {
            TownhallMessageKind.ChannelEvent,
            TownhallMessageKind.AgentAction,
            TownhallMessageKind.AgentThink,
            TownhallMessageKind.ToolCall,
            TownhallMessageKind.ToolResult,
            TownhallMessageKind.AgentError,
            TownhallMessageKind.System
        };

        foreach (var kind in kinds)
        {
            var panel = new TownhallChatPanel();
            panel.SetMessages(new ObservableCollection<TownhallMessage>
            {
                CreateMessage(kind, "sys", "System", "Event occurred", 0)
            });

            var rows = GetRenderedRows(panel);
            Assert.Single(rows);
            // Compact: horizontal StackPanel with icon + texts, no MessageHeaderRow
            var row = rows[0];
            Assert.False(row.Child is StackPanel sp && sp.Children.OfType<Grid>().Any(g => g.Name == "MessageHeaderRow"));
            Assert.True(row.Child is StackPanel { Orientation: Orientation.Horizontal });
        }
    }

    private static TownhallMessage CreateChatMessage(string senderId, string senderName, string content, int minuteOffset)
    {
        var timestamp = new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero).AddMinutes(minuteOffset);
        return new TownhallMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = senderId,
            SenderName = senderName,
            Content = content,
            Timestamp = timestamp,
            Kind = TownhallMessageKind.Chat
        };
    }

    private static TownhallMessage CreateMessage(TownhallMessageKind kind, string senderId, string senderName, string content, int minuteOffset)
    {
        var timestamp = new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero).AddMinutes(minuteOffset);
        return new TownhallMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = senderId,
            SenderName = senderName,
            Content = content,
            Timestamp = timestamp,
            Kind = kind
        };
    }

    private static System.Collections.Generic.IReadOnlyList<Border> GetRenderedRows(TownhallChatPanel panel)
    {
        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(panel.Children));
        var messageList = Assert.IsType<StackPanel>(scrollViewer.Content);
        return messageList.Children.OfType<Border>().ToList();
    }

    private static void EnsureApplication()
    {
        if (Application.Current is App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
            {
                app.Initialize();
            }

            return;
        }

        var createdApp = new App();
        createdApp.Initialize();
    }
}
