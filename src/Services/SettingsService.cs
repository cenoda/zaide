using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// Thread-safe singleton implementation of <see cref="ISettingsService"/>.
///
/// **Construction:** Loads synchronously; <see cref="ISettingsService.Current"/>
/// is never null after the constructor returns.
///
/// **Mutation gate:** A private <see cref="SemaphoreSlim"/> serializes every
/// <see cref="UpdateAsync"/> / <see cref="ApplyAsync"/> transaction
/// (read–modify–validate–publish). This guarantees that concurrent disjoint-field
/// producers compose correctly and that <see cref="ApplyAsync"/> never overwrites
/// a concurrent change.
///
/// **Queued writer:** A single background loop drains a bounded
/// <see cref="System.Threading.Channels.Channel{T}"/>. Writes are generation-aware:
/// if a newer mutation has already been committed, the older queued write is skipped
/// (<see cref="SettingsSaveResult.Superseded"/>). Disk failures surface via
/// <see cref="ISettingsService.WriteErrors"/> and the per-item
/// <see cref="TaskCompletionSource{T}"/>.
///
/// **Atomic writes:** A same-directory temp file is written first, then renamed
/// over the target. On success, the last-known-good file is updated.
///
/// **Cancellation contract:** Caller tokens are observed only before the
/// mutation gate is acquired or before a save is enqueued. Once committed,
/// caller cancellation is ignored.
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    // ── Volatile backing for lock-free Current reads ──────────────────────
    private volatile SettingsModel _current;

    // ── Generation counter (Interlocked for writer-loop safety) ───────────
    private long _generation;

    // ── Mutation gate ────────────────────────────────────────────────────
    private readonly SemaphoreSlim _mutationGate = new(1, 1);

    // ── Queued writer (unbounded — never drops items, never strands callers) ─
    private readonly System.Threading.Channels.Channel<WriteItem> _writeQueue =
        System.Threading.Channels.Channel.CreateUnbounded<WriteItem>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                AllowSynchronousContinuations = false,
            });

    private sealed record WriteItem(
        long Generation,
        TaskCompletionSource<SettingsSaveResult> Completion);

    // ── Observables ──────────────────────────────────────────────────────
    private readonly System.Reactive.Subjects.Subject<SettingsModel> _whenChanged = new();
    private readonly System.Reactive.Subjects.Subject<SettingsSaveError> _writeErrors = new();

    // ── File paths ───────────────────────────────────────────────────────
    private readonly string _settingsPath;
    private readonly string _lastKnownGoodPath;
    private readonly string _tempPath;

    // ── Migrator ─────────────────────────────────────────────────────────
    private readonly SettingsMigrator _migrator;

    // ── Test hook — called before each write-item is processed ───────────
    internal Func<Task>? OnBeforeWriteItem { get; set; }

    // ── Shutdown ─────────────────────────────────────────────────────────
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _writerLoopTask;
    private bool _disposed;

    // ── Load result ──────────────────────────────────────────────────────
    private readonly SettingsLoadResult _loadResult;

    // ── Public API ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public SettingsModel Current => _current;

    /// <inheritdoc />
    public IObservable<SettingsModel> WhenChanged => _whenChanged;

    /// <inheritdoc />
    public SettingsLoadResult LoadResult => _loadResult;

    /// <inheritdoc />
    public IObservable<SettingsSaveError> WriteErrors => _writeErrors;

    // ── Constructors ─────────────────────────────────────────────────────

    /// <summary>
    /// Production constructor. Resolves paths via <see cref="SettingsPathResolver"/>
    /// and uses an empty migration list.
    /// </summary>
    public SettingsService()
        : this(SettingsPathResolver.GetSettingsPath(),
               SettingsPathResolver.GetLastKnownGoodPath(),
               SettingsPathResolver.GetTempPath(),
               new SettingsMigrator(Array.Empty<ISettingsMigration>()))
    {
    }

    /// <summary>
    /// Test-only constructor that accepts explicit file paths and a migrator.
    /// </summary>
    internal SettingsService(
        string settingsPath,
        string lastKnownGoodPath,
        string tempPath,
        SettingsMigrator migrator)
    {
        _settingsPath = settingsPath;
        _lastKnownGoodPath = lastKnownGoodPath;
        _tempPath = tempPath;
        _migrator = migrator;

        // Synchronous load — Current is never null after this point.
        SettingsLoadResult loadResult;
        SettingsModel? loaded;
        try
        {
            loaded = TryLoadFrom(_settingsPath, out loadResult);
        }
        catch
        {
            loaded = null;
            loadResult = SettingsLoadResult.Corrupt;
        }

        _current = loaded ?? SettingsModel.Defaults;
        _loadResult = loaded is not null && loadResult == SettingsLoadResult.Loaded
            ? SettingsLoadResult.Loaded
            : loadResult;

        // Update last-known-good when we loaded successfully from the real file.
        if (loadResult == SettingsLoadResult.Loaded && loaded is not null)
        {
            SaveLastKnownGood(loaded);
        }

        // Start the background writer loop.
        _writerLoopTask = Task.Factory.StartNew(
            () => WriterLoopAsync(_shutdownCts.Token),
            _shutdownCts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)
            .Unwrap();
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SettingsMutationResult> UpdateAsync(
        Func<SettingsModel, SettingsModel> producer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(producer);

        ct.ThrowIfCancellationRequested();

        TaskCompletionSource<SettingsSaveResult>? tcs = null;
        SettingsModel? next = null;

        await _mutationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Cancellation still honoured before we modify state.
            ct.ThrowIfCancellationRequested();

            var current = _current;
            next = producer(current);

            var errors = SettingsValidator.Validate(next);
            if (errors.Count > 0)
            {
                return new SettingsMutationResult.Invalid(next, errors);
            }

            // Commit in-memory (volatile field — assignment is a volatile write).
            var generation = Interlocked.Increment(ref _generation);
            _current = next;
            _whenChanged.OnNext(next);

            // Enqueue write.
            tcs = new TaskCompletionSource<SettingsSaveResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _writeQueue.Writer.TryWrite(new WriteItem(generation, tcs));
        }
        finally
        {
            // Release the gate before awaiting the write. This allows concurrent
            // mutations to enqueue writes while the first call's write is still
            // pending, which is necessary for generation-based supersession.
            _mutationGate.Release();
        }

        // Await write outcome outside the gate (caller cancellation is ignored
        // after commit — the writer uses the shutdown token, not ct).
        var saveResult = await tcs!.Task.ConfigureAwait(false);
        return new SettingsMutationResult.Applied(next!, saveResult);
    }

    // ── ApplyAsync ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SettingsMutationResult> ApplyAsync(
        SettingsModel expectedCurrent,
        SettingsModel next,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(expectedCurrent);
        ArgumentNullException.ThrowIfNull(next);

        ct.ThrowIfCancellationRequested();

        TaskCompletionSource<SettingsSaveResult>? tcs = null;

        await _mutationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();

            // Reference-identity check — must be the exact same snapshot object.
            if (!ReferenceEquals(expectedCurrent, _current))
            {
                return new SettingsMutationResult.Conflict(_current);
            }

            var errors = SettingsValidator.Validate(next);
            if (errors.Count > 0)
            {
                return new SettingsMutationResult.Invalid(next, errors);
            }

            // Commit in-memory (volatile field — assignment is a volatile write).
            var generation = Interlocked.Increment(ref _generation);
            _current = next;
            _whenChanged.OnNext(next);

            tcs = new TaskCompletionSource<SettingsSaveResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _writeQueue.Writer.TryWrite(new WriteItem(generation, tcs));
        }
        finally
        {
            _mutationGate.Release();
        }

        var saveResult = await tcs!.Task.ConfigureAwait(false);
        return new SettingsMutationResult.Applied(next, saveResult);
    }



    /// <inheritdoc />
    public async Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var tcs = new TaskCompletionSource<SettingsSaveResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Capture the live generation so the writer can determine freshness.
        var generation = Interlocked.Read(ref _generation);
        _writeQueue.Writer.TryWrite(new WriteItem(generation, tcs));

        return await tcs.Task.ConfigureAwait(false);
    }

    // ── Writer loop ──────────────────────────────────────────────────────

    private async Task WriterLoopAsync(CancellationToken ct)
    {
        // Yield immediately so construction can complete.
        await Task.Yield();

        try
        {
            await foreach (var item in _writeQueue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                // Test hook — allows deterministic write-ordering tests.
                if (OnBeforeWriteItem is not null)
                    await OnBeforeWriteItem().ConfigureAwait(false);

                try
                {
                    var currentGen = Interlocked.Read(ref _generation);
                    if (item.Generation < currentGen)
                    {
                        // A newer mutation has been committed — skip this write.
                        item.Completion.TrySetResult(new SettingsSaveResult.Superseded());
                        continue;
                    }

                    // Write the current in-memory snapshot.
                    var snapshot = _current;
                    var json = SettingsSerializer.Serialize(snapshot);

                    // Ensure directory exists.
                    var dir = Path.GetDirectoryName(_settingsPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    // Atomic write: temp → rename.
                    await File.WriteAllTextAsync(_tempPath, json, ct).ConfigureAwait(false);
                    File.Move(_tempPath, _settingsPath, overwrite: true);

                    // Update last-known-good.
                    await File.WriteAllTextAsync(_lastKnownGoodPath, json, ct).ConfigureAwait(false);

                    item.Completion.TrySetResult(new SettingsSaveResult.Saved());
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Shutdown — drain remaining items.
                    item.Completion.TrySetResult(new SettingsSaveResult.Failed(
                        new OperationCanceledException("Service is shutting down.")));
                    while (_writeQueue.Reader.TryRead(out var remaining))
                    {
                        remaining.Completion.TrySetResult(new SettingsSaveResult.Failed(
                            new OperationCanceledException("Service is shutting down.")));
                    }
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    item.Completion.TrySetResult(new SettingsSaveResult.Failed(ex));
                    _writeErrors.OnNext(new SettingsSaveError(
                        ex, _current, DateTime.UtcNow));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Channel was completed; drain remaining items.
            while (_writeQueue.Reader.TryRead(out var item))
            {
                item.Completion.TrySetResult(new SettingsSaveResult.Failed(
                    new OperationCanceledException("Service is shutting down.")));
            }
        }
    }

    // ── Load helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to load settings from <paramref name="path"/>.
    /// Returns <c>null</c> and sets <paramref name="result"/> on failure.
    /// </summary>
    private SettingsModel? TryLoadFrom(string path, out SettingsLoadResult result)
    {
        if (!File.Exists(path))
        {
            result = SettingsLoadResult.Missing;
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var parsed = SettingsSerializer.Deserialize(json, out var schemaRejected);
            if (parsed is null)
            {
                if (schemaRejected)
                {
                    result = SettingsLoadResult.UnsupportedVersion;
                    // Try LKG but do not let the fallback overwrite the result.
                    return TryLoadLastKnownGood();
                }

                // Unparseable or structurally invalid.
                result = SettingsLoadResult.Corrupt;
                return TryFallbackToLastKnownGood(out result);
            }

            // Run migrations in case the file is from an older schema version.
            var migrated = _migrator.Migrate(parsed);

            result = SettingsLoadResult.Loaded;
            return migrated;
        }
        catch (JsonException)
        {
            result = SettingsLoadResult.Corrupt;
            return TryFallbackToLastKnownGood(out result);
        }
        catch (IOException)
        {
            result = SettingsLoadResult.Corrupt;
            return TryFallbackToLastKnownGood(out result);
        }
        catch (UnauthorizedAccessException)
        {
            result = SettingsLoadResult.Corrupt;
            return TryFallbackToLastKnownGood(out result);
        }
    }

    /// <summary>Load from LKG without overwriting the caller's result.</summary>
    private SettingsModel? TryLoadLastKnownGood()
    {
        if (!File.Exists(_lastKnownGoodPath))
            return null;

        try
        {
            var json = File.ReadAllText(_lastKnownGoodPath);
            return SettingsSerializer.Deserialize(json, out _);
        }
        catch
        {
            return null;
        }
    }

    private SettingsModel? TryFallbackToLastKnownGood(out SettingsLoadResult result)
    {
        if (File.Exists(_lastKnownGoodPath))
        {
            try
            {
                var json = File.ReadAllText(_lastKnownGoodPath);
                var parsed = SettingsSerializer.Deserialize(json, out _);
                if (parsed is not null)
                {
                    result = SettingsLoadResult.Corrupt; // original load failed
                    return parsed;
                }
            }
            catch
            {
                // Fall through to defaults.
            }
        }

        result = SettingsLoadResult.Corrupt;
        return null;
    }

    private void SaveLastKnownGood(SettingsModel model)
    {
        try
        {
            var json = SettingsSerializer.Serialize(model);
            var dir = Path.GetDirectoryName(_lastKnownGoodPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_lastKnownGoodPath, json);
        }
        catch
        {
            // Best-effort — failures here must not prevent construction.
        }
    }

    // ── Dispose ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdownCts.Cancel();
        _shutdownCts.Dispose();

        try { _writerLoopTask.GetAwaiter().GetResult(); } catch { /* swallow */ }

        _writeQueue.Writer.TryComplete();
        _mutationGate.Dispose();
        _whenChanged.Dispose();
        _writeErrors.Dispose();
    }
}
