namespace Zaide.Features.Agents.Domain;

/// <summary>
/// How the harness treats provider or broker work that completes after cancellation.
/// </summary>
internal enum NativeHarnessLateCompletionDisposition
{
    None,
    ObservedAndDiscarded,
    ObservedAndReportedIndeterminate,
}
