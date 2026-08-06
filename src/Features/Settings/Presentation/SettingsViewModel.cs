using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Settings.Presentation;

/// <summary>Transient editor for one immutable settings candidate.</summary>
public sealed class SettingsViewModel : ReactiveObject, IDisposable
{
    public const string ApiKeyName = "llm.apiKey";
    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly CompositeDisposable _subscriptions = new();
    private SettingsModel _baseSnapshot;
    private SettingsModel _candidate;
    private string? _apiKey;
    private IReadOnlyList<SettingsValidationError> _validationErrors = Array.Empty<SettingsValidationError>();
    private SettingsModel? _conflictSnapshot;
    private bool _disposed;

    public SettingsModel BaseSnapshot { get => _baseSnapshot; private set => this.RaiseAndSetIfChanged(ref _baseSnapshot, value); }
    public SettingsModel Candidate { get => _candidate; private set => this.RaiseAndSetIfChanged(ref _candidate, value); }
    public string? ApiKey { get => _apiKey; set => this.RaiseAndSetIfChanged(ref _apiKey, value); }
    public IReadOnlyList<SettingsValidationError> ValidationErrors { get => _validationErrors; private set => this.RaiseAndSetIfChanged(ref _validationErrors, value); }
    public SettingsModel? ConflictSnapshot { get => _conflictSnapshot; private set => this.RaiseAndSetIfChanged(ref _conflictSnapshot, value); }
    public bool HasConflict => ConflictSnapshot is not null;
    public bool HasErrors => ValidationErrors.Count != 0;
    public event EventHandler? CloseRequested;

    public ReactiveCommand<Unit, bool> ApplyCommand { get; }
    public ReactiveCommand<Unit, Unit> DiscardCommand { get; }
    public ReactiveCommand<Unit, Unit> RebaseCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public SettingsViewModel(ISettingsService settings, ISecretStore secrets)
        : this(settings, secrets, AvaloniaScheduler.Instance)
    {
    }

    internal SettingsViewModel(ISettingsService settings, ISecretStore secrets, IScheduler scheduler)
    {
        _settings = settings;
        _secrets = secrets;
        _baseSnapshot = settings.Current;
        _candidate = _baseSnapshot;
        _apiKey = secrets.Get(ApiKeyName);
        ValidateCandidate();
        _subscriptions.Add(settings.WhenChanged
            .ObserveOn(scheduler)
            .Subscribe(snapshot =>
            {
                if (ReferenceEquals(snapshot, BaseSnapshot)) return;
                ConflictSnapshot = snapshot;
                if (ReferenceEquals(Candidate, BaseSnapshot))
                {
                    BaseSnapshot = snapshot;
                    Candidate = snapshot;
                    ValidateCandidate();
                }
            }));
        ApplyCommand = ReactiveCommand.CreateFromTask(ApplyAsync);
        DiscardCommand = ReactiveCommand.Create(Discard);
        RebaseCommand = ReactiveCommand.Create(Rebase);
        CloseCommand = ReactiveCommand.Create(RequestClose);
    }

    public void SetCandidate(SettingsModel candidate)
    {
        Candidate = candidate;
        ValidateCandidate();
    }

    public void SetModel(string model) => SetCandidate(Candidate with { Llm = Candidate.Llm with { Model = model } });
    public void SetBaseUrl(string baseUrl) => SetCandidate(Candidate with { Llm = Candidate.Llm with { BaseUrl = baseUrl } });

    // Editor setters
    public void SetCodeFontFamily(string family) => SetCandidate(Candidate with { Editor = Candidate.Editor with { CodeFontFamily = family } });
    public void SetCodeFontSize(int size) => SetCandidate(Candidate with { Editor = Candidate.Editor with { CodeFontSize = size } });
    public void SetProseFontFamily(string family) => SetCandidate(Candidate with { Editor = Candidate.Editor with { ProseFontFamily = family } });
    public void SetTerminalFontFamily(string family) => SetCandidate(Candidate with { Editor = Candidate.Editor with { TerminalFontFamily = family } });
    public void SetTerminalFontSize(int size) => SetCandidate(Candidate with { Editor = Candidate.Editor with { TerminalFontSize = size } });
    public void SetTabSize(int size) => SetCandidate(Candidate with { Editor = Candidate.Editor with { TabSize = size } });
    public void SetInsertSpaces(bool spaces) => SetCandidate(Candidate with { Editor = Candidate.Editor with { InsertSpaces = spaces } });
    public void SetShowWhitespace(bool show) => SetCandidate(Candidate with { Editor = Candidate.Editor with { ShowWhitespace = show } });
    public void SetShowTabs(bool show) => SetCandidate(Candidate with { Editor = Candidate.Editor with { ShowTabs = show } });
    public void SetShowSpaces(bool show) => SetCandidate(Candidate with { Editor = Candidate.Editor with { ShowSpaces = show } });
    public void SetFormatOnSave(bool formatOnSave) => SetCandidate(Candidate with { Editor = Candidate.Editor with { FormatOnSave = formatOnSave } });

