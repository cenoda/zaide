using System.Collections.Generic;

namespace Zaide.Features.Debugging.Application;

/// <summary>
/// Explicit launch parameters for a DAP <c>launch</c> request in M1.
/// Build-to-debug handoff is added in a later milestone.
/// </summary>
/// <param name="ProgramPath">Absolute path to the executable assembly.</param>
/// <param name="WorkingDirectory">Absolute project working directory for <c>cwd</c>.</param>
/// <param name="StopAtEntry">Whether the adapter should stop at program entry.</param>
/// <param name="Breakpoints">Initial source breakpoints to submit before <c>configurationDone</c>.</param>
public sealed record DebugLaunchRequest(
    string ProgramPath,
    string WorkingDirectory,
    bool StopAtEntry,
    IReadOnlyList<DebugBreakpointRequest> Breakpoints);
