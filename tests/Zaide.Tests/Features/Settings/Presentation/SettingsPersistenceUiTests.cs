using Avalonia;
using System;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Moq;
using Xunit;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using ReactiveUI;
using Splat;
using Zaide.App.Shell;
using Zaide.App.Composition;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Settings.Presentation;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;

namespace Zaide.Tests.Features.Settings.Presentation;

public sealed class SettingsPersistenceUiTests
{
    static SettingsPersistenceUiTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        EnsureApplication();
        Locator.CurrentMutable.Register(
            () => new AvaloniaActivationForViewFetcher(),
            typeof(IActivationForViewFetcher));
    }

    [Fact]
    public void ConfiguredModelStatus_IsShownOnlyForNonEmptySavedModel()
    {
        Assert.Equal("configured: saved-model", StatusBar.FormatConfiguredModel("saved-model"));
        Assert.Null(StatusBar.FormatConfiguredModel(""));
        Assert.Null(StatusBar.FormatConfiguredModel("  "));
    }

    [Fact]
    public void TerminalRenderControl_ApplyFontSettings_UpdatesMetricsAndInvalidates()
    {
        EnsureApplication();
        var control = new TestTerminalRenderControl();

        control.ApplyFontSettings("DejaVu Sans Mono", 19);

        Assert.Equal("DejaVu Sans Mono", control.CurrentFontFamily);
        Assert.Equal(19, control.CurrentFontSize);
        Assert.Equal(1, control.MeasureInvalidationCalls);
        Assert.Equal(1, control.VisualInvalidationCalls);
    }

    [Fact]
    public void EditorSettingsProjection_AppliesInitialAndLiveEditorOptions()
    {
        var initial = SettingsModel.Defaults.Editor with
        {
            CodeFontFamily = "Initial Code",
            ProseFontFamily = "Initial Prose",
            CodeFontSize = 17,
            TabSize = 2,
            InsertSpaces = false,
            ShowWhitespace = true,
            ShowTabs = true,
            ShowSpaces = false
        };
        var live = initial with
        {
            CodeFontFamily = "Live Code",
            ProseFontFamily = "Live Prose",
            CodeFontSize = 21,
            TabSize = 8,
            InsertSpaces = true,
            ShowTabs = false,
            ShowSpaces = true
        };

        var initialProjection = EditorView.ProjectSettings(initial);
        var liveProjection = EditorView.ProjectSettings(live);

        Assert.Equal("Initial Code", initialProjection.CodeFont.Name);
        Assert.Equal("Initial Prose", initialProjection.ProseFont.Name);
        Assert.Equal(17, initialProjection.CodeFontSize);
        Assert.Equal(2, initialProjection.TabSize);
        Assert.False(initialProjection.InsertSpaces);
        Assert.True(initialProjection.ShowTabs);
        Assert.False(initialProjection.ShowSpaces);
        Assert.Equal("Live Code", liveProjection.CodeFont.Name);
        Assert.Equal("Live Prose", liveProjection.ProseFont.Name);
        Assert.Equal(21, liveProjection.CodeFontSize);
        Assert.Equal(8, liveProjection.TabSize);
        Assert.True(liveProjection.InsertSpaces);
        Assert.False(liveProjection.ShowTabs);
        Assert.True(liveProjection.ShowSpaces);
    }

    [Fact]
    public void EditorSettingsProjection_SelectsProseForMarkdownAndCodeOtherwise()
    {
        var projection = EditorView.ProjectSettings(SettingsModel.Defaults.Editor with
        {
            CodeFontFamily = "Configured Code",
            ProseFontFamily = "Configured Prose"
        });

        Assert.Equal("Configured Prose", EditorView.SelectFont(projection, "README.md").Name);
        Assert.Equal("Configured Code", EditorView.SelectFont(projection, "Program.cs").Name);
    }

    [Fact]
    public void SettingsBinding_AppliesInitialAndLiveSnapshots_AndDisposesOnce()
    {
        var initial = SettingsModel.Defaults with
        {
            Editor = SettingsModel.Defaults.Editor with { CodeFontSize = 16 }
        };
        var live = initial with
        {
            Editor = initial.Editor with { CodeFontSize = 23 }
        };
        var changes = new Subject<SettingsModel>();
        var counted = new CountingObservable<SettingsModel>(changes);
        var settings = new Mock<ISettingsService>();
        settings.SetupGet(x => x.Current).Returns(initial);
        settings.SetupGet(x => x.WhenChanged).Returns(counted);
        var applied = new System.Collections.Generic.List<SettingsModel>();

        using (var binding = EditorView.CreateSettingsBinding(
            settings.Object,
            applied.Add,
            ImmediateScheduler.Instance))
        {
            Assert.Single(applied);
            Assert.Equal(16, applied[0].Editor.CodeFontSize);
            changes.OnNext(live);
            Assert.Equal(2, applied.Count);
            Assert.Equal(23, applied[1].Editor.CodeFontSize);
        }

        Assert.Equal(1, counted.DisposeCount);
    }

    [Fact]
    public void TerminalSettingsProjection_AppliesLiveFontSettingsWithoutRecreatingSurface()
    {
        EnsureApplication();
        var initial = TerminalPanel.ProjectTerminalSettings(SettingsModel.Defaults.Editor with
        {
            TerminalFontFamily = "Initial Terminal",
            TerminalFontSize = 13
        });
        var live = TerminalPanel.ProjectTerminalSettings(SettingsModel.Defaults.Editor with
        {
            TerminalFontFamily = "Live Terminal",
            TerminalFontSize = 22
        });
        var surface = new TerminalRenderControl();
        var originalSurface = surface;

        surface.ApplyFontSettings(initial.Family, initial.Size);
        surface.ApplyFontSettings(live.Family, live.Size);

        Assert.Same(originalSurface, surface);
        Assert.Equal("Live Terminal", surface.CurrentFontFamily);
        Assert.Equal(22, surface.CurrentFontSize);
    }

    [Fact]
    public void TerminalTabHost_DisposesInactivePanelsOnRemovalReplacementDetachAndClose()
    {
        EnsureApplication();
        var settings = new Mock<ISettingsService>();
        settings.SetupGet(x => x.Current).Returns(SettingsModel.Defaults);
        settings.SetupGet(x => x.WhenChanged).Returns(Observable.Empty<SettingsModel>());
        var handles = new System.Collections.Generic.List<CountingDisposable>();
        var viewHost = new TerminalTabHost(
            settings.Object,
            _ => null,
            (_, _) =>
            {
                var handle = new CountingDisposable();
                handles.Add(handle);
                return handle;
            });

        using var terminalHost = CreateTerminalHost();
        viewHost.SetHost(terminalHost);
        terminalHost.Tabs.Add(new TerminalTabViewModel(CreateTerminalSession()));
        var firstTab = terminalHost.Tabs[0];
        terminalHost.Tabs.Remove(firstTab);
        Assert.Equal(1, handles[0].DisposeCount);

        viewHost.SetHost(terminalHost);
        Assert.Equal(3, handles.Count);
        viewHost.SetHost(CreateTerminalHost());
        Assert.Equal(1, handles[2].DisposeCount);
        Assert.Equal(4, handles.Count);

        viewHost.DetachHost();
        Assert.All(handles, handle => Assert.Equal(1, handle.DisposeCount));
        viewHost.SetHost(terminalHost);
        Assert.Equal(5, handles.Count);
        viewHost.Dispose();
        viewHost.Dispose();
        Assert.All(handles, handle => Assert.Equal(1, handle.DisposeCount));
    }

    [Fact]
    public void MainWindowFinalCleanup_DisposesEditorAndTerminalExactlyOnce()
    {
        int editorDisposals = 0;
        int terminalDisposals = 0;
        using var cleanup = new FinalWindowCleanup(
            () => editorDisposals++,
            () => terminalDisposals++);

        cleanup.Dispose();
        cleanup.Dispose();

        Assert.Equal(1, editorDisposals);
        Assert.Equal(1, terminalDisposals);
    }

    private sealed class TestTerminalRenderControl : TerminalRenderControl
    {
        public int MeasureInvalidationCalls { get; private set; }
        public int VisualInvalidationCalls { get; private set; }

        internal override void InvalidateMeasureForFontChange() => MeasureInvalidationCalls++;

        internal override void InvalidateVisualForFontChange() => VisualInvalidationCalls++;
    }

    private sealed class CountingObservable<T> : IObservable<T>
    {
        private readonly IObservable<T> _source;
        public int DisposeCount { get; private set; }

        public CountingObservable(IObservable<T> source) => _source = source;

        public IDisposable Subscribe(IObserver<T> observer)
        {
            var sourceSubscription = _source.Subscribe(observer);
            return new CountingSubscription(sourceSubscription, this);
        }

        private sealed class CountingSubscription : IDisposable
        {
            private readonly IDisposable _sourceSubscription;
            private readonly CountingObservable<T> _owner;
            private bool _disposed;

            public CountingSubscription(IDisposable sourceSubscription, CountingObservable<T> owner)
            {
                _sourceSubscription = sourceSubscription;
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _sourceSubscription.Dispose();
                _owner.DisposeCount++;
            }
        }
    }

    private static TerminalHost CreateTerminalHost()
    {
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(x => x.Create()).Returns(() => new Mock<ITerminalService>().Object);
        return new TerminalHost(factory.Object);
    }

    private static TerminalViewModel CreateTerminalSession()
    {
        var service = new Mock<ITerminalService>();
        return new TerminalViewModel(service.Object, action => action());
    }

    private sealed class CountingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            if (DisposeCount == 0)
                DisposeCount++;
        }
    }

    private static void EnsureApplication()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        if (Application.Current is global::Zaide.App.Composition.App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
                app.Initialize();
            return;
        }

        var createdApp = new global::Zaide.App.Composition.App();
        createdApp.Initialize();
    }
}