    public void SetTraceCaptureEnabled(bool enabled) =>
        SetCandidate(Candidate with { Agents = Candidate.Agents with { TraceCaptureEnabled = enabled } });

    public void SetUsageCaptureEnabled(bool enabled) =>
        SetCandidate(Candidate with { Agents = Candidate.Agents with { UsageCaptureEnabled = enabled } });

    public void SetTracePageSize(int size) =>
        SetCandidate(Candidate with { Agents = Candidate.Agents with { TracePageSize = size } });

    public void SetTraceMaxPageSize(int size) =>
        SetCandidate(Candidate with { Agents = Candidate.Agents with { TraceMaxPageSize = size } });

    public void SetAcpExecutablePath(string path) =>
        SetCandidate(Candidate with { Agents = Candidate.Agents with { AcpExecutablePath = path ?? string.Empty } });

    public void SetAcpArguments(string args) =>
        SetCandidate(Candidate with { Agents = Candidate.Agents with { AcpArguments = args ?? string.Empty } });

    public void SetAcpExpectedAgentName(string name) =>
        SetCandidate(Candidate with { Agents = Candidate.Agents with { AcpExpectedAgentName = name ?? string.Empty } });

    public void SetAcpExpectedAgentVersion(string version) =>
        SetCandidate(Candidate with { Agents = Candidate.Agents with { AcpExpectedAgentVersion = version ?? string.Empty } });

    public void SetDefaultContextPolicyLevel(string level) =>
        SetCandidate(Candidate with { Agents = Candidate.Agents with { DefaultContextPolicyLevel = level ?? "Standard" } });

    public void RefreshFromCurrent(bool preserveCandidate = true)
    {
        var current = _settings.Current;
        if (preserveCandidate) Rebase(current);
        else
        {
            BaseSnapshot = current;
            Candidate = current;
            ConflictSnapshot = null;
            ValidateCandidate();
        }
    }

    public async Task<bool> ApplyAsync()
    {
        ValidateCandidate();
        if (ValidationErrors.Count != 0) return false;
        var result = await _settings.ApplyAsync(BaseSnapshot, Candidate);
        switch (result)
        {
            case SettingsMutationResult.Applied:
                if (string.IsNullOrWhiteSpace(ApiKey)) _secrets.Delete(ApiKeyName);
                else _secrets.Set(ApiKeyName, ApiKey);
                BaseSnapshot = Candidate;
                ConflictSnapshot = null;
                return true;
            case SettingsMutationResult.Invalid invalid:
                ValidationErrors = invalid.Errors;
                return false;
            case SettingsMutationResult.Conflict conflict:
                ConflictSnapshot = conflict.Current;
                return false;
            default:
                return false;
        }
    }

    public void Discard()
    {
        Candidate = BaseSnapshot;
        ApiKey = _secrets.Get(ApiKeyName);
        ConflictSnapshot = null;
        ValidateCandidate();
    }

    public void Rebase() => Rebase(_settings.Current);

    private void Rebase(SettingsModel current)
    {
        Candidate = MergeChanges(BaseSnapshot, Candidate, current);
        BaseSnapshot = current;
        ConflictSnapshot = null;
        ValidateCandidate();
    }

    public void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void ValidateCandidate()
    {
        ValidationErrors = SettingsValidator.Validate(Candidate);
    }

