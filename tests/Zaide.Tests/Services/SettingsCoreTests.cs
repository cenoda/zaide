using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 8.1.1 / M1 core behaviour tests: immutable snapshots, validation,
/// concurrency, cancellation, write-error publication, and save/retry.
/// </summary>
public sealed class SettingsCoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly string _lastKnownGoodPath;
    private readonly string _tempPath;

    public SettingsCoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideCore_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
        _lastKnownGoodPath = Path.Combine(_tempDir, "settings.json.lastknowngood");
        _tempPath = Path.Combine(_tempDir, "settings.json.tmp");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private SettingsService CreateService()
    {
        return new SettingsService(_settingsPath, _lastKnownGoodPath, _tempPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));
    }

    private async Task<SettingsModel> LoadDefaultWithRoundtrip()
    {
        // Write defaults as the initial file so we start with Loaded state.
        var json = SettingsSerializer.Serialize(SettingsModel.Defaults);
        await File.WriteAllTextAsync(_settingsPath, json);
        return SettingsModel.Defaults;
    }

    // ── Immutable snapshots ─────────────────────────────────────────────

    [Fact]
    public async Task ImmutableSnapshot_WithExpressions_CreateNewInstance()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();
        var original = service.Current;

        // Act: create a candidate via `with` expression.
        var candidate = original with
        {
            Editor = original.Editor with { CodeFontSize = 22 }
        };

        // Assert: original snapshot is unchanged.
        Assert.Equal(14, original.Editor.CodeFontSize);
        Assert.Equal(14, service.Current.Editor.CodeFontSize);

        // Commit the candidate.
        var result = await service.UpdateAsync(_ => candidate);
        Assert.IsType<SettingsMutationResult.Applied>(result);

        // The original snapshot reference still has the old value.
        Assert.Equal(14, original.Editor.CodeFontSize);
        // The service now has the new value.
        Assert.Equal(22, service.Current.Editor.CodeFontSize);
    }

    // ── Validation rejection ────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_InvalidSettings_ReturnsInvalidWithoutChangingCurrent()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();
        var snapshot = service.Current;

        // Act: try to set a negative font size.
        var invalid = snapshot with
        {
            Editor = snapshot.Editor with { CodeFontSize = -5 }
        };
        var result = await service.UpdateAsync(_ => invalid);

        // Assert
        var invalidResult = Assert.IsType<SettingsMutationResult.Invalid>(result);
        Assert.NotEmpty(invalidResult.Errors);
        Assert.Contains(invalidResult.Errors, e =>
            e.PropertyPath == "Editor.CodeFontSize" &&
            e.Message.Contains("positive"));

        // Current must remain unchanged.
        Assert.Equal(14, service.Current.Editor.CodeFontSize);
        Assert.Same(snapshot, service.Current);
    }

    [Fact]
    public async Task ApplyAsync_InvalidSettings_ReturnsInvalid()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();
        var snapshot = service.Current;

        // Act
        var invalid = snapshot with
        {
            Llm = snapshot.Llm with { BaseUrl = "" }
        };
        var result = await service.ApplyAsync(snapshot, invalid);

        // Assert
        var invalidResult = Assert.IsType<SettingsMutationResult.Invalid>(result);
        Assert.Contains(invalidResult.Errors, e =>
            e.PropertyPath == "Llm.BaseUrl");
        Assert.Same(snapshot, service.Current);
    }

    // ── Current-before-WhenChanged ordering ─────────────────────────────

    [Fact]
    public async Task Current_Before_WhenChanged_Ordering()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();

        SettingsModel? observedOnChange = null;
        SettingsModel? currentOnNotification = null;

        using var sub = service.WhenChanged.Subscribe(s =>
        {
            observedOnChange = s;
            // Read Current inside the handler — must be the new snapshot.
            currentOnNotification = service.Current;
        });

        // Act
        var next = service.Current with
        {
            Editor = service.Current.Editor with { CodeFontSize = 18 }
        };
        var result = await service.UpdateAsync(_ => next);

        // Assert
        Assert.IsType<SettingsMutationResult.Applied>(result);
        Assert.NotNull(observedOnChange);
        Assert.NotNull(currentOnNotification);

        // The handler observes the live (committed) snapshot, which is the
        // service's Current. Its Editor values are exactly the candidate's.
        Assert.Same(service.Current, observedOnChange);
        Assert.Same(service.Current, currentOnNotification);
        Assert.Equal(next.Editor, observedOnChange.Editor);
        Assert.Equal(18, observedOnChange.Editor.CodeFontSize);
        Assert.Equal(18, service.Current.Editor.CodeFontSize);
    }

    // ── Concurrent disjoint-field updates compose ───────────────────────

    [Fact]
    public async Task ConcurrentDisjointFieldUpdates_ComposeCorrectly()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();

        // Act: two concurrent producers modifying different fields.
        var t1 = Task.Run(async () =>
        {
            var r = await service.UpdateAsync(s => s with
            {
                Editor = s.Editor with { CodeFontSize = 24, TabSize = 8 }
            });
            return r;
        });

        var t2 = Task.Run(async () =>
        {
            var r = await service.UpdateAsync(s => s with
            {
                Llm = s.Llm with { BaseUrl = "https://concurrent.test/api" }
            });
            return r;
        });

        await Task.WhenAll(t1, t2);

        // Assert: both changes are reflected (order is non-deterministic, but
        // both fields must be updated).
        Assert.Equal(24, service.Current.Editor.CodeFontSize);
        Assert.Equal(8, service.Current.Editor.TabSize);
        Assert.Equal("https://concurrent.test/api", service.Current.Llm.BaseUrl);
    }

    // ── Stale ApplyAsync returns Conflict ───────────────────────────────

    [Fact]
    public async Task StaleApply_ReturnsConflict_WithoutChangingCurrent()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();

        // Get a snapshot and then mutate Current so it becomes stale.
        var staleBase = service.Current;
        await service.UpdateAsync(s => s with
        {
            Editor = s.Editor with { CodeFontSize = 30 }
        });

        // Act: try to apply using the stale base.
        var staleCandidate = staleBase with
        {
            Llm = staleBase.Llm with { BaseUrl = "https://stale.test" }
        };
        var result = await service.ApplyAsync(staleBase, staleCandidate);

        // Assert
        var conflict = Assert.IsType<SettingsMutationResult.Conflict>(result);
        Assert.Same(service.Current, conflict.Current); // Returns the live snapshot
        Assert.Equal(30, service.Current.Editor.CodeFontSize); // Unchanged
        Assert.Equal("https://api.openai.com/v1", service.Current.Llm.BaseUrl); // Unchanged
    }

    // ── Queued-write supersession ───────────────────────────────────────

    [Fact]
    public async Task QueuedWrite_SupersedesOlderWrites()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();

        // Install a writer gate: the writer loop blocks before processing
        // each item. This guarantees that all three UpdateAsync calls commit
        // and enqueue their writes before any write is consumed.
        using var writerGate = new SemaphoreSlim(0);
        service.OnBeforeWriteItem = () => writerGate.WaitAsync();

        // Act: fire all three updates concurrently. Because the writer is
        // blocked on the gate, they all commit and enqueue before the first
        // write is consumed.
        var t1 = service.UpdateAsync(s => s with { Editor = s.Editor with { CodeFontSize = 10 } });
        var t2 = service.UpdateAsync(s => s with { Editor = s.Editor with { CodeFontSize = 12 } });
        var t3 = service.UpdateAsync(s => s with { Editor = s.Editor with { CodeFontSize = 14 } });

        // Release the writer gate so items are processed.
        writerGate.Release(3);

        var results = await Task.WhenAll(t1, t2, t3);

        // Assert: with three writes queued and the final generation at 3,
        // the first two writes (gen 1, gen 2) must be Superseded because
        // 1 < 3 and 2 < 3. Only the last write (gen 3) is Saved.
        var supersededCount = results.Count(r =>
            r is SettingsMutationResult.Applied
            {
                SaveResult: SettingsSaveResult.Superseded
            });

        Assert.True(supersededCount >= 2,
            $"Expected at least 2 Superseded writes, got {supersededCount}");

        // All three must have been Applied (in-memory commit always succeeds).
        Assert.All(results, r => Assert.IsType<SettingsMutationResult.Applied>(r));

        // The final on-disk value must be 14.
        Assert.True(File.Exists(_settingsPath));
        var onDisk = File.ReadAllText(_settingsPath);
        var doc = JsonDocument.Parse(onDisk);
        Assert.Equal(14, doc.RootElement.GetProperty("editor").GetProperty("codeFontSize").GetInt32());

        // Verify the in-memory value is also 14.
        Assert.Equal(14, service.Current.Editor.CodeFontSize);

        // Clean up the test hook.
        service.OnBeforeWriteItem = null;
    }

    private static void AssertSavedOrSuperseded(SettingsSaveResult saveResult)
    {
        Assert.True(saveResult is SettingsSaveResult.Saved or SettingsSaveResult.Superseded,
            $"Unexpected save result: {saveResult.GetType().Name}");
    }

    // ── Cancellation before gate ───────────────────────────────────────

    [Fact]
    public async Task Cancellation_BeforeGate_ThrowsOperationCanceled()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled.

        // Act & Assert: UpdateAsync
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.UpdateAsync(s => s with
            {
                Editor = s.Editor with { CodeFontSize = 99 }
            }, cts.Token));

        // State unchanged.
        Assert.Equal(14, service.Current.Editor.CodeFontSize);

        // Act & Assert: ApplyAsync
        var snapshot = service.Current;
        var candidate = snapshot with
        {
            Editor = snapshot.Editor with { CodeFontSize = 99 }
        };
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ApplyAsync(snapshot, candidate, cts.Token));

        Assert.Equal(14, service.Current.Editor.CodeFontSize);

        // Act & Assert: SaveAsync
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.SaveAsync(cts.Token));

        // SaveAsync doesn't change state, but verify no file was written.
        if (File.Exists(_settingsPath))
        {
            var onDisk = File.ReadAllText(_settingsPath);
            Assert.DoesNotContain("99", onDisk);
        }
    }

    // ── Cancellation after commit is ignored ───────────────────────────

    [Fact]
    public async Task Cancellation_AfterCommit_Ignored()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();

        using var cts = new CancellationTokenSource();

        // Act: start an update and cancel after it has a chance to commit.
        // Since the mutation gate is fast, we'll use a pattern where we
        // cancel just before awaiting the result.
        var updateTask = service.UpdateAsync(s => s with
        {
            Editor = s.Editor with { CodeFontSize = 42 }
        }, cts.Token);

        // Cancel immediately – the gate may have already been acquired.
        cts.Cancel();

        try
        {
            var result = await updateTask;

            // If we got here, cancellation was after commit – we must have a
            // deterministic result, not an exception.
            var applied = Assert.IsType<SettingsMutationResult.Applied>(result);
            Assert.Equal(42, applied.Current.Editor.CodeFontSize);

            // State was committed.
            Assert.Equal(42, service.Current.Editor.CodeFontSize);
        }
        catch (OperationCanceledException)
        {
            // If cancellation happened before the gate, that's also valid per
            // the contract – state is unchanged.
            Assert.Equal(14, service.Current.Editor.CodeFontSize);
        }
    }

    // ── Cancellation after SaveAsync enqueue ───────────────────────────

    [Fact]
    public async Task SaveAsync_CancellationAfterEnqueue_Ignored()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();
        // Commit a change so there's something to save.
        await service.UpdateAsync(s => s with
        {
            Editor = s.Editor with { CodeFontSize = 16 }
        });

        using var cts = new CancellationTokenSource();

        // Act: cancel immediately after calling SaveAsync.
        var saveTask = service.SaveAsync(cts.Token);
        cts.Cancel();

        try
        {
            var result = await saveTask;
            // Must be a deterministic save result, not an exception.
            Assert.True(result is SettingsSaveResult.Saved or SettingsSaveResult.Superseded,
                $"Unexpected save result: {result.GetType().Name}");
        }
        catch (OperationCanceledException)
        {
            // If cancellation happened before enqueue, that's valid.
        }
    }

    // ── Write-error publication ────────────────────────────────────────

    [Fact]
    public async Task WriteError_PublishedOnFailure()
    {
        // Arrange: make the temp path a directory so writing to it as a file
        // throws UnauthorizedAccessException on all platforms.
        await LoadDefaultWithRoundtrip();
        Directory.CreateDirectory(_tempPath);

        using var service = CreateService();

        // Use an event to synchronize the async write-error publication.
        SettingsSaveError? capturedError = null;
        var gotError = new System.Threading.ManualResetEventSlim(false);
        using var sub = service.WriteErrors.Subscribe(e =>
        {
            capturedError = e;
            gotError.Set();
        });

        // Act: mutate – the write will fail because _tempPath is a directory.
        Assert.IsType<SettingsMutationResult.Applied>(
            await service.UpdateAsync(s => s with
            {
                Editor = s.Editor with { CodeFontSize = 50 }
            }));

        // Wait for the async error to be published.
        if (!gotError.Wait(TimeSpan.FromSeconds(5)))
        {
            // If the event never fires, check via SaveResult on the mutation.
            // The write failure still manifests in the mutation result.
            // Force a retry save to trigger the error path differently.
            var retryResult = await service.SaveAsync();
            // If we get here without timeout, the error was published.
            if (retryResult is SettingsSaveResult.Saved)
            {
                // The second save succeeded (directory races) — this is fine.
                // WriteError may or may not have fired.
            }
        }

        // Assert in-memory state is updated regardless.
        Assert.Equal(50, service.Current.Editor.CodeFontSize);

        // WriteErrors should have fired (via either UpdateAsync or SaveAsync).
        Assert.NotNull(capturedError);
        Assert.Equal(50, capturedError.FailedSnapshot.Editor.CodeFontSize);
        Assert.NotNull(capturedError.Exception);

        // Restore temp path for disposal by removing the directory.
        Directory.Delete(_tempPath);
    }

    // ── SaveAsync retry after failure ──────────────────────────────────

    [Fact]
    public async Task SaveAsync_Retry_SucceedsAfterFailure()
    {
        // Arrange: make the temp path a directory so the first write fails.
        await LoadDefaultWithRoundtrip();
        Directory.CreateDirectory(_tempPath);

        using var service = CreateService();

        // First update – write will fail (temp path is a directory).
        await service.UpdateAsync(s => s with
        {
            Editor = s.Editor with { CodeFontSize = 30 }
        });

        // Remove the temp directory so the next write succeeds.
        Directory.Delete(_tempPath);

        // Act: SaveAsync retry.
        var retryResult = await service.SaveAsync();

        // Assert
        Assert.IsType<SettingsSaveResult.Saved>(retryResult);
        Assert.True(File.Exists(_settingsPath));
        var onDisk = File.ReadAllText(_settingsPath);
        var doc = JsonDocument.Parse(onDisk);
        Assert.Equal(30, doc.RootElement.GetProperty("editor").GetProperty("codeFontSize").GetInt32());
    }

    // ── UpdateAsync does not fault on write error ──────────────────────

    [Fact]
    public async Task UpdateAsync_WriteFailure_DoesNotThrow()
    {
        // Arrange: make temp path a directory so writing fails.
        await LoadDefaultWithRoundtrip();
        Directory.CreateDirectory(_tempPath);

        using var service = CreateService();

        // Act & Assert: no exception from UpdateAsync.
        var result = await service.UpdateAsync(s => s with
        {
            Editor = s.Editor with { CodeFontSize = 77 }
        });

        var applied = Assert.IsType<SettingsMutationResult.Applied>(result);
        Assert.IsType<SettingsSaveResult.Failed>(applied.SaveResult);
        Assert.Equal(77, service.Current.Editor.CodeFontSize);

        // Cleanup for disposal.
        Directory.Delete(_tempPath);
    }

    // ── Multiple sequential SaveAsync calls ────────────────────────────

    [Fact]
    public async Task MultipleSaveAsyncCalls_AllComplete()
    {
        // Arrange
        await LoadDefaultWithRoundtrip();
        using var service = CreateService();

        // Act: call SaveAsync twice in quick succession.
        var s1 = service.SaveAsync();
        var s2 = service.SaveAsync();
        var results = await Task.WhenAll(s1, s2);

        // Assert
        Assert.Equal(2, results.Length);
        foreach (var r in results)
        {
            Assert.True(r is SettingsSaveResult.Saved or SettingsSaveResult.Superseded,
                $"Unexpected: {r.GetType().Name}");
        }
    }
}
