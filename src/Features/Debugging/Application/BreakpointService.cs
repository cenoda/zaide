using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.Debugging.Contracts;

namespace Zaide.Features.Debugging.Application;

/// <summary>
/// Workspace-scoped breakpoint persistence backed by <see cref="ISettingsService"/>.
/// </summary>
internal sealed class BreakpointService : IBreakpointService
{
    private readonly IProjectContextService _projectContext;
    private readonly ISettingsService _settings;

    public BreakpointService(
        IProjectContextService projectContext,
        ISettingsService settings)
    {
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public IReadOnlyList<PersistedBreakpoint> GetBreakpoints()
    {
        var workspaceRoot = TryGetCurrentWorkspaceRoot();
        if (workspaceRoot is null)
            return Array.Empty<PersistedBreakpoint>();

        if (!_settings.Current.Debug.BreakpointsByWorkspaceRoot.TryGetValue(
                workspaceRoot,
                out var breakpoints))
        {
            return Array.Empty<PersistedBreakpoint>();
        }

        return breakpoints.ToArray();
    }

    /// <inheritdoc />
    public async Task<BreakpointOperationResult> AddAsync(
        string sourcePath,
        int line,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (line < 1)
        {
            return new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.InvalidLine,
                "Breakpoint line must be at least 1.");
        }

        var workspaceRoot = TryGetCurrentWorkspaceRoot();
        if (workspaceRoot is null)
        {
            return new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.NoWorkspace,
                "Breakpoints can only be persisted when a workspace is open.");
        }

        var normalizedSource = Path.GetFullPath(sourcePath);
        var mutation = await _settings.UpdateAsync(
            model => UpsertBreakpoint(model, workspaceRoot, normalizedSource, line, enabled: true),
            cancellationToken).ConfigureAwait(false);

