using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Zaide.Services;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Tests.Services;

#region Test infrastructure

/// <summary>
/// Captures log entries for assertion in tests.
/// </summary>
public sealed class TestLoggerProvider : ILoggerProvider
{
    public List<LogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName)
        => new TestLogger(this, categoryName);

    public void Dispose()
    {
        Entries.Clear();
    }

    public sealed class LogEntry
    {
        public LogLevel Level { get; init; }
        public string Category { get; init; } = "";
        public string Message { get; init; } = "";
        public Exception? Exception { get; init; }
    }

    private sealed class TestLogger(TestLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            provider.Entries.Add(new LogEntry
            {
                Level = logLevel,
                Category = category,
                Message = formatter(state, exception),
                Exception = exception
            });
        }
    }
}

/// <summary>
/// Simple ICommand that always allows execution.
/// </summary>
internal sealed class AlwaysEnabledCommand : ICommand
{
    public int ExecutionCount { get; private set; }
    public object? LastParameter { get; private set; }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        ExecutionCount++;
        LastParameter = parameter;
    }
}

/// <summary>
/// Simple ICommand that never allows execution (unavailable).
/// </summary>
internal sealed class AlwaysDisabledCommand : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => false;
    public void Execute(object? parameter) => throw new InvalidOperationException("Should never be called");
}

/// <summary>
/// Parameterized ICommand that only accepts string parameters.
/// </summary>
internal sealed class StringOnlyCommand : ICommand
{
    public int ExecutionCount { get; private set; }
    public string? LastValue { get; private set; }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => parameter is string;

    public void Execute(object? parameter)
    {
        if (parameter is string s)
        {
            ExecutionCount++;
            LastValue = s;
        }
    }
}

/// <summary>
/// ICommand whose CanExecute throws.
/// </summary>
internal sealed class ThrowingCanExecuteCommand : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => throw new InvalidOperationException("CanExecute failure");
    public void Execute(object? parameter) { }
}

/// <summary>
/// ICommand whose Execute throws.
/// </summary>
internal sealed class ThrowingExecuteCommand : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => throw new InvalidOperationException("Execute failure");
}

#endregion

public sealed class CommandRegistryTests
{
    private readonly TestLoggerProvider _loggerProvider;
    private readonly CommandRegistry _registry;

