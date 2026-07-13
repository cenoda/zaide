using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>
/// Immutable snapshot of parsed build diagnostics for one build generation.
/// </summary>
/// <param name="BuildGeneration">
/// Workflow operation generation that produced these diagnostics.
/// </param>
/// <param name="LastOutcome">
/// Terminal workflow outcome for the build, or <c>null</c> while a build is active.
/// </param>
/// <param name="IsPartial">
/// <c>true</c> when the build was cancelled but partial output was parsed (U3).
/// </param>
/// <param name="Diagnostics">Parsed build diagnostics. Never null.</param>
public sealed record BuildDiagnosticsSnapshot(
    long BuildGeneration,
    ProjectWorkflowOutcomeKind? LastOutcome,
    bool IsPartial,
    IReadOnlyList<BuildDiagnostic> Diagnostics)
{
    public static BuildDiagnosticsSnapshot Empty { get; } = new(
        BuildGeneration: 0,
        LastOutcome: null,
        IsPartial: false,
        Diagnostics: System.Array.Empty<BuildDiagnostic>());
}
