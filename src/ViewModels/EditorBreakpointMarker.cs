namespace Zaide.ViewModels;

/// <summary>
/// One persisted breakpoint marker projected into the active editor margin.
/// </summary>
/// <param name="Line">One-based source line.</param>
/// <param name="Enabled">Whether the breakpoint is enabled.</param>
public sealed record EditorBreakpointMarker(int Line, bool Enabled);