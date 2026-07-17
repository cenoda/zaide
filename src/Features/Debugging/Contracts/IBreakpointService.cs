using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Models;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Debugging.Application;

namespace Zaide.Features.Debugging.Contracts;

/// <summary>
/// Owns workspace-scoped persistent source breakpoints stored in app-global
/// settings. Adapter verification state is session-only and is not persisted.
/// </summary>
public interface IBreakpointService
{
    /// <summary>
    /// Returns an immutable snapshot of breakpoints for the current workspace.
    /// Empty when no workspace root is loaded.
    /// </summary>
    IReadOnlyList<PersistedBreakpoint> GetBreakpoints();

    /// <summary>
    /// Adds or re-enables a breakpoint at the normalized source path and line.
    /// Does not persist when no workspace root is loaded.
    /// </summary>
    Task<BreakpointOperationResult> AddAsync(
        string sourcePath,
        int line,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a breakpoint at the normalized source path and line.
    /// Does not persist when no workspace root is loaded.
    /// </summary>
    Task<BreakpointOperationResult> RemoveAsync(
        string sourcePath,
        int line,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles the enabled state of an existing breakpoint, or adds an enabled
    /// breakpoint when none exists at the location.
    /// Does not persist when no workspace root is loaded.
    /// </summary>
    Task<BreakpointOperationResult> ToggleAsync(
        string sourcePath,
        int line,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps persisted enabled breakpoints into complete per-source replacement
    /// line sets for later DAP <c>setBreakpoints</c> calls. Every requested
    /// <paramref name="sourcePaths"/> entry is present, including empty sets.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<int>> MapToDapReplacementBySource(
        IReadOnlyCollection<string> sourcePaths);
}