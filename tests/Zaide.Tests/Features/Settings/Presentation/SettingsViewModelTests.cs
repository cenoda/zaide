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
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Presentation;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Settings.Presentation;

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

    // ── Editor field setters ──────────────────────────────────────────────

    public static IEnumerable<object?[]> EditorFieldSetterData()
    {
        yield return new object?[] { "SetCodeFontFamily", new[] { "Fira Code" }, (Func<SettingsModel, string?>)(m => m.Editor.CodeFontFamily) };
        yield return new object?[] { "SetCodeFontSize", new object[] { 18 }, (Func<SettingsModel, int?>)(m => m.Editor.CodeFontSize) };
        yield return new object?[] { "SetProseFontFamily", new[] { "Noto Serif" }, (Func<SettingsModel, string?>)(m => m.Editor.ProseFontFamily) };
        yield return new object?[] { "SetTabSize", new object[] { 2 }, (Func<SettingsModel, int?>)(m => m.Editor.TabSize) };
        yield return new object?[] { "SetInsertSpaces", new object[] { false }, (Func<SettingsModel, bool?>)(m => m.Editor.InsertSpaces) };
        yield return new object?[] { "SetShowWhitespace", new object[] { true }, (Func<SettingsModel, bool?>)(m => m.Editor.ShowWhitespace) };
        yield return new object?[] { "SetShowTabs", new object[] { true }, (Func<SettingsModel, bool?>)(m => m.Editor.ShowTabs) };
        yield return new object?[] { "SetShowSpaces", new object[] { true }, (Func<SettingsModel, bool?>)(m => m.Editor.ShowSpaces) };
    }

    [Theory]
    [MemberData(nameof(EditorFieldSetterData))]
    public void EditorFieldSetter_ProjectsFromCandidate(string methodName, object[] args, Delegate accessor)
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        var method = typeof(SettingsViewModel).GetMethod(methodName);
        Assert.NotNull(method);

        var original = accessor.DynamicInvoke(vm.Candidate);
        method.Invoke(vm, args);
        var updated = accessor.DynamicInvoke(vm.Candidate);
        Assert.NotEqual(original, updated);
        Assert.Equal(args[0], updated);
    }

    [Fact]
    public void TerminalFontFamilySetter_ProjectsFromCandidate()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        var original = vm.Candidate.Editor.TerminalFontFamily;
        vm.SetTerminalFontFamily("Iosevka");
        Assert.NotEqual(original, vm.Candidate.Editor.TerminalFontFamily);
        Assert.Equal("Iosevka", vm.Candidate.Editor.TerminalFontFamily);
    }

    [Fact]
    public void TerminalFontSizeSetter_ProjectsFromCandidate()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        var original = vm.Candidate.Editor.TerminalFontSize;
        vm.SetTerminalFontSize(16);
        Assert.NotEqual(original, vm.Candidate.Editor.TerminalFontSize);
        Assert.Equal(16, vm.Candidate.Editor.TerminalFontSize);
    }

    // ── Immutability ──────────────────────────────────────────────────────

    [Fact]
    public void CandidateUpdates_RemainImmutable_BaseUnchanged()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        var baseSnapshot = settings.Current;
        var originalCodeFontFamily = baseSnapshot.Editor.CodeFontFamily;

        vm.SetCodeFontFamily("JetBrains Mono");

        Assert.NotEqual(originalCodeFontFamily, vm.Candidate.Editor.CodeFontFamily);
        Assert.Equal("JetBrains Mono", vm.Candidate.Editor.CodeFontFamily);
        Assert.Equal(originalCodeFontFamily, settings.Current.Editor.CodeFontFamily);
        Assert.Equal(originalCodeFontFamily, vm.BaseSnapshot.Editor.CodeFontFamily);
    }

    // ── Editor validation ─────────────────────────────────────────────────

    [Fact]
    public void Validation_CodeFontFamilyEmpty_ReportsError()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetCodeFontFamily("");
        Assert.Contains(vm.ValidationErrors, e => e.PropertyPath == "Editor.CodeFontFamily");
        Assert.True(vm.HasErrors);
    }

    [Fact]
    public void Validation_CodeFontSizeZero_ReportsError()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetCodeFontSize(0);
        Assert.Contains(vm.ValidationErrors, e => e.PropertyPath == "Editor.CodeFontSize");
        Assert.True(vm.HasErrors);
    }

    [Fact]
    public void Validation_ProseFontFamilyEmpty_ReportsError()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetProseFontFamily("");
        Assert.Contains(vm.ValidationErrors, e => e.PropertyPath == "Editor.ProseFontFamily");
        Assert.True(vm.HasErrors);
    }

    [Fact]
    public void Validation_TerminalFontFamilyEmpty_ReportsError()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetTerminalFontFamily("");
        Assert.Contains(vm.ValidationErrors, e => e.PropertyPath == "Editor.TerminalFontFamily");
        Assert.True(vm.HasErrors);
    }

    [Fact]
    public void Validation_TerminalFontSizeZero_ReportsError()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetTerminalFontSize(0);
        Assert.Contains(vm.ValidationErrors, e => e.PropertyPath == "Editor.TerminalFontSize");
        Assert.True(vm.HasErrors);
    }

    [Fact]
    public void Validation_TabSizeZero_ReportsError()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetTabSize(0);
        Assert.Contains(vm.ValidationErrors, e => e.PropertyPath == "Editor.TabSize");
        Assert.True(vm.HasErrors);
    }

    // ── Validation prevents Apply ─────────────────────────────────────────

    [Fact]
    public async Task Apply_WithValidationErrors_ReturnsFalseWithoutCallingService()
    {
        using var settings = new FakeSettingsService();
        var original = settings.Current;
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetCodeFontSize(0); // invalid: must be positive

        Assert.False(await vm.ApplyAsync());
        Assert.True(vm.HasErrors);
        Assert.Same(original, settings.Current); // service not contacted
    }

    // ── Discard preserves validation recovery ─────────────────────────────

    [Fact]
    public void Discard_ClearsValidationErrors()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetCodeFontFamily("");

        Assert.True(vm.HasErrors);

        vm.Discard();

        Assert.False(vm.HasErrors);
        Assert.Equal(settings.Current, vm.Candidate);
    }

    // ── Rebase preserves candidate edits ──────────────────────────────────

    [Fact]
    public async Task ConflictRebase_PreservesAllEditorAndTerminalFields()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetCodeFontFamily("Fira Code");
        vm.SetProseFontFamily("Noto Serif");
        vm.SetTabSize(2);
        vm.SetTerminalFontFamily("Iosevka");
        vm.SetModel("deepseek-chat");

        // External change
        settings.CommitExternally(settings.Current with
        {
            Editor = settings.Current.Editor with { CodeFontSize = 99 }
        });

        Assert.False(await vm.ApplyAsync());
        Assert.True(vm.HasConflict);

        vm.Rebase();

        Assert.False(vm.HasConflict);
        Assert.Equal("Fira Code", vm.Candidate.Editor.CodeFontFamily);
        Assert.Equal("Noto Serif", vm.Candidate.Editor.ProseFontFamily);
        Assert.Equal(2, vm.Candidate.Editor.TabSize);
        Assert.Equal("Iosevka", vm.Candidate.Editor.TerminalFontFamily);
        Assert.Equal("deepseek-chat", vm.Candidate.Llm.Model);
        Assert.Equal(99, vm.Candidate.Editor.CodeFontSize); // external value merged
    }

    // ── API-key not in settings serialization ─────────────────────────────

    [Fact]
    public void ApiKey_IsNotPresentInSettingsSerialization()
    {
        using var settings = new FakeSettingsService();
        var secrets = new FakeSecretStore();
        using var vm = new SettingsViewModel(settings, secrets);
        vm.ApiKey = "super-secret-key";

        var json = System.Text.Json.JsonSerializer.Serialize(vm.Candidate);
        Assert.DoesNotContain("super-secret-key", json);
    }

    // ── Transient subscription lifecycle ──────────────────────────────────

    [Fact]
    public void TransientSettingsViewModel_DoesNotLeakSubscriptionsAfterDispose()
    {
        using var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeSecretStore());
        Assert.Equal(1, settings.SubscriptionCount);

        vm.Dispose();
        Assert.Equal(1, settings.SubscriptionDisposeCount);

        // After dispose, external changes should not affect the disposed VM
        settings.CommitExternally(settings.Current with
        {
            Llm = settings.Current.Llm with { Model = "should-not-reach" }
        });
        // No crash — subscriptions are disposed
    }

    [Fact]
    public async Task RefreshFromCurrent_WithoutPreserve_DiscardsCandidateAndClearsConflict()
    {
        using var settings = new FakeSettingsService();
        using var vm = new SettingsViewModel(settings, new FakeSecretStore());
        vm.SetModel("edited");

        // Simulate external change; subscription won't fire in test (AvaloniaScheduler),
        // so use ApplyAsync to trigger conflict detection.
        settings.CommitExternally(settings.Current with
        {
            Editor = settings.Current.Editor with { CodeFontSize = 30 }
        });
        Assert.False(await vm.ApplyAsync()); // detects conflict
        Assert.True(vm.HasConflict);

        vm.RefreshFromCurrent(preserveCandidate: false);

        Assert.False(vm.HasConflict);
        Assert.Equal(30, vm.Candidate.Editor.CodeFontSize);
        Assert.NotEqual("edited", vm.Candidate.Llm.Model); // discarded
    }

    // ── Fake service / store ──────────────────────────────────────────────

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