    private static SettingsModel MergeChanges(SettingsModel oldBase, SettingsModel candidate, SettingsModel current)
    {
        var editor = current.Editor;
        if (candidate.Editor.CodeFontFamily != oldBase.Editor.CodeFontFamily) editor = editor with { CodeFontFamily = candidate.Editor.CodeFontFamily };
        if (candidate.Editor.CodeFontSize != oldBase.Editor.CodeFontSize) editor = editor with { CodeFontSize = candidate.Editor.CodeFontSize };
        if (candidate.Editor.ProseFontFamily != oldBase.Editor.ProseFontFamily) editor = editor with { ProseFontFamily = candidate.Editor.ProseFontFamily };
        if (candidate.Editor.TerminalFontFamily != oldBase.Editor.TerminalFontFamily) editor = editor with { TerminalFontFamily = candidate.Editor.TerminalFontFamily };
        if (candidate.Editor.TerminalFontSize != oldBase.Editor.TerminalFontSize) editor = editor with { TerminalFontSize = candidate.Editor.TerminalFontSize };
        if (candidate.Editor.TabSize != oldBase.Editor.TabSize) editor = editor with { TabSize = candidate.Editor.TabSize };
        if (candidate.Editor.InsertSpaces != oldBase.Editor.InsertSpaces) editor = editor with { InsertSpaces = candidate.Editor.InsertSpaces };
        if (candidate.Editor.ShowWhitespace != oldBase.Editor.ShowWhitespace) editor = editor with { ShowWhitespace = candidate.Editor.ShowWhitespace };
        if (candidate.Editor.ShowTabs != oldBase.Editor.ShowTabs) editor = editor with { ShowTabs = candidate.Editor.ShowTabs };
        if (candidate.Editor.ShowSpaces != oldBase.Editor.ShowSpaces) editor = editor with { ShowSpaces = candidate.Editor.ShowSpaces };
        if (candidate.Editor.FormatOnSave != oldBase.Editor.FormatOnSave) editor = editor with { FormatOnSave = candidate.Editor.FormatOnSave };
        var llm = current.Llm;
        if (candidate.Llm.BaseUrl != oldBase.Llm.BaseUrl) llm = llm with { BaseUrl = candidate.Llm.BaseUrl };
        if (candidate.Llm.Model != oldBase.Llm.Model) llm = llm with { Model = candidate.Llm.Model };
        if (candidate.Llm.ApiKeySource != oldBase.Llm.ApiKeySource) llm = llm with { ApiKeySource = candidate.Llm.ApiKeySource };
        var agents = current.Agents;
        if (candidate.Agents.TraceCaptureEnabled != oldBase.Agents.TraceCaptureEnabled)
            agents = agents with { TraceCaptureEnabled = candidate.Agents.TraceCaptureEnabled };
        if (candidate.Agents.UsageCaptureEnabled != oldBase.Agents.UsageCaptureEnabled)
            agents = agents with { UsageCaptureEnabled = candidate.Agents.UsageCaptureEnabled };
        if (candidate.Agents.TracePageSize != oldBase.Agents.TracePageSize)
            agents = agents with { TracePageSize = candidate.Agents.TracePageSize };
        if (candidate.Agents.TraceMaxPageSize != oldBase.Agents.TraceMaxPageSize)
            agents = agents with { TraceMaxPageSize = candidate.Agents.TraceMaxPageSize };
        if (candidate.Agents.AcpExecutablePath != oldBase.Agents.AcpExecutablePath)
            agents = agents with { AcpExecutablePath = candidate.Agents.AcpExecutablePath };
        if (candidate.Agents.AcpArguments != oldBase.Agents.AcpArguments)
            agents = agents with { AcpArguments = candidate.Agents.AcpArguments };
        if (candidate.Agents.AcpExpectedAgentName != oldBase.Agents.AcpExpectedAgentName)
            agents = agents with { AcpExpectedAgentName = candidate.Agents.AcpExpectedAgentName };
        if (candidate.Agents.AcpExpectedAgentVersion != oldBase.Agents.AcpExpectedAgentVersion)
            agents = agents with { AcpExpectedAgentVersion = candidate.Agents.AcpExpectedAgentVersion };
        if (candidate.Agents.DefaultContextPolicyLevel != oldBase.Agents.DefaultContextPolicyLevel)
            agents = agents with { DefaultContextPolicyLevel = candidate.Agents.DefaultContextPolicyLevel };
        return current with { Editor = editor, Llm = llm, Agents = agents };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _subscriptions.Dispose();
        CloseRequested = null;
    }
}
