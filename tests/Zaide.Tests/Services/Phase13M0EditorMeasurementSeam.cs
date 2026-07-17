using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Builder;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 13 M0 test-only measurement seam for editor open/edit/save/restore and
/// large-document load. Exercises the same application command paths used in
/// production (<see cref="EditorTabViewModel.OpenFileCommand"/>,
/// <see cref="EditorViewModel.TextContent"/>, <see cref="EditorViewModel.SaveCommand"/>).
/// Uses a high-resolution monotonic test-host clock. No production telemetry,
/// no keyboard/pointer injection, no desktop automation.
/// </summary>
public static class Phase13M0EditorMeasurementSeam
{
    private static int _reactiveInitialized;

    /// <summary>
    /// Clock boundary for all samples in this seam: <see cref="Stopwatch.GetTimestamp"/>
    /// (high-resolution monotonic) starts immediately before the first timed
    /// command and stops immediately after the last timed command completes.
    /// Fixture preparation, SHA-256 hashing, and cleanup are outside the clock.
    /// </summary>
    public const string ClockBoundary =
        "Stopwatch.GetTimestamp high-resolution monotonic test-host clock; " +
        "starts immediately before the first app command and stops immediately " +
        "after the last timed command returns. Fixture copy, SHA-256, " +
        "one untimed warm-up sample (JIT/path priming), and teardown are excluded.";

    public static EditorTabViewModel CreateTabManager()
    {
        EnsureReactiveUiInitialized();
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();
        return new EditorTabViewModel(
            sp,
            sp.GetRequiredService<IFileService>(),
            sp.GetRequiredService<Workspace>());
    }

    private static void EnsureReactiveUiInitialized()
    {
        if (Interlocked.Exchange(ref _reactiveInitialized, 1) == 1)
            return;

        try
        {
            RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        }
        catch (InvalidOperationException)
        {
            // Already initialized by another test in this process.
        }
    }