    public CommandRegistryTests()
    {
        _loggerProvider = new TestLoggerProvider();
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(_loggerProvider));
        var logger = loggerFactory.CreateLogger<CommandRegistry>();
        _registry = new CommandRegistry(logger);
    }

    private static CommandDescriptor CreateDescriptor(
        string id = "test.command",
        string displayName = "Test Command",
        string category = "Test",
        IReadOnlyList<string>? defaultGestures = null,
        ICommand? command = null)
    {
        return new CommandDescriptor(
            id, displayName, category,
            defaultGestures ?? Array.Empty<string>(),
            command ?? new AlwaysEnabledCommand());
    }

    // ── CommandDescriptor validation ────────────────────────────────────

    [Fact]
    public void Descriptor_ValidInput_PreservesAllProperties()
    {
        var gestures = new[] { "Ctrl+S" };
        var cmd = new AlwaysEnabledCommand();
        var descriptor = new CommandDescriptor("file.save", "Save", "File", gestures, cmd);

        Assert.Equal("file.save", descriptor.Id);
        Assert.Equal("Save", descriptor.DisplayName);
        Assert.Equal("File", descriptor.Category);
        Assert.Equal(gestures, descriptor.DefaultGestures);
        Assert.Same(cmd, descriptor.Command);
    }

    [Fact]
    public void Descriptor_EmptyId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new CommandDescriptor("", "Save", "File", Array.Empty<string>(), new AlwaysEnabledCommand()));
    }

    [Fact]
    public void Descriptor_WhitespaceId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new CommandDescriptor("  ", "Save", "File", Array.Empty<string>(), new AlwaysEnabledCommand()));
    }

    [Fact]
    public void Descriptor_EmptyCategory_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new CommandDescriptor("file.save", "Save", "", Array.Empty<string>(), new AlwaysEnabledCommand()));
    }

    [Fact]
    public void Descriptor_WhitespaceCategory_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new CommandDescriptor("file.save", "Save", "  ", Array.Empty<string>(), new AlwaysEnabledCommand()));
    }

    [Fact]
    public void Descriptor_EmptyGestures_IsAllowed()
    {
        var descriptor = new CommandDescriptor(
            "file.save", "Save", "File", Array.Empty<string>(), new AlwaysEnabledCommand());

        Assert.Empty(descriptor.DefaultGestures);
    }

    [Fact]
    public void Descriptor_IsSealedClass()
    {
        Assert.True(typeof(CommandDescriptor).IsSealed);
        Assert.False(typeof(CommandDescriptor).IsValueType);
    }

    [Fact]
    public void Descriptor_DefensiveCopy_GesturesNotMutatedByExternalChange()
    {
        var mutableList = new List<string> { "Ctrl+S" };
        var descriptor = new CommandDescriptor("file.save", "Save", "File", mutableList, new AlwaysEnabledCommand());

        mutableList.Add("Ctrl+Shift+S");

        Assert.Single(descriptor.DefaultGestures);
        Assert.Equal("Ctrl+S", descriptor.DefaultGestures[0]);
    }

    // ── Duplicate registration ──────────────────────────────────────────

    [Fact]
    public void Register_DuplicateId_ThrowsInvalidOperationException()
    {
        _registry.Register(CreateDescriptor(id: "test.command"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _registry.Register(CreateDescriptor(id: "test.command")));

        Assert.Contains("test.command", ex.Message);
    }

    [Fact]
    public void Register_DuplicateId_DoesNotReplaceOriginal()
    {
        var original = new AlwaysEnabledCommand();
        var replacement = new AlwaysEnabledCommand();
        _registry.Register(CreateDescriptor(id: "test.command", command: original));

        Assert.Throws<InvalidOperationException>(() =>
            _registry.Register(CreateDescriptor(id: "test.command", command: replacement)));

        var found = _registry.GetById("test.command");
        Assert.Same(original, found!.Command);
    }

    // ── GetAll and GetById ──────────────────────────────────────────────

    [Fact]
    public void GetAll_EmptyRegistry_ReturnsEmptyList()
    {
        var all = _registry.GetAll();
        Assert.Empty(all);
    }

    [Fact]
    public void GetAll_AfterRegistration_ReturnsAllDescriptors()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a"));
        _registry.Register(CreateDescriptor(id: "cmd.b"));
        _registry.Register(CreateDescriptor(id: "cmd.c"));

        var all = _registry.GetAll();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, d => d.Id == "cmd.a");
        Assert.Contains(all, d => d.Id == "cmd.b");
        Assert.Contains(all, d => d.Id == "cmd.c");
    }

    [Fact]
    public void GetById_RegisteredCommand_ReturnsDescriptor()
    {
        _registry.Register(CreateDescriptor(id: "test.command"));

        var result = _registry.GetById("test.command");

        Assert.NotNull(result);
        Assert.Equal("test.command", result.Id);
    }

    [Fact]
    public void GetById_UnknownCommand_ReturnsNull()
    {
        var result = _registry.GetById("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public void GetById_NullId_ReturnsNull()
    {
        var result = _registry.GetById(null!);
        Assert.Null(result);
    }

    [Fact]
    public void GetById_EmptyId_ReturnsNull()
    {
        var result = _registry.GetById("");
        Assert.Null(result);
    }

    // ── Parameterless command execution ─────────────────────────────────

    [Fact]
    public void Execute_RegisteredAvailableCommand_ReturnsTrueAndExecutes()
    {
        var cmd = new AlwaysEnabledCommand();
        _registry.Register(CreateDescriptor(id: "test.command", command: cmd));

        var result = _registry.Execute("test.command");

        Assert.True(result);
        Assert.Equal(1, cmd.ExecutionCount);
    }

    [Fact]
    public void Execute_PassesNullParameter()
    {
        var cmd = new AlwaysEnabledCommand();
        _registry.Register(CreateDescriptor(id: "test.command", command: cmd));

        _registry.Execute("test.command");

        Assert.Null(cmd.LastParameter);
    }

    // ── Typed command execution ─────────────────────────────────────────

    [Fact]
    public void ExecuteTyped_RegisteredAvailableCommand_ReturnsTrueAndExecutes()
    {
        var cmd = new StringOnlyCommand();
        _registry.Register(CreateDescriptor(id: "test.command", command: cmd));

        var result = _registry.Execute<string>("test.command", "hello");

        Assert.True(result);
        Assert.Equal(1, cmd.ExecutionCount);
        Assert.Equal("hello", cmd.LastValue);
    }

    [Fact]
    public void ExecuteTyped_CanExecuteFalse_ReturnsFalse()
    {
        var cmd = new AlwaysDisabledCommand();
        _registry.Register(CreateDescriptor(id: "test.command", command: cmd));

        var result = _registry.Execute<string>("test.command", "hello");

        Assert.False(result);
    }

    // ── Unknown command handling ────────────────────────────────────────

    [Fact]
    public void Execute_UnknownCommand_ReturnsFalse()
    {
        Assert.False(_registry.Execute("nonexistent"));
    }

    [Fact]
    public void ExecuteTyped_UnknownCommand_ReturnsFalse()
    {
        Assert.False(_registry.Execute<string>("nonexistent", "value"));
    }

    [Fact]
    public void Execute_NullCommandId_ReturnsFalse()
    {
        Assert.False(_registry.Execute(null!));
    }

    // ── Unavailable command handling ────────────────────────────────────

    [Fact]
    public void Execute_UnavailableCommand_ReturnsFalse()
    {
        _registry.Register(CreateDescriptor(id: "test.command", command: new AlwaysDisabledCommand()));

        Assert.False(_registry.Execute("test.command"));
    }

    [Fact]
    public void Execute_UnavailableCommand_DoesNotThrow()
    {
        _registry.Register(CreateDescriptor(id: "test.command", command: new AlwaysDisabledCommand()));

        var ex = Record.Exception(() => _registry.Execute("test.command"));
        Assert.Null(ex);
    }

    // ── Wrong parameter handling without exceptions ─────────────────────

    [Fact]
    public void ExecuteTyped_WrongParameterType_ReturnsFalse()
    {
        var cmd = new StringOnlyCommand();
        _registry.Register(CreateDescriptor(id: "test.command", command: cmd));

        var result = _registry.Execute<int>("test.command", 42);

        Assert.False(result);
        Assert.Equal(0, cmd.ExecutionCount);
    }

    [Fact]
    public void ExecuteTyped_WrongParameterType_DoesNotThrow()
    {
        var cmd = new StringOnlyCommand();
        _registry.Register(CreateDescriptor(id: "test.command", command: cmd));

        var ex = Record.Exception(() => _registry.Execute<int>("test.command", 42));
        Assert.Null(ex);
    }

    // ── Never-throws: underlying command exceptions are caught ──────────

    [Fact]
    public void Execute_CanExecuteThrows_ReturnsFalse()
    {
        _registry.Register(CreateDescriptor(id: "test.command", command: new ThrowingCanExecuteCommand()));

        var result = _registry.Execute("test.command");

        Assert.False(result);
    }

    [Fact]
    public void Execute_ExecuteThrows_ReturnsFalse()
    {
        _registry.Register(CreateDescriptor(id: "test.command", command: new ThrowingExecuteCommand()));

        var result = _registry.Execute("test.command");

        Assert.False(result);
    }

    [Fact]
    public void ExecuteTyped_CanExecuteThrows_ReturnsFalse()
    {
        _registry.Register(CreateDescriptor(id: "test.command", command: new ThrowingCanExecuteCommand()));

        var result = _registry.Execute<string>("test.command", "value");

        Assert.False(result);
    }

    [Fact]
    public void ExecuteTyped_ExecuteThrows_ReturnsFalse()
    {
        _registry.Register(CreateDescriptor(id: "test.command", command: new ThrowingExecuteCommand()));

        var result = _registry.Execute<string>("test.command", "value");

        Assert.False(result);
    }

    [Fact]
    public void Execute_CommandThrows_LogsWarning()
    {
        _registry.Register(CreateDescriptor(id: "test.command", command: new ThrowingExecuteCommand()));

        _registry.Execute("test.command");

        var warningEntries = _loggerProvider.Entries
            .Where(e => e.Level == LogLevel.Warning)
            .ToList();

        Assert.NotEmpty(warningEntries);
        Assert.Contains(warningEntries, e =>
            e.Message.Contains("test.command") && e.Exception is not null);
    }

    // ── ILogger injection and DI singleton resolution ───────────────────

    [Fact]
    public void DI_ILoggerInjection_ResolvesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(new TestLoggerProvider()));
        services.AddSingleton<ICommandRegistry, CommandRegistry>();

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ICommandRegistry>();

        Assert.NotNull(registry);
        Assert.IsType<CommandRegistry>(registry);
    }

    [Fact]
    public void DI_SingletonResolution_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(new TestLoggerProvider()));
        services.AddSingleton<ICommandRegistry, CommandRegistry>();

        using var sp = services.BuildServiceProvider();
        var first = sp.GetRequiredService<ICommandRegistry>();
        var second = sp.GetRequiredService<ICommandRegistry>();

        Assert.Same(first, second);
    }

    // ── Debug logging for unknown/unavailable commands ──────────────────

    [Fact]
    public void DebugLog_UnknownCommand_LoggedAtDebugLevel()
    {
        _registry.Execute("nonexistent.command");

        var debugEntries = _loggerProvider.Entries
            .Where(e => e.Level == LogLevel.Debug)
            .ToList();

        Assert.NotEmpty(debugEntries);
        Assert.Contains(debugEntries, e => e.Message.Contains("nonexistent.command"));
    }

    [Fact]
    public void DebugLog_UnavailableCommand_LoggedAtDebugLevel()
    {
        _registry.Register(CreateDescriptor(id: "test.command", command: new AlwaysDisabledCommand()));

        _registry.Execute("test.command");

        var debugEntries = _loggerProvider.Entries
            .Where(e => e.Level == LogLevel.Debug)
            .ToList();

        Assert.NotEmpty(debugEntries);
        Assert.Contains(debugEntries, e =>
            e.Message.Contains("test.command") &&
            e.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DebugLog_UnknownCommand_ExecuteTyped_LoggedAtDebugLevel()
    {
        _registry.Execute<int>("nonexistent", 42);

        var debugEntries = _loggerProvider.Entries
            .Where(e => e.Level == LogLevel.Debug)
            .ToList();

        Assert.NotEmpty(debugEntries);
        Assert.Contains(debugEntries, e => e.Message.Contains("nonexistent"));
    }

    [Fact]
    public void DebugLog_WrongParameterType_LoggedAtDebugLevel()
    {
        var cmd = new StringOnlyCommand();
        _registry.Register(CreateDescriptor(id: "test.command", command: cmd));

        _registry.Execute<int>("test.command", 42);

        var debugEntries = _loggerProvider.Entries
            .Where(e => e.Level == LogLevel.Debug)
            .ToList();

        Assert.NotEmpty(debugEntries);
        Assert.Contains(debugEntries, e =>
            e.Message.Contains("test.command") && e.Message.Contains("CanExecute"));
    }

    // ── Warning logging through test logger provider ────────────────────

    [Fact]
    public void WarningLog_InvalidGesture_LoggedAtWarningLevel()
    {
        _registry.ValidateGestureFormat("");

        var warningEntries = _loggerProvider.Entries
            .Where(e => e.Level == LogLevel.Warning)
            .ToList();

        Assert.NotEmpty(warningEntries);
        Assert.Contains(warningEntries, e =>
            e.Message.Contains("Invalid gesture", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WarningLog_GestureConflict_LoggedAtWarningLevel()
    {
        _registry.LogGestureConflict("Ctrl+S", "cmd.a", "cmd.b");

        var warningEntries = _loggerProvider.Entries
            .Where(e => e.Level == LogLevel.Warning)
            .ToList();

        Assert.NotEmpty(warningEntries);
        Assert.Contains(warningEntries, e =>
            e.Message.Contains("Ctrl+S") &&
            e.Message.Contains("cmd.a") &&
            e.Message.Contains("cmd.b"));
    }

    [Fact]
    public void WarningLog_ValidGesture_DoesNotLogWarning()
    {
        _registry.ValidateGestureFormat("Ctrl+S");

        var warningEntries = _loggerProvider.Entries
            .Where(e => e.Level == LogLevel.Warning)
            .ToList();

        Assert.Empty(warningEntries);
    }

    // ── ResolveKeyBindings M7a no-registration behavior ─────────────────

    [Fact]
    public void ResolveKeyBindings_EmptyRegistry_ReturnsEmpty()
    {
        var settings = new NoOpSettingsService();
        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Empty(bindings);
    }

    // ── ResolvedKeyBinding record ───────────────────────────────────────

    [Fact]
    public void ResolvedKeyBinding_PreservesProperties()
    {
        var binding = new ResolvedKeyBinding("Ctrl+S", "file.save");

        Assert.Equal("Ctrl+S", binding.Gesture);
        Assert.Equal("file.save", binding.CommandId);
    }

    [Fact]
    public void ResolvedKeyBinding_IsSealedRecord()
    {
        Assert.True(typeof(ResolvedKeyBinding).IsSealed);
    }

    // ── M8b Neutral gesture resolution ─────────────────────────────────

    #region Helpers

    private static ISettingsService CreateSettings(
        IReadOnlyDictionary<string, string>? overrides = null)
    {
        var keybindings = overrides is not null
            ? new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(overrides))
            : SettingsModel.Defaults.Keybindings;

        var model = new SettingsModel(
            SettingsModel.Defaults.SchemaVersion,
            SettingsModel.Defaults.Editor,
            SettingsModel.Defaults.Llm,
            keybindings,
            SettingsModel.Defaults.Debug);

        return new SimpleSettingsService(model);
    }

    private sealed class SimpleSettingsService : ISettingsService
    {
        private readonly SettingsModel _model;

        public SimpleSettingsService(SettingsModel model)
        {
            _model = model;
        }

        public SettingsModel Current => _model;
        public IObservable<SettingsModel> WhenChanged
            => System.Reactive.Linq.Observable.Empty<SettingsModel>();
        public SettingsLoadResult LoadResult => SettingsLoadResult.Missing;

        public System.Threading.Tasks.Task<SettingsMutationResult> UpdateAsync(
            Func<SettingsModel, SettingsModel> producer,
            System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.FromResult<SettingsMutationResult>(
                new SettingsMutationResult.Conflict(_model));

        public System.Threading.Tasks.Task<SettingsMutationResult> ApplyAsync(
            SettingsModel expectedCurrent,
            SettingsModel next,
            System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.FromResult<SettingsMutationResult>(
                new SettingsMutationResult.Conflict(_model));

        public System.Threading.Tasks.Task<SettingsSaveResult> SaveAsync(
            System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.FromResult<SettingsSaveResult>(new SettingsSaveResult.Saved());

        public IObservable<SettingsSaveError> WriteErrors
            => System.Reactive.Linq.Observable.Empty<SettingsSaveError>();
    }

    #endregion

    // ── Valid grammar and case-insensitive parsing ──────────────────────

    [Fact]
    public void ResolveKeyBindings_CaseInsensitiveGesture_ParsesToCanonicalForm()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "ctrl+shift+h" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+Shift+H", bindings[0].Gesture);
        Assert.Equal("cmd.a", bindings[0].CommandId);
    }

    [Fact]
    public void ResolveKeyBindings_MixedCaseGesture_ParsesToCanonicalForm()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "CTRL+Oem3" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+Oem3", bindings[0].Gesture);
        Assert.Equal("cmd.a", bindings[0].CommandId);
    }

    [Fact]
    public void ResolveKeyBindings_AllModifiers_ParsesCorrectly()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "ctrl+alt+shift+meta+a" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+Alt+Shift+Meta+A", bindings[0].Gesture);
        Assert.Equal("cmd.a", bindings[0].CommandId);
    }

    // ── Modifier/key parsing ───────────────────────────────────────────

    [Fact]
    public void ResolveKeyBindings_SingleModifier_Resolves()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);
    }

    [Fact]
    public void ResolveKeyBindings_MultipleModifiers_Resolves()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+Shift+S" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+Shift+S", bindings[0].Gesture);
    }

    [Fact]
    public void ResolveKeyBindings_InvalidModifier_IgnoredAndLogged()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Super+S" }));
        _registry.Register(CreateDescriptor(id: "cmd.b", defaultGestures: new[] { "Ctrl+O" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+O", bindings[0].Gesture);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("Invalid default gesture") &&
            e.Message.Contains("cmd.a"));
    }

    [Fact]
    public void ResolveKeyBindings_InvalidKey_IgnoredAndLogged()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+NotAKey" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Empty(bindings);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("Invalid default gesture") &&
            e.Message.Contains("cmd.a"));
    }

    // ── Default resolution ─────────────────────────────────────────────

    [Fact]
    public void ResolveKeyBindings_DefaultsResolveToCorrectCommands()
    {
        _registry.Register(CreateDescriptor(id: "file.save", defaultGestures: new[] { "Ctrl+S" }));
        _registry.Register(CreateDescriptor(id: "workspace.openFolder", defaultGestures: new[] { "Ctrl+O" }));
        _registry.Register(CreateDescriptor(id: "view.toggleBottomPanel", defaultGestures: new[] { "Ctrl+Oem3", "Ctrl+J" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Equal(4, bindings.Count);
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+S" && b.CommandId == "file.save");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+O" && b.CommandId == "workspace.openFolder");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+Oem3" && b.CommandId == "view.toggleBottomPanel");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+J" && b.CommandId == "view.toggleBottomPanel");
    }

    [Fact]
    public void ResolveKeyBindings_EmptyDefaultGestures_NoBinding()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: Array.Empty<string>()));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Empty(bindings);
    }

    // ── User override precedence ───────────────────────────────────────

    [Fact]
    public void ResolveKeyBindings_UserOverride_OverridesDefault()
    {
        _registry.Register(CreateDescriptor(id: "file.save", defaultGestures: new[] { "Ctrl+S" }));

        var settings = CreateSettings(new Dictionary<string, string>
        {
            ["file.save"] = "Ctrl+Shift+S"
        });

        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Single(bindings);
        Assert.Equal("Ctrl+Shift+S", bindings[0].Gesture);
        Assert.Equal("file.save", bindings[0].CommandId);
    }

    // ── Explicit empty-string unbind ───────────────────────────────────

    [Fact]
    public void ResolveKeyBindings_EmptyStringOverride_UnbindsCommand()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S" }));
        _registry.Register(CreateDescriptor(id: "cmd.b", defaultGestures: new[] { "Ctrl+O" }));

        var settings = CreateSettings(new Dictionary<string, string>
        {
            ["cmd.a"] = ""
        });

        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Single(bindings);
        Assert.Equal("Ctrl+O", bindings[0].Gesture);
        Assert.Equal("cmd.b", bindings[0].CommandId);
    }

    [Fact]
    public void ResolveKeyBindings_EmptyStringOverride_RemovesDefaultBinding()
    {
        _registry.Register(CreateDescriptor(id: "file.save", defaultGestures: new[] { "Ctrl+S" }));

        var settings = CreateSettings(new Dictionary<string, string>
        {
            ["file.save"] = ""
        });

        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.DoesNotContain(bindings, b => b.CommandId == "file.save");
    }

    [Fact]
    public void ResolveKeyBindings_NullOverride_InvalidLoggedNotUnbound()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S" }));

        var settings = CreateSettings(new Dictionary<string, string>
        {
            ["cmd.a"] = null!
        });

        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("Invalid override gesture") &&
            e.Message.Contains("cmd.a"));
    }

    [Fact]
    public void ResolveKeyBindings_WhitespaceOverride_InvalidLoggedNotUnbound()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S" }));

        var settings = CreateSettings(new Dictionary<string, string>
        {
            ["cmd.a"] = "   "
        });

        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("Invalid override gesture") &&
            e.Message.Contains("cmd.a"));
    }

    [Fact]
    public void ResolveKeyBindings_NumericKeyToken_Rejected()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S" }));
        _registry.Register(CreateDescriptor(id: "cmd.b", defaultGestures: new[] { "Ctrl+1" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("Invalid default gesture") &&
            e.Message.Contains("cmd.b"));
    }

    [Fact]
    public void ResolveKeyBindings_WhitespaceDefaultGesture_InvalidLogged()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "   " }));
        _registry.Register(CreateDescriptor(id: "cmd.b", defaultGestures: new[] { "Ctrl+S" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("Invalid default gesture") &&
            e.Message.Contains("cmd.a"));
    }

    // ── Unknown command IDs in settings ────────────────────────────────

    [Fact]
    public void ResolveKeyBindings_UnknownCommandIdInSettings_IgnoredAndLogged()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S" }));

        var settings = CreateSettings(new Dictionary<string, string>
        {
            ["nonexistent.command"] = "Ctrl+Shift+S"
        });

        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);
        Assert.Equal("cmd.a", bindings[0].CommandId);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("unregistered command ID") &&
            e.Message.Contains("nonexistent.command"));
    }

    [Fact]
    public void ResolveKeyBindings_InvalidOverrideGesture_IgnoredAndFallsBackToDefault()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S" }));

        var settings = CreateSettings(new Dictionary<string, string>
        {
            ["cmd.a"] = "Invalid+Gesture"
        });

        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("Invalid override gesture") &&
            e.Message.Contains("cmd.a"));
    }

    // ── Deterministic conflict handling ────────────────────────────────

    [Fact]
    public void ResolveKeyBindings_UserOverrideConflict_LexicographicallyEarlierWins()
    {
        _registry.Register(CreateDescriptor(id: "cmd.b", defaultGestures: new[] { "Ctrl+S" }));
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+O" }));

        var settings = CreateSettings(new Dictionary<string, string>
        {
            ["cmd.a"] = "Ctrl+S",
            ["cmd.b"] = "Ctrl+S"
        });

        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);
        Assert.Equal("cmd.a", bindings[0].CommandId);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("cmd.a") &&
            e.Message.Contains("cmd.b"));
    }

    [Fact]
    public void ResolveKeyBindings_DefaultGestureConflict_LexicographicallyEarlierWins()
    {
        _registry.Register(CreateDescriptor(id: "cmd.b", defaultGestures: new[] { "Ctrl+S" }));
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);
        Assert.Equal("cmd.a", bindings[0].CommandId);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("cmd.a") &&
            e.Message.Contains("cmd.b"));
    }

    [Fact]
    public void ResolveKeyBindings_UserOverrideBeatsDefault()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+O" }));
        _registry.Register(CreateDescriptor(id: "cmd.b", defaultGestures: new[] { "Ctrl+S" }));

        var settings = CreateSettings(new Dictionary<string, string>
        {
            ["cmd.a"] = "Ctrl+S"
        });

        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);
        Assert.Equal("cmd.a", bindings[0].CommandId);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("cmd.a") &&
            e.Message.Contains("cmd.b"));
    }

    [Fact]
    public void ResolveKeyBindings_UserOverrideConflict_IndependentOfRegistrationOrder()
    {
        _registry.Register(CreateDescriptor(id: "z.command", defaultGestures: new[] { "Ctrl+S" }));
        _registry.Register(CreateDescriptor(id: "a.command", defaultGestures: new[] { "Ctrl+O" }));

        var settings = CreateSettings(new Dictionary<string, string>
        {
            ["z.command"] = "Ctrl+O",
            ["a.command"] = "Ctrl+O"
        });

        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Single(bindings);
        Assert.Equal("Ctrl+O", bindings[0].Gesture);
        Assert.Equal("a.command", bindings[0].CommandId);
    }

    [Fact]
    public void ResolveKeyBindings_DefaultConflict_IndependentOfRegistrationOrder()
    {
        _registry.Register(CreateDescriptor(id: "z.command", defaultGestures: new[] { "Ctrl+S" }));
        _registry.Register(CreateDescriptor(id: "a.command", defaultGestures: new[] { "Ctrl+S" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);
        Assert.Equal("a.command", bindings[0].CommandId);
    }

    // ── Warning logs for ignored invalid input and conflicts ────────────

    [Fact]
    public void ResolveKeyBindings_InvalidDefaultGesture_LogsWarning()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Bad+Gesture" }));
        _registry.Register(CreateDescriptor(id: "cmd.b", defaultGestures: new[] { "Ctrl+S" }));

        _registry.ResolveKeyBindings(CreateSettings());

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("Invalid default gesture") &&
            e.Message.Contains("cmd.a"));
    }

    [Fact]
    public void ResolveKeyBindings_Conflict_LogsWarning()
    {
        _registry.Register(CreateDescriptor(id: "cmd.b", defaultGestures: new[] { "Ctrl+S" }));
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S" }));

        _registry.ResolveKeyBindings(CreateSettings());

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, e =>
            e.Message.Contains("Gesture conflict") &&
            e.Message.Contains("cmd.a") &&
            e.Message.Contains("cmd.b"));
    }

    // ── Repeated resolution without duplicate output ────────────────────

    [Fact]
    public void ResolveKeyBindings_RepeatedCalls_CompleteReplacementNoDuplicates()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S" }));

        var settings = CreateSettings();
        var first = _registry.ResolveKeyBindings(settings);
        var second = _registry.ResolveKeyBindings(settings);

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ResolveKeyBindings_DuplicateDefaultsSameCommand_NoDuplicateBindings()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S", "Ctrl+S" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Single(bindings);
        Assert.Equal("Ctrl+S", bindings[0].Gesture);
        Assert.Equal("cmd.a", bindings[0].CommandId);
    }

    [Fact]
    public void ResolveKeyBindings_MultipleDistinctDefaults_AllResolve()
    {
        _registry.Register(CreateDescriptor(id: "cmd.a", defaultGestures: new[] { "Ctrl+S", "Ctrl+Shift+S", "Ctrl+Alt+S" }));

        var bindings = _registry.ResolveKeyBindings(CreateSettings());

        Assert.Equal(3, bindings.Count);
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+S");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+Shift+S");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+Alt+S");
    }
}

