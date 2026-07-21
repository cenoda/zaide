using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.UI.DesignSystem;
using Zaide.App.Shell;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// People panel for the Townhall sidebar.
/// Shows workspace agents with avatar circle, name, status dot, and optional warning icon.
/// Matches M0.5 palette and M3 spec: colored circle with initials, status colors, amber warning.
/// </summary>
public class TownhallPeoplePanel : Panel
{
    // M6: Hover overlay brush (faint white) shared with ChannelPanel.
    private static readonly Color HoverOverlay = Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF);

    private readonly StackPanel _agentList;
    private Action<ActorId>? _onOpenDirectMessage;

    public TownhallPeoplePanel()
    {
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingLg, LayoutTokens.SpacingMd, LayoutTokens.SpacingSm),
            Children =
            {
                TextStyles.Header("People"),
                IconFactory.Create(
                    "Icon.Bell",
                    (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                    14)
            }
        };

        _agentList = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingNone
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
    /// Sets the callback invoked when the user opens a direct message with an agent.
    /// </summary>
    public void SetOnOpenDirectMessage(Action<ActorId> onOpenDirectMessage)
    {
        _onOpenDirectMessage = onOpenDirectMessage;
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

    private Border CreateAgentRow(WorkspaceAgent agent)
    {
        var statusBrushKey = agent.Status switch
        {
            AgentStatus.Active => "SuccessBrush",
            AgentStatus.Busy => "BusyBrush",
            AgentStatus.Idle => "IdleBrush",
            _ => "IdleBrush"
        };

        var avatarPanel = TownhallAvatarFactory.Create(agent.Name, statusBrushKey, 32, 8);

        // Name text
        var nameText = TextStyles.Body(agent.Name);
        nameText.VerticalAlignment = VerticalAlignment.Center;

        // Status label
        var statusLabel = TextStyles.Caption(agent.Status switch {
            AgentStatus.Active => "active",
            AgentStatus.Busy => "busy",
            AgentStatus.Idle => "idle",
            _ => "idle"
        });
        statusLabel.VerticalAlignment = VerticalAlignment.Center;
        statusLabel.Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0);

        // Role label
        var roleLabel = TextStyles.Caption(agent.Role);
        roleLabel.VerticalAlignment = VerticalAlignment.Center;
        roleLabel.Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0);

        var nameAndMeta = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = LayoutTokens.SpacingNone,
            Children =
            {
                nameText,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = LayoutTokens.SpacingXs,
                    Children = { roleLabel, statusLabel }
                }
            }
        };

        var contentStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { avatarPanel, nameAndMeta }
        };

        // Warning icon if HasWarning
        if (agent.HasWarning)
        {
            var warningIcon = IconFactory.Create(
                "Icon.Warning",
                PaletteTokens.WarningBrush,
                14);
            warningIcon.Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0);
            contentStack.Children.Add(warningIcon);
        }

        var row = new Border
        {
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm),
            Child = contentStack,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        var openable = string.Equals(agent.Role, "agent", StringComparison.OrdinalIgnoreCase);
        AutomationProperties.SetName(
            row,
            openable
                ? $"Open direct conversation with {agent.Name}"
                : agent.Name);
        if (openable)
        {
            AutomationProperties.SetHelpText(
                row,
                "Opens or selects the Human↔Agent direct conversation in Townhall.");
        }

        // Hover effect
        row.PointerEntered += (_, _) =>
        {
            row.Background = new SolidColorBrush(HoverOverlay);
            row.CornerRadius = LayoutTokens.RadiusSm;
        };
        row.PointerExited += (_, _) =>
        {
            row.Background = null;
            row.CornerRadius = LayoutTokens.NoneRadius;
        };

        if (openable)
        {
            row.PointerPressed += (_, _) => _onOpenDirectMessage?.Invoke(agent.ActorId);
        }

        return row;
    }
}
