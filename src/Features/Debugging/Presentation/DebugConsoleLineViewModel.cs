namespace Zaide.Features.Debugging.Presentation;

/// <summary>
/// One structured line in the Debug Console surface.
/// </summary>
/// <param name="DisplayText">Rendered line text.</param>
/// <param name="Kind">Line classification for styling.</param>
public sealed record DebugConsoleLineViewModel(string DisplayText, DebugConsoleLineKind Kind);

/// <summary>
/// Debug Console line classification.
/// </summary>
public enum DebugConsoleLineKind
{
    Info,
    Output,
    Error,
}