        return mutation switch
        {
            SettingsMutationResult.Applied => new BreakpointOperationResult(true, null, null),
            SettingsMutationResult.Invalid => new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.InvalidLine,
                "Breakpoint settings were rejected."),
            SettingsMutationResult.Conflict => new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.NoWorkspace,
                "Breakpoint update conflicted with a concurrent settings change."),
            _ => new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.NoWorkspace,
                "Breakpoint update failed."),
        };
    }

    /// <inheritdoc />
    public async Task<BreakpointOperationResult> RemoveAsync(
        string sourcePath,
        int line,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (line < 1)
        {
            return new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.InvalidLine,
                "Breakpoint line must be at least 1.");
        }

        var workspaceRoot = TryGetCurrentWorkspaceRoot();
        if (workspaceRoot is null)
        {
            return new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.NoWorkspace,
                "Breakpoints can only be persisted when a workspace is open.");
        }

        var normalizedSource = Path.GetFullPath(sourcePath);
        var current = GetWorkspaceBreakpoints(_settings.Current, workspaceRoot);
        if (!current.Any(bp =>
                bp.Line == line &&
                string.Equals(bp.SourcePath, normalizedSource, StringComparison.Ordinal)))
        {
            return new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.NotFound,
                "No breakpoint exists at the requested location.");
        }

        var mutation = await _settings.UpdateAsync(
            model => RemoveBreakpoint(model, workspaceRoot, normalizedSource, line),
            cancellationToken).ConfigureAwait(false);

        return mutation switch
        {
            SettingsMutationResult.Applied => new BreakpointOperationResult(true, null, null),
            SettingsMutationResult.Invalid => new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.InvalidLine,
                "Breakpoint settings were rejected."),
            SettingsMutationResult.Conflict => new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.NoWorkspace,
                "Breakpoint update conflicted with a concurrent settings change."),
            _ => new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.NoWorkspace,
                "Breakpoint update failed."),
        };
    }

    /// <inheritdoc />
    public async Task<BreakpointOperationResult> ToggleAsync(
        string sourcePath,
        int line,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (line < 1)
        {
            return new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.InvalidLine,
                "Breakpoint line must be at least 1.");
        }

        var workspaceRoot = TryGetCurrentWorkspaceRoot();
        if (workspaceRoot is null)
        {
            return new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.NoWorkspace,
                "Breakpoints can only be persisted when a workspace is open.");
        }

        var normalizedSource = Path.GetFullPath(sourcePath);
        var current = GetWorkspaceBreakpoints(_settings.Current, workspaceRoot);
        var existing = current.FirstOrDefault(bp =>
            bp.Line == line &&
            string.Equals(bp.SourcePath, normalizedSource, StringComparison.Ordinal));

        var nextEnabled = existing is null || !existing.Enabled;
        var mutation = await _settings.UpdateAsync(
            model => UpsertBreakpoint(model, workspaceRoot, normalizedSource, line, nextEnabled),
            cancellationToken).ConfigureAwait(false);

        return mutation switch
        {
            SettingsMutationResult.Applied => new BreakpointOperationResult(true, null, null),
            SettingsMutationResult.Invalid => new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.InvalidLine,
                "Breakpoint settings were rejected."),
            SettingsMutationResult.Conflict => new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.NoWorkspace,
                "Breakpoint update conflicted with a concurrent settings change."),
            _ => new BreakpointOperationResult(
                false,
                BreakpointOutcomeKind.NoWorkspace,
                "Breakpoint update failed."),
        };
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<int>> MapToDapReplacementBySource(
        IReadOnlyCollection<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var enabledBySource = GetBreakpoints()
            .Where(bp => bp.Enabled)
            .GroupBy(bp => bp.SourcePath, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group
                    .Select(bp => bp.Line)
                    .OrderBy(line => line)
                    .ToArray(),
                StringComparer.Ordinal);

        var result = new Dictionary<string, IReadOnlyList<int>>(
            sourcePaths.Count,
            StringComparer.Ordinal);

        foreach (var sourcePath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                continue;

            var normalized = Path.GetFullPath(sourcePath);
            result[normalized] = enabledBySource.TryGetValue(normalized, out var lines)
                ? lines
                : Array.Empty<int>();
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<int>>(result);
    }

    private string? TryGetCurrentWorkspaceRoot()
    {
        var root = _projectContext.Current.WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(root))
            return null;

        return Path.GetFullPath(root);
    }

    private static IReadOnlyList<PersistedBreakpoint> GetWorkspaceBreakpoints(
        SettingsModel model,
        string workspaceRoot)
    {
        if (!model.Debug.BreakpointsByWorkspaceRoot.TryGetValue(workspaceRoot, out var breakpoints))
            return Array.Empty<PersistedBreakpoint>();

        return breakpoints;
    }

    private static SettingsModel UpsertBreakpoint(
        SettingsModel model,
        string workspaceRoot,
        string sourcePath,
        int line,
        bool enabled)
    {
        var map = CopyBreakpointMap(model.Debug.BreakpointsByWorkspaceRoot);
        var current = map.TryGetValue(workspaceRoot, out var existing)
            ? existing.ToList()
            : new List<PersistedBreakpoint>();

        current.RemoveAll(bp =>
            bp.Line == line &&
            string.Equals(bp.SourcePath, sourcePath, StringComparison.Ordinal));
        current.Add(new PersistedBreakpoint(sourcePath, line, enabled));
        current.Sort(CompareBreakpoints);

        map[workspaceRoot] = current.ToArray();
        return model with { Debug = new DebugSettings(map) };
    }

    private static SettingsModel RemoveBreakpoint(
        SettingsModel model,
        string workspaceRoot,
        string sourcePath,
        int line)
    {
        var map = CopyBreakpointMap(model.Debug.BreakpointsByWorkspaceRoot);
        if (!map.TryGetValue(workspaceRoot, out var existing))
            return model;

        var current = existing
            .Where(bp =>
                bp.Line != line ||
                !string.Equals(bp.SourcePath, sourcePath, StringComparison.Ordinal))
            .ToArray();

        if (current.Length == 0)
            map.Remove(workspaceRoot);
        else
            map[workspaceRoot] = current;

        return model with { Debug = new DebugSettings(map) };
    }

    private static Dictionary<string, IReadOnlyList<PersistedBreakpoint>> CopyBreakpointMap(
        IReadOnlyDictionary<string, IReadOnlyList<PersistedBreakpoint>> source)
    {
        var copy = new Dictionary<string, IReadOnlyList<PersistedBreakpoint>>(
            source.Count,
            StringComparer.Ordinal);

        foreach (var (workspaceRoot, breakpoints) in source)
            copy[workspaceRoot] = breakpoints.ToArray();

        return copy;
    }

    private static int CompareBreakpoints(PersistedBreakpoint left, PersistedBreakpoint right)
    {
        var pathCompare = string.Compare(
            left.SourcePath,
            right.SourcePath,
            StringComparison.Ordinal);
        return pathCompare != 0 ? pathCompare : left.Line.CompareTo(right.Line);
    }
}