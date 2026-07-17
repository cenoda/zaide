using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Infrastructure;

/// <summary>
/// Production resolver that queries MSBuild <c>TargetPath</c> through the managed
/// redirected process runner. Never scans or guesses under <c>bin/</c>.
/// </summary>
public sealed class ProjectDebugTargetResolver : IProjectDebugTargetResolver
{
    private readonly IManagedProcessRunner _runner;

    public ProjectDebugTargetResolver(IManagedProcessRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    /// <inheritdoc />
    public async Task<ProjectDebugTargetResolution> ResolveTargetPathAsync(
        string absoluteCsprojPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteCsprojPath);
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedCsproj;
        try
        {
            normalizedCsproj = Path.GetFullPath(absoluteCsprojPath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return ProjectDebugTargetResolution.Unsupported(
                $"Project file path is invalid: {ex.Message}");
        }

        if (!string.Equals(Path.GetExtension(normalizedCsproj), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectDebugTargetResolution.Unsupported(
                "Debug target resolution requires a .csproj file.");
        }

        if (!File.Exists(normalizedCsproj))
        {
            return ProjectDebugTargetResolution.Unsupported(
                "Project file does not exist.");
        }

        var workingDirectory = Path.GetDirectoryName(normalizedCsproj)
            ?? normalizedCsproj;

        var request = new ManagedProcessStartRequest(
            "dotnet",
            $"msbuild \"{normalizedCsproj}\" -getProperty:TargetPath -nologo",
            workingDirectory,
            Generation: 0);

        var stdout = new List<string>();

        void OnOutput(ManagedProcessOutputLine line)
        {
            if (line.Stream == ProcessStreamKind.StdOut)
                stdout.Add(line.Text);
        }

        _runner.OutputReceived += OnOutput;
        ManagedProcessRunResult runResult;
        try
        {
            runResult = await _runner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ProjectDebugTargetResolution.Unsupported(
                $"TargetPath query failed: {ex.Message}");
        }
        finally
        {
            _runner.OutputReceived -= OnOutput;
        }

        if (runResult.StartupFailed)
        {
            return ProjectDebugTargetResolution.Unsupported(
                "TargetPath query could not start.");
        }

        if (runResult.WasCancelled)
        {
            return ProjectDebugTargetResolution.Unsupported(
                "TargetPath query was cancelled.");
        }

        if (runResult.ExitCode != 0)
        {
            return ProjectDebugTargetResolution.Unsupported(
                $"TargetPath query failed with exit code {runResult.ExitCode}.");
        }

        return ParseTargetPathOutput(string.Join(Environment.NewLine, stdout));
    }

    internal static ProjectDebugTargetResolution ParseTargetPathOutput(string? stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return ProjectDebugTargetResolution.Unsupported(
                "TargetPath query returned no output.");
        }

        var lines = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0)
            .ToArray();

        if (lines.Length == 0)
        {
            return ProjectDebugTargetResolution.Unsupported(
                "TargetPath query returned no output.");
        }

        if (lines.Length > 1)
        {
            var distinct = new HashSet<string>(lines, StringComparer.Ordinal);
            if (distinct.Count > 1)
            {
                return ProjectDebugTargetResolution.Unsupported(
                    "TargetPath query returned multiple values.");
            }
        }

        var candidate = lines[0];
        if (!Path.IsPathRooted(candidate))
        {
            return ProjectDebugTargetResolution.Unsupported(
                "TargetPath query returned a relative path.");
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(candidate);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return ProjectDebugTargetResolution.Unsupported(
                $"TargetPath query returned a malformed path: {ex.Message}");
        }

        if (!string.Equals(Path.GetExtension(normalized), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectDebugTargetResolution.Unsupported(
                "TargetPath query did not resolve to a .dll assembly.");
        }

        if (!File.Exists(normalized))
        {
            return ProjectDebugTargetResolution.Unsupported(
                "TargetPath query resolved to a missing file.");
        }

        return ProjectDebugTargetResolution.Success(normalized);
    }
}