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
        var header = new TextBlock
        {
            Text = "PEOPLE",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"],
            Margin = new Thickness(10, 10, 10, 8)
        };

        _peoplePanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            Margin = new Thickness(8, 0, 8, 8)
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Background = (IBrush?)Application.Current!.Resources["PanelDeep"]
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(_peoplePanel, 1);
        root.Children.Add(header);
        root.Children.Add(_peoplePanel);

        Content = root;

        this.GetObservable(AgentsProperty).Subscribe(_ => RenderAgents(), _ => { }, () => { });
    }

    public void Refresh()
    {
        RenderAgents();
    }

    private void RenderAgents()
    {
        _peoplePanel.Children.Clear();
        if (Agents is null)
            return;

        foreach (var agent in Agents)
        {
            var statusDot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = ResolveStatusBrush(agent.Status),
                VerticalAlignment = VerticalAlignment.Center
            };

            var name = new TextBlock
            {
                Text = agent.Name,
                FontSize = 12,
                Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
                VerticalAlignment = VerticalAlignment.Center
            };

            var status = new TextBlock
            {
                Text = agent.Status.ToString().ToLowerInvariant(),
                FontSize = 10,
                Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"],
                VerticalAlignment = VerticalAlignment.Center
            };

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };

            var details = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 0,
                Margin = new Thickness(8, 0, 0, 0),
                Children = { name, status }
            };

            Grid.SetColumn(statusDot, 0);
            Grid.SetColumn(details, 1);
            row.Children.Add(statusDot);
            row.Children.Add(details);

            var card = new Border
            {
                Background = (IBrush?)Application.Current!.Resources["SurfacePanel"],
                BorderBrush = (IBrush?)Application.Current!.Resources["SurfaceBorder"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Child = row
            };

            _peoplePanel.Children.Add(card);
        }
    }

    private static IBrush? ResolveStatusBrush(WorkspaceAgentStatus status)
    {
        return status switch
        {
            WorkspaceAgentStatus.Active => new SolidColorBrush(Color.Parse("#28A745")),
            WorkspaceAgentStatus.Busy => new SolidColorBrush(Color.Parse("#FCBB47")),
            _ => new SolidColorBrush(Color.Parse("#8B95A5"))
        };
    }
}
