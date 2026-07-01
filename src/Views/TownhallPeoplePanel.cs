using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Models;

namespace Zaide.Views;

public class TownhallPeoplePanel : UserControl
{
    private readonly StackPanel _peoplePanel;

    public static readonly StyledProperty<IReadOnlyList<WorkspaceAgent>?> AgentsProperty =
        AvaloniaProperty.Register<TownhallPeoplePanel, IReadOnlyList<WorkspaceAgent>?>(nameof(Agents));

    public IReadOnlyList<WorkspaceAgent>? Agents
    {
        get => GetValue(AgentsProperty);
        set => SetValue(AgentsProperty, value);
    }

    public TownhallPeoplePanel()
    {
        // Section header
        var header = new TextBlock
        {
            Text = "PEOPLE",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            Margin = new Thickness(12, 10, 12, 8)
        };

        _peoplePanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Margin = new Thickness(6, 0, 6, 6)
        };

        var separator = new Border
        {
            Height = 1,
            Background = (IBrush?)Application.Current!.Resources["SurfaceBorder"]
        };

        Background = (IBrush?)Application.Current!.Resources["PanelDeep"];

        Content = new DockPanel
        {
            Children =
            {
                new Border { Child = header, [DockPanel.DockProperty] = Dock.Top },
                separator,
                _peoplePanel
            }
        };

        this.GetObservable(AgentsProperty).Subscribe(_ => RenderAgents(), _ => { }, () => { });
    }

    private void RenderAgents()
    {
        _peoplePanel.Children.Clear();
        if (Agents is null)
            return;

        foreach (var agent in Agents)
        {
            // Avatar circle with first letter
            var avatarLetter = agent.Name.Length > 0 ? agent.Name[0].ToString() : "?";
            var avatar = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = (IBrush?)Application.Current!.Resources["ActiveHighlight"],
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = avatarLetter,
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            // Status dot
            var statusDot = new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(3.5),
                Background = ResolveStatusBrush(agent.Status),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Name
            var name = new TextBlock
            {
                Text = agent.Name,
                FontSize = 12,
                Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            // Status label
            var status = new TextBlock
            {
                Text = agent.Status.ToString().ToLowerInvariant(),
                FontSize = 10,
                Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };

            // Row: avatar + dot + name + status
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 0,
                Margin = new Thickness(6, 2),
                Children = { avatar, statusDot, name, status }
            };

            _peoplePanel.Children.Add(row);
        }
    }

    private static IBrush ResolveStatusBrush(WorkspaceAgentStatus status)
    {
        return (IBrush)(status switch
        {
            WorkspaceAgentStatus.Active => Application.Current!.Resources["Success"],
            WorkspaceAgentStatus.Busy => Application.Current!.Resources["Warning"],
            _ => Application.Current!.Resources["TextSecondary"]
        });
    }
}