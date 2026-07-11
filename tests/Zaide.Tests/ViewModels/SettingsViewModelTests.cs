using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void CandidateValidation_IsInlineAndFieldSpecific()
    {
        using var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeSecretStore());

        vm.SetBaseUrl("");

        Assert.Contains(vm.ValidationErrors, error => error.PropertyPath == "Llm.BaseUrl");
        Assert.True(vm.HasErrors);
    }

    [Fact]
    public async Task ApplySuccess_CommitsCandidateAndSecretThroughBoundary()
    {
        using var settings = new FakeSettingsService();
        var secrets = new FakeSecretStore();
        using var vm = new SettingsViewModel(settings, secrets);
        vm.SetModel("configured-model");
        vm.ApiKey = "secret-value";

        Assert.True(await vm.ApplyAsync());
        Assert.Equal("configured-model", settings.Current.Llm.Model);
        Assert.Equal("secret-value", secrets.Get(SettingsViewModel.ApiKeyName));
        Assert.DoesNotContain("secret-value", System.Text.Json.JsonSerializer.Serialize(settings.Current));
    }

    [Fact]
    public void Discard_RestoresBaseCandidateAndSecret()
    {
        using var settings = new FakeSettingsService();
        var secrets = new FakeSecretStore();
        secrets.Set(SettingsViewModel.ApiKeyName, "original");
        using var vm = new SettingsViewModel(settings, secrets);
        vm.SetModel("edited");
        vm.ApiKey = "changed";

        vm.Discard();

        Assert.Equal(settings.Current, vm.Candidate);
        Assert.Equal("original", vm.ApiKey);
    }

    [Fact]
    public async Task StaleApply_ReturnsConflict_ThenRebasePreservesIntent()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetModel("user-intent");
        settings.CommitExternally(settings.Current with
        {
            Editor = settings.Current.Editor with { CodeFontSize = 22 }
        });

        Assert.False(await vm.ApplyAsync());
        Assert.True(vm.HasConflict);
        vm.Rebase();

        Assert.False(vm.HasConflict);
        Assert.Equal("user-intent", vm.Candidate.Llm.Model);
        Assert.Equal(22, vm.Candidate.Editor.CodeFontSize);
        Assert.True(await vm.ApplyAsync());
    }

    [Fact]
    public async Task EmptyApiKey_DeletesSecretWithoutAddingItToSettings()
    {
        using var settings = new FakeSettingsService();
        var secrets = new FakeSecretStore();
        secrets.Set(SettingsViewModel.ApiKeyName, "old-secret");
        using var vm = new SettingsViewModel(settings, secrets);
        vm.ApiKey = "";

        Assert.True(await vm.ApplyAsync());
        Assert.Null(secrets.Get(SettingsViewModel.ApiKeyName));
        Assert.DoesNotContain("old-secret", System.Text.Json.JsonSerializer.Serialize(settings.Current));
    }

    [Fact]
    public void Dispose_DisposesOwnedSettingsSubscriptionExactlyOnce()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        Assert.Equal(1, settings.SubscriptionCount);

        vm.Dispose();
        vm.Dispose();

        Assert.Equal(1, settings.SubscriptionDisposeCount);
    }

    private sealed class FakeSettingsService : ISettingsService, IDisposable
    {
        private readonly Subject<SettingsModel> _changes = new();
        private SettingsModel _current = SettingsModel.Defaults;
        public SettingsModel Current => _current;
        public IObservable<SettingsModel> WhenChanged => new CountingObservable<SettingsModel>(_changes, this);
        public SettingsLoadResult LoadResult => SettingsLoadResult.Missing;
        public IObservable<SettingsSaveError> WriteErrors => System.Reactive.Linq.Observable.Empty<SettingsSaveError>();
        public int SubscriptionCount { get; private set; }
        public int SubscriptionDisposeCount { get; private set; }

        public Task<SettingsMutationResult> ApplyAsync(SettingsModel expectedCurrent, SettingsModel next, CancellationToken ct = default)
        {
            if (!ReferenceEquals(expectedCurrent, _current))
                return Task.FromResult<SettingsMutationResult>(new SettingsMutationResult.Conflict(_current));
            var errors = SettingsValidator.Validate(next);
            if (errors.Count != 0)
                return Task.FromResult<SettingsMutationResult>(new SettingsMutationResult.Invalid(next, errors));
            _current = next;
            _changes.OnNext(next);
            return Task.FromResult<SettingsMutationResult>(new SettingsMutationResult.Applied(next, new SettingsSaveResult.Saved()));
        }

        public Task<SettingsMutationResult> UpdateAsync(Func<SettingsModel, SettingsModel> producer, CancellationToken ct = default) =>
            ApplyAsync(_current, producer(_current), ct);

        public Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default) =>
            Task.FromResult<SettingsSaveResult>(new SettingsSaveResult.Saved());

        public void CommitExternally(SettingsModel next)
        {
            _current = next;
            _changes.OnNext(next);
        }

        public void Dispose() => _changes.Dispose();

        private sealed class CountingObservable<T>(IObservable<T> source, FakeSettingsService owner) : IObservable<T>
        {
            public IDisposable Subscribe(IObserver<T> observer)
            {
                owner.SubscriptionCount++;
                var subscription = source.Subscribe(observer);
                return System.Reactive.Disposables.Disposable.Create(() =>
                {
                    owner.SubscriptionDisposeCount++;
                    subscription.Dispose();
                });
            }
        }
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new();
        public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;
        public void Set(string key, string value) => _values[key] = value;
        public void Delete(string key) => _values.Remove(key);
    }
}
