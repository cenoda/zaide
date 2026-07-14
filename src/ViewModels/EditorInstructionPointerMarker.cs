namespace Zaide.ViewModels;

/// <summary>
/// Instruction-pointer marker for one source line in the active debug frame.
/// </summary>
/// <param name="Line">One-based source line.</param>
public sealed record EditorInstructionPointerMarker(int Line);