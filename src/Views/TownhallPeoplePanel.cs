using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using Zaide.Models;

namespace Zaide.Views;

/// <summary>
/// People panel for the Townhall sidebar.
/// Shows workspace agents with avatar circle, name, status dot, and optional warning icon.
/// Matches M0.5 palette and M3 spec: colored circle with initials, status colors, amber warning.
/// </summary>
public class TownhallPeoplePanel : Panel
{
    private readonly StackPanel _agentList;

    public TownhallPeoplePanel()
    {
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(12, 16, 12, 8),
            Children =
            {
                new TextBlock
                {
                    Text = "People",
                    Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
                    FontSize = 13,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                },
                new TextBlock
                {
                    Text = "\uD83D\uDD14",
                    FontSize = 14,
                    Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };

        _agentList = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        var scrollViewer = new ScrollViewer
        {
            Content = _agentList,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Children.Add(new DockPanel
        {
            Children =
            {
                header,
                scrollViewer
            }
        });

        // Dock: header at top, scrollable list fills rest
        DockPanel.SetDock(header, Dock.Top);
    }

    /// <summary>
    /// Populates the panel with the given agents.
    /// Called each time the agent list changes or is initialized.
    /// </summary>
    public void SetAgents(System.Collections.Generic.IEnumerable<WorkspaceAgent> agents)
    {
        _agentList.Children.Clear();

        foreach (var agent in agents)
        {
            _agentList.Children.Add(CreateAgentRow(agent));
        }
    }

    private static Border CreateAgentRow(WorkspaceAgent agent)
    {
        // Avatar circle with initials
        var initials = agent.Name.Length > 0
            ? agent.Name[..1].ToUpperInvariant()
            : "?";

        var avatarCircle = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(9999),
            Background = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
            Child = new TextBlock
            {
                Text = initials,
                Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        // Status dot
        var statusBrushKey = agent.Status switch
        {
            AgentStatus.Active => "SuccessBrush",
            AgentStatus.Busy => "BusyBrush",
            AgentStatus.Idle => "IdleBrush",
            _ => "IdleBrush"
        };

        var statusDot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(9999),
            Background = (IBrush?)Application.Current!.Resources[statusBrushKey],
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 1, 1)
        };

        var avatarPanel = new Panel
        {
            Width = 32,
            Height = 32,
            Children = { avatarCircle, statusDot }
        };

        // Name text
        var nameText = new TextBlock
        {
            Text = agent.Name,
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Status label
        var statusLabel = new TextBlock
        {
            Text = agent.Status switch
            {
                AgentStatus.Active => "active",
                AgentStatus.Busy => "busy",
                AgentStatus.Idle => "idle",
                _ => "idle"
            },
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };

        // Role label
        var roleLabel = new TextBlock
        {
            Text = agent.Role,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };

        var nameAndMeta = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 0,
            Children =
            {
                nameText,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Children = { roleLabel, statusLabel }
                }
            }
        };

        var contentStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { avatarPanel, nameAndMeta }
        };

        // Warning icon if HasWarning
        if (agent.HasWarning)
        {
            var warningIcon = new TextBlock
            {
                Text = "\u26A0",
                FontSize = 14,
                Foreground = (IBrush?)Application.Current!.Resources["WarningBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            contentStack.Children.Add(warningIcon);
        }

        var row = new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            Child = contentStack,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        // Hover effect
        row.PointerEntered += (_, _) =>
        {
            row.Background = new SolidColorBrush(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
            row.CornerRadius = new CornerRadius(4);
        };
        row.PointerExited += (_, _) =>
        {
            row.Background = null;
            row.CornerRadius = new CornerRadius(0);
        };

        return row;
    }
}