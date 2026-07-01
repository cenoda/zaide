using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Zaide.ViewModels;

namespace Zaide.Views;

public class TownhallView : UserControl
{
    private readonly TownhallChannelPanel _channelPanel;
    private readonly TownhallChatPanel _chatPanel;
    private readonly TownhallPeoplePanel _peoplePanel;
    private readonly TownhallInputArea _inputArea;

    public static readonly StyledProperty<TownhallViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<TownhallView, TownhallViewModel?>(nameof(ViewModel));

    public TownhallViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public TownhallView()
    {
        _channelPanel = new TownhallChannelPanel();
        _chatPanel = new TownhallChatPanel();
        _peoplePanel = new TownhallPeoplePanel();
        _inputArea = new TownhallInputArea();

        var contentGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(140) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 220 },
                new ColumnDefinition { Width = new GridLength(140) }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        Grid.SetColumn(_channelPanel, 0);
        Grid.SetRow(_channelPanel, 0);

        Grid.SetColumn(_chatPanel, 1);
        Grid.SetRow(_chatPanel, 0);

        Grid.SetColumn(_peoplePanel, 2);
        Grid.SetRow(_peoplePanel, 0);

        Grid.SetColumn(_inputArea, 0);
        Grid.SetColumnSpan(_inputArea, 3);
        Grid.SetRow(_inputArea, 1);

        contentGrid.Children.Add(_channelPanel);
        contentGrid.Children.Add(_chatPanel);
        contentGrid.Children.Add(_peoplePanel);
        contentGrid.Children.Add(_inputArea);

        Content = contentGrid;

        _channelPanel.ChannelSelected += channelId =>
        {
            if (ViewModel is null) return;
            ViewModel.SelectChannel(channelId);
            RefreshFromViewModel();
        };

        _inputArea.DraftChanged += text =>
        {
            if (ViewModel is null) return;
            ViewModel.DraftText = text;
        };

        _inputArea.SendClicked += () =>
        {
            if (ViewModel is null) return;
            ViewModel.SendMessage();
            RefreshFromViewModel();
        };

        this.GetObservable(ViewModelProperty).Subscribe(_ => RefreshFromViewModel(), _ => { }, () => { });
    }

    private void RefreshFromViewModel()
    {
        if (ViewModel is null)
            return;

        _channelPanel.Channels = ViewModel.Channels.ToList();
        _chatPanel.Messages = ViewModel.Messages.ToList();
        _peoplePanel.Agents = ViewModel.Agents.ToList();
        _inputArea.DraftText = ViewModel.DraftText;

        _channelPanel.Refresh();
        _chatPanel.Refresh();
        _peoplePanel.Refresh();
    }
}
