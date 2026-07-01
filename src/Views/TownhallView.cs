using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ReactiveUI;
using Zaide.ViewModels;

namespace Zaide.Views;

public class TownhallView : UserControl
{
    private readonly TownhallChannelPanel _channelPanel;
    private readonly TownhallChatPanel _chatPanel;
    private readonly TownhallPeoplePanel _peoplePanel;
    private readonly TownhallInputArea _inputArea;

    private IDisposable? _viewModelBindings;

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
            ViewModel?.SelectChannel(channelId);
        };

        _inputArea.DraftChanged += text =>
        {
            if (ViewModel is not null && ViewModel.DraftText != text)
                ViewModel.DraftText = text;
        };

        _inputArea.SendClicked += () =>
        {
            ViewModel?.SendMessage();
        };

        this.GetObservable(ViewModelProperty).Subscribe(OnViewModelChanged);
    }

    private void OnViewModelChanged(TownhallViewModel? vm)
    {
        _viewModelBindings?.Dispose();
        _viewModelBindings = null;

        if (vm is null)
        {
            _channelPanel.Channels = Array.Empty<Models.Channel>();
            _chatPanel.Messages = Array.Empty<Models.TownhallMessage>();
            _peoplePanel.Agents = Array.Empty<Models.WorkspaceAgent>();
            _inputArea.DraftText = string.Empty;
            return;
        }

        var disposable = new System.Reactive.Disposables.CompositeDisposable();

        disposable.Add(vm.WhenAnyValue(x => x.ActiveChannelId)
            .Subscribe(_ =>
            {
                _channelPanel.Channels = vm.Channels.ToList();
                _chatPanel.Messages = vm.Messages.ToList();
            }));

        System.Collections.Specialized.NotifyCollectionChangedEventHandler messagesChanged = (_, _) =>
        {
            _chatPanel.Messages = vm.Messages.ToList();
        };
        vm.Messages.CollectionChanged += messagesChanged;
        disposable.Add(System.Reactive.Disposables.Disposable.Create(() =>
            vm.Messages.CollectionChanged -= messagesChanged));

        System.Collections.Specialized.NotifyCollectionChangedEventHandler agentsChanged = (_, _) =>
        {
            _peoplePanel.Agents = vm.Agents.ToList();
        };
        vm.Agents.CollectionChanged += agentsChanged;
        disposable.Add(System.Reactive.Disposables.Disposable.Create(() =>
            vm.Agents.CollectionChanged -= agentsChanged));

        System.Collections.Specialized.NotifyCollectionChangedEventHandler channelsChanged = (_, _) =>
        {
            _channelPanel.Channels = vm.Channels.ToList();
        };
        vm.Channels.CollectionChanged += channelsChanged;
        disposable.Add(System.Reactive.Disposables.Disposable.Create(() =>
            vm.Channels.CollectionChanged -= channelsChanged));

        disposable.Add(vm.WhenAnyValue(x => x.DraftText)
            .Subscribe(text =>
            {
                if (_inputArea.DraftText != text)
                    _inputArea.DraftText = text;
            }));

        _channelPanel.Channels = vm.Channels.ToList();
        _chatPanel.Messages = vm.Messages.ToList();
        _peoplePanel.Agents = vm.Agents.ToList();
        _inputArea.DraftText = vm.DraftText;

        _viewModelBindings = disposable;
    }
}
