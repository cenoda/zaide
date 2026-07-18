using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Splat;
using Xunit;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Presentation;

namespace Zaide.Tests.Features.Settings.Presentation;

/// <summary>
/// Focused proofs for <see cref="SettingsPanelFactory"/> (Refactor 6.3 M10):
/// exact ViewModel/View pair, DataContext ownership, fresh instances per Create,
/// and supplied settings/secrets wiring.
/// </summary>
public sealed class SettingsPanelFactoryTests
{
    static SettingsPanelFactoryTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Locator.CurrentMutable.Register(
            () => new AvaloniaActivationForViewFetcher(),
            typeof(IActivationForViewFetcher));
        EnsureApplication();
    }

    [Fact]
    public void Create_ReturnsViewModelAndPanelPair_WithPanelBoundToViewModel()
    {
        using var settings = new TestSettingsService();
        var secrets = new TestSecretStore();
        var factory = new SettingsPanelFactory();

        var (viewModel, panel) = factory.Create(settings, secrets);
        using (viewModel)
        using (panel)
        {
            Assert.NotNull(viewModel);
            Assert.NotNull(panel);
            Assert.IsType<SettingsViewModel>(viewModel);
            Assert.IsType<SettingsPanelView>(panel);
            Assert.Same(viewModel, panel.ViewModel);
            Assert.Same(viewModel, panel.DataContext);
        }
    }

    [Fact]
    public void Create_ReturnsFreshInstances_OnEachCall()
    {
        using var settings = new TestSettingsService();
        var secrets = new TestSecretStore();
        var factory = new SettingsPanelFactory();

        var (vm1, panel1) = factory.Create(settings, secrets);
        var (vm2, panel2) = factory.Create(settings, secrets);

        using (vm1)
        using (panel1)
        using (vm2)
        using (panel2)
        {
            Assert.NotSame(vm1, vm2);
            Assert.NotSame(panel1, panel2);
            Assert.Same(vm1, panel1.ViewModel);
            Assert.Same(vm2, panel2.ViewModel);
        }
    }

    [Fact]
    public void Create_UsesSuppliedSettingsAndSecrets()
    {
        using var settings = new TestSettingsService();
        settings.Publish(settings.Current with
        {
            Llm = settings.Current.Llm with { Model = "factory-model" }
        });
        var secrets = new TestSecretStore();
        secrets.Set(SettingsViewModel.ApiKeyName, "factory-secret");
        var factory = new SettingsPanelFactory();

        var (viewModel, panel) = factory.Create(settings, secrets);
        using (viewModel)
        using (panel)
        {
            Assert.Equal("factory-model", viewModel.Candidate.Llm.Model);
            Assert.Equal("factory-secret", viewModel.ApiKey);
            Assert.Same(viewModel, panel.DataContext);
        }
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

        new global::Zaide.App.Composition.App().Initialize();
    }

    private sealed class TestSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new();
        public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;
        public void Set(string key, string value) => _values[key] = value;
        public void Delete(string key) => _values.Remove(key);
    }

    private sealed class TestSettingsService : ISettingsService, IDisposable
    {
        private readonly Subject<SettingsModel> _changes = new();
        public SettingsModel Current { get; private set; } = SettingsModel.Defaults;
        public IObservable<SettingsModel> WhenChanged => _changes;
        public SettingsLoadResult LoadResult => SettingsLoadResult.Missing;
        public IObservable<SettingsSaveError> WriteErrors =>
            System.Reactive.Linq.Observable.Empty<SettingsSaveError>();

        public void Publish(SettingsModel snapshot)
        {
            Current = snapshot;
            _changes.OnNext(snapshot);
        }

        public Task<SettingsMutationResult> ApplyAsync(
            SettingsModel expectedCurrent,
            SettingsModel next,
            CancellationToken ct = default)
        {
            if (!ReferenceEquals(expectedCurrent, Current))
            {
                return Task.FromResult<SettingsMutationResult>(
                    new SettingsMutationResult.Conflict(Current));
            }

            Current = next;
            return Task.FromResult<SettingsMutationResult>(
                new SettingsMutationResult.Applied(next, new SettingsSaveResult.Saved()));
        }

        public Task<SettingsMutationResult> UpdateAsync(
            Func<SettingsModel, SettingsModel> producer,
            CancellationToken ct = default) =>
            ApplyAsync(Current, producer(Current), ct);

        public Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default) =>
            Task.FromResult<SettingsSaveResult>(new SettingsSaveResult.Saved());

        public void Dispose() => _changes.Dispose();
    }
}
