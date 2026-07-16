using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Zaide.Tests.Services;

/// <summary>
/// Focused correctness tests for the Phase 13 M0 editor measurement seam.
/// Full five-sample evidence is produced by the local runner extension
/// (<c>tools/phase13-measure.py --areas editor large-file</c>), not by CI timing.
/// </summary>
public class Phase13M0EditorMeasurementTests
{
    private static string WorkflowConsoleProgramPath =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "fixtures", "workflow-console", "Program.cs"));

    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public async Task OpenEditSaveRestore_Completes_AndRestoresSavedContent()
    {
        var fixture = WorkflowConsoleProgramPath;
        Assert.True(File.Exists(fixture), $"Missing fixture: {fixture}");

        var sourceShaBefore = Phase13M0EditorMeasurementSeam.Sha256Hex(fixture);
        var sample = await Phase13M0EditorMeasurementSeam
            .MeasureOpenEditSaveRestoreAsync(fixture, sampleNumber: 1);

        Assert.Equal("pass", sample.Status);
        Assert.True(sample.Restored, sample.Note);
        Assert.Equal(sourceShaBefore, sample.FixtureSha256);
        Assert.Equal(sourceShaBefore, Phase13M0EditorMeasurementSeam.Sha256Hex(fixture));
        Assert.True(sample.ElapsedMs > 0);
        Assert.Contains("Stopwatch.GetTimestamp", sample.ClockBoundary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LargeDocumentLoad_OpensGeneratedEightMibFixture()
    {
        var largePath = Path.Combine("/tmp", "zaide-phase13", "large-file-8MiB.txt");
        EnsureLargeFileFixture(largePath);

        var expectedSha = Phase13M0EditorMeasurementSeam.Sha256Hex(largePath);
        var sample = await Phase13M0EditorMeasurementSeam
            .MeasureLargeDocumentLoadAsync(largePath, sampleNumber: 1);

        Assert.Equal("pass", sample.Status);
        Assert.Equal(expectedSha, sample.FixtureSha256);
        Assert.Equal(8 * 1024 * 1024, sample.ContentLength);
        Assert.True(sample.ElapsedMs > 0);
        Assert.Equal(expectedSha, Phase13M0EditorMeasurementSeam.Sha256Hex(largePath));
    }

    [Fact]
    public void CreateTabManager_UsesProductionFileServiceAndWorkspace()
    {
        var tabs = Phase13M0EditorMeasurementSeam.CreateTabManager();
        Assert.NotNull(tabs);
        Assert.Empty(tabs.OpenTabs);
        Assert.Null(tabs.ActiveTab);
    }

    /// <summary>
    /// Opt-in multi-sample evidence writer used by <c>tools/phase13-measure.py</c>.
    /// Set <c>ZAIDE_PHASE13_EDITOR_MEASURE_OUTPUT</c> to a directory path and
    /// <c>ZAIDE_PHASE13_EDITOR_MEASURE_AREAS</c> to a comma list of
    /// <c>editor</c> and/or <c>large-file</c>. Sample count defaults to 5
    /// (<c>ZAIDE_PHASE13_EDITOR_MEASURE_SAMPLES</c>).
    /// When the output env var is unset this test is a no-op success so the
    /// normal suite never times or writes machine-local artifacts.
    /// </summary>
    [Fact]
    public async Task MeasurementRunner_WritesEvidence_WhenOutputEnvSet()
    {
        var output = Environment.GetEnvironmentVariable("ZAIDE_PHASE13_EDITOR_MEASURE_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
            return;

        var areasEnv = Environment.GetEnvironmentVariable("ZAIDE_PHASE13_EDITOR_MEASURE_AREAS")
                       ?? "editor,large-file";
        var samplesEnv = Environment.GetEnvironmentVariable("ZAIDE_PHASE13_EDITOR_MEASURE_SAMPLES");
        var sampleCount = int.TryParse(samplesEnv, out var parsed) && parsed > 0 ? parsed : 5;

        var areas = areasEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Directory.CreateDirectory(output);

        var editorFixture = WorkflowConsoleProgramPath;
        var largeFixture = Path.Combine("/tmp", "zaide-phase13", "large-file-8MiB.txt");

        foreach (var area in areas)
        {
            if (area is "editor")
            {
                Assert.True(File.Exists(editorFixture), $"Missing editor fixture: {editorFixture}");
                // Untimed warm-ups (JIT + path priming). Outside the sample clock;
                // not counted as retained samples and not outlier discards.
                for (var warm = 0; warm < 3; warm++)
                {
                    var warmUp = await Phase13M0EditorMeasurementSeam
                        .MeasureOpenEditSaveRestoreAsync(editorFixture, sampleNumber: 0);
                    Assert.Equal("pass", warmUp.Status);
                }

                var samples = new Phase13M0EditorSample[sampleCount];
                for (var i = 1; i <= sampleCount; i++)
                {
                    samples[i - 1] = await Phase13M0EditorMeasurementSeam
                        .MeasureOpenEditSaveRestoreAsync(editorFixture, i);
                }

                WriteAreaEvidence(output, "editor", samples);
            }
            else if (area is "large-file")
            {
                EnsureLargeFileFixture(largeFixture);
                for (var warm = 0; warm < 3; warm++)
                {
                    var warmUp = await Phase13M0EditorMeasurementSeam
                        .MeasureLargeDocumentLoadAsync(largeFixture, sampleNumber: 0);
                    Assert.Equal("pass", warmUp.Status);
                }

                var samples = new Phase13M0EditorSample[sampleCount];
                for (var i = 1; i <= sampleCount; i++)
                {
                    samples[i - 1] = await Phase13M0EditorMeasurementSeam
                        .MeasureLargeDocumentLoadAsync(largeFixture, i);
                }

                WriteAreaEvidence(output, "large-file", samples);
            }
            else
            {
                throw new InvalidOperationException($"Unknown measurement area: {area}");
            }
        }
    }

    private static void EnsureLargeFileFixture(string largePath)
    {
        var expectedSha = "0a014ac760b7eb31cd7b75b2aa1a897b7fe430571a5ac874a3c8706c54c9ffd9";
        if (File.Exists(largePath)
            && Phase13M0EditorMeasurementSeam.Sha256Hex(largePath) == expectedSha)
        {
            return;
        }

        var generator = Path.Combine(RepoRoot, "tools", "phase13-generate-large-file.py");
        Assert.True(File.Exists(generator), $"Missing generator: {generator}");
        Directory.CreateDirectory(Path.GetDirectoryName(largePath)!);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "python3",
            ArgumentList = { generator, largePath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = System.Diagnostics.Process.Start(startInfo)
                            ?? throw new InvalidOperationException("Failed to start large-file generator.");
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(expectedSha, Phase13M0EditorMeasurementSeam.Sha256Hex(largePath));
    }

    private static void WriteAreaEvidence(string outputRoot, string area, Phase13M0EditorSample[] samples)
    {
        var areaDir = Path.Combine(outputRoot, area);
        Directory.CreateDirectory(areaDir);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var path = Path.Combine(areaDir, "raw-samples.json");
        File.WriteAllText(path, JsonSerializer.Serialize(samples, options) + Environment.NewLine);

        // Also write a simple TSV for operator inspection.
        var tsv = Path.Combine(areaDir, "raw-samples.tsv");
        using var writer = new StreamWriter(tsv);
        writer.WriteLine("area\tclassification\tsample\telapsed_ms\tstatus\trestored\tcontent_length\tfixture_path\tfixture_sha256\tnote");
        foreach (var sample in samples)
        {
            writer.WriteLine(string.Join('\t',
                sample.Area,
                sample.Classification,
                sample.Sample,
                sample.ElapsedMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                sample.Status,
                sample.Restored,
                sample.ContentLength,
                sample.FixturePath,
                sample.FixtureSha256,
                sample.Note.Replace('\t', ' ')));
        }
    }
}