/// <summary>
/// Minimal ISettingsService stub for M7a tests. M8 owns the real resolution contract.
/// </summary>
internal sealed class NoOpSettingsService : ISettingsService
{
    public SettingsModel Current => SettingsModel.Defaults;
    public System.IObservable<SettingsModel> WhenChanged
        => System.Reactive.Linq.Observable.Empty<SettingsModel>();
    public SettingsLoadResult LoadResult => SettingsLoadResult.Missing;

    public System.Threading.Tasks.Task<SettingsMutationResult> UpdateAsync(
        Func<SettingsModel, SettingsModel> producer,
        System.Threading.CancellationToken ct = default)
        => System.Threading.Tasks.Task.FromResult<SettingsMutationResult>(
            new SettingsMutationResult.Conflict(SettingsModel.Defaults));

    public System.Threading.Tasks.Task<SettingsMutationResult> ApplyAsync(
        SettingsModel expectedCurrent,
        SettingsModel next,
        System.Threading.CancellationToken ct = default)
        => System.Threading.Tasks.Task.FromResult<SettingsMutationResult>(
            new SettingsMutationResult.Conflict(SettingsModel.Defaults));

    public System.Threading.Tasks.Task<SettingsSaveResult> SaveAsync(
        System.Threading.CancellationToken ct = default)
        => System.Threading.Tasks.Task.FromResult<SettingsSaveResult>(new SettingsSaveResult.Saved());

    public System.IObservable<SettingsSaveError> WriteErrors
        => System.Reactive.Linq.Observable.Empty<SettingsSaveError>();
}