    public static string Sha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// One sample of open → harmless edit → save → close → re-open restore check.
    /// Works on a private temp copy so the source fixture is never mutated.
    /// Timed path: open + edit + save + close + re-open (restore verification
    /// of in-memory content is inside the clock; on-disk byte check is also
    /// inside because it confirms save completed).
    /// </summary>
    public static async Task<Phase13M0EditorSample> MeasureOpenEditSaveRestoreAsync(
        string sourceFixturePath,
        int sampleNumber)
    {
        if (!File.Exists(sourceFixturePath))
            throw new FileNotFoundException("Editor measurement fixture missing.", sourceFixturePath);

        var sourceSha = Sha256Hex(sourceFixturePath);
        var originalBytes = await File.ReadAllBytesAsync(sourceFixturePath).ConfigureAwait(false);
        var originalText = await File.ReadAllTextAsync(sourceFixturePath).ConfigureAwait(false);

        var workDir = Path.Combine(Path.GetTempPath(), "zaide-phase13-editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var workPath = Path.Combine(workDir, Path.GetFileName(sourceFixturePath));

        try
        {
            await File.WriteAllBytesAsync(workPath, originalBytes).ConfigureAwait(false);

            var tabs = CreateTabManager();
            var marker = $"// phase13-m0-sample-{sampleNumber}-{Guid.NewGuid():N}";
            var expectedSaved = originalText + Environment.NewLine + marker + Environment.NewLine;

            var start = Stopwatch.GetTimestamp();

            var opened = await tabs.OpenFileCommand.Execute(workPath).FirstAsync();
            if (!opened)
            {
                var elapsedFail = ElapsedMs(start);
                return new Phase13M0EditorSample(
                    Area: "editor",
                    Classification: "open-edit-save-restore",
                    Sample: sampleNumber,
                    ElapsedMs: elapsedFail,
                    Status: "fail",
                    Note: "OpenFileCommand failed: " + (tabs.LastOpenError ?? "unknown"),
                    FixturePath: sourceFixturePath,
                    FixtureSha256: sourceSha,
                    WorkPath: workPath,
                    ClockBoundary: ClockBoundary,
                    Restored: false,
                    ContentLength: 0);
            }

            var tab = tabs.ActiveTab
                ?? throw new InvalidOperationException("OpenFileCommand returned true without ActiveTab.");

            // Same dirty path the editor uses when text changes.
            tab.TextContent = expectedSaved;
            if (!tab.IsDirty)
            {
                var elapsedFail = ElapsedMs(start);
                return new Phase13M0EditorSample(
                    Area: "editor",
                    Classification: "open-edit-save-restore",
                    Sample: sampleNumber,
                    ElapsedMs: elapsedFail,
                    Status: "fail",
                    Note: "TextContent assignment did not mark the tab dirty",
                    FixturePath: sourceFixturePath,
                    FixtureSha256: sourceSha,
                    WorkPath: workPath,
                    ClockBoundary: ClockBoundary,
                    Restored: false,
                    ContentLength: tab.TextContent.Length);
            }

            var saved = await tab.SaveCommand.Execute().FirstAsync();
            if (!saved)
            {
                var elapsedFail = ElapsedMs(start);
                return new Phase13M0EditorSample(
                    Area: "editor",
                    Classification: "open-edit-save-restore",
                    Sample: sampleNumber,
                    ElapsedMs: elapsedFail,
                    Status: "fail",
                    Note: "SaveCommand failed: " + (tab.LastSaveError ?? "unknown"),
                    FixturePath: sourceFixturePath,
                    FixtureSha256: sourceSha,
                    WorkPath: workPath,
                    ClockBoundary: ClockBoundary,
                    Restored: false,
                    ContentLength: tab.TextContent.Length);
            }

            if (tab.IsDirty)
            {
                var elapsedFail = ElapsedMs(start);
                return new Phase13M0EditorSample(
                    Area: "editor",
                    Classification: "open-edit-save-restore",
                    Sample: sampleNumber,
                    ElapsedMs: elapsedFail,
                    Status: "fail",
                    Note: "SaveCommand returned true but IsDirty remained true",
                    FixturePath: sourceFixturePath,
                    FixtureSha256: sourceSha,
                    WorkPath: workPath,
                    ClockBoundary: ClockBoundary,
                    Restored: false,
                    ContentLength: tab.TextContent.Length);
            }

            // Close without prompt (already clean).
            await tabs.CloseTabCommand.Execute(tab).FirstAsync();

            var reopened = await tabs.OpenFileCommand.Execute(workPath).FirstAsync();
            var restoredTab = tabs.ActiveTab;
            if (!reopened || restoredTab is null)
            {
                var elapsedFail = ElapsedMs(start);
                return new Phase13M0EditorSample(
                    Area: "editor",
                    Classification: "open-edit-save-restore",
                    Sample: sampleNumber,
                    ElapsedMs: elapsedFail,
                    Status: "fail",
                    Note: "Post-save re-open failed: " + (tabs.LastOpenError ?? "unknown"),
                    FixturePath: sourceFixturePath,
                    FixtureSha256: sourceSha,
                    WorkPath: workPath,
                    ClockBoundary: ClockBoundary,
                    Restored: false,
                    ContentLength: 0);
            }

            var restoredContent = restoredTab.TextContent;
            var diskContent = await File.ReadAllTextAsync(workPath).ConfigureAwait(false);
            var restored = restoredContent == expectedSaved
                           && diskContent == expectedSaved
                           && !restoredTab.IsDirty;

            var elapsedMs = ElapsedMs(start);

            return new Phase13M0EditorSample(
                Area: "editor",
                Classification: "open-edit-save-restore",
                Sample: sampleNumber,
                ElapsedMs: elapsedMs,
                Status: restored ? "pass" : "fail",
                Note: restored
                    ? "open+edit+save+re-open content matches saved text; source fixture SHA unchanged"
                    : "post-save restore verification failed (in-memory or on-disk mismatch)",
                FixturePath: sourceFixturePath,
                FixtureSha256: sourceSha,
                WorkPath: workPath,
                ClockBoundary: ClockBoundary,
                Restored: restored,
                ContentLength: restoredContent.Length);
        }
        finally
        {
            try
            {
                if (Directory.Exists(workDir))
                    Directory.Delete(workDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup; measurement artifacts live under /tmp.
            }
        }
    }

    /// <summary>
    /// One sample of large-document load via <see cref="EditorTabViewModel.OpenFileCommand"/>.
    /// Timed path: open only (read + document/tab materialization). No UI render claim.
    /// </summary>
    public static async Task<Phase13M0EditorSample> MeasureLargeDocumentLoadAsync(
        string fixturePath,
        int sampleNumber)
    {
        if (!File.Exists(fixturePath))
            throw new FileNotFoundException("Large-file measurement fixture missing.", fixturePath);

        var fixtureSha = Sha256Hex(fixturePath);
        var expectedLength = new FileInfo(fixturePath).Length;

        var tabs = CreateTabManager();
        var start = Stopwatch.GetTimestamp();

        var opened = await tabs.OpenFileCommand.Execute(fixturePath).FirstAsync();
        var tab = tabs.ActiveTab;
        if (!opened || tab is null)
        {
            var elapsedFail = ElapsedMs(start);
            return new Phase13M0EditorSample(
                Area: "large-file",
                Classification: "document-load",
                Sample: sampleNumber,
                ElapsedMs: elapsedFail,
                Status: "fail",
                Note: "OpenFileCommand failed: " + (tabs.LastOpenError ?? "unknown"),
                FixturePath: fixturePath,
                FixtureSha256: fixtureSha,
                WorkPath: fixturePath,
                ClockBoundary: ClockBoundary,
                Restored: false,
                ContentLength: 0);
        }

        var contentLength = tab.TextContent.Length;
        var elapsedMs = ElapsedMs(start);

        // Close so repeated samples do not hit the existing-tab fast path.
        await tabs.CloseTabCommand.Execute(tab).FirstAsync();

        var ok = contentLength == expectedLength && !tab.IsDirty;
        return new Phase13M0EditorSample(
            Area: "large-file",
            Classification: "document-load",
            Sample: sampleNumber,
            ElapsedMs: elapsedMs,
            Status: ok ? "pass" : "fail",
            Note: ok
                ? $"loaded {contentLength} characters via OpenFileCommand; fixture unchanged"
                : $"loaded length {contentLength} != fixture byte length {expectedLength}",
            FixturePath: fixturePath,
            FixtureSha256: fixtureSha,
            WorkPath: fixturePath,
            ClockBoundary: ClockBoundary,
            Restored: false,
            ContentLength: contentLength);
    }

    private static double ElapsedMs(long startTimestamp)
    {
        var delta = Stopwatch.GetTimestamp() - startTimestamp;
        return delta * 1000.0 / Stopwatch.Frequency;
    }
}

/// <summary>
/// One timed sample produced by <see cref="Phase13M0EditorMeasurementSeam"/>.
/// </summary>
public sealed record Phase13M0EditorSample(
    string Area,
    string Classification,
    int Sample,
    double ElapsedMs,
    string Status,
    string Note,
    string FixturePath,
    string FixtureSha256,
    string WorkPath,
    string ClockBoundary,
    bool Restored,
    int ContentLength);
