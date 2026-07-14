namespace Zaide.Services;

/// <summary>
/// One source breakpoint submitted during launch configuration.
/// </summary>
/// <param name="SourcePath">Absolute normalized on-disk source path.</param>
/// <param name="Line">One-based source line.</param>
public sealed record DebugBreakpointRequest(string SourcePath, int Line);
