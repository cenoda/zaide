namespace Zaide.Features.Agents.Domain;

/// <summary>
/// High-level phase of one Native Harness turn loop iteration.
/// </summary>
internal enum NativeHarnessTurnPhase
{
    AwaitingModel,
    ExecutingTools,
    Terminal,
}
