namespace Zaide.Services;

/// <summary>
/// Neutral gesture-to-command resolution record (D5).
/// Framework-agnostic; Avalonia KeyBinding materialization is done by the window layer.
/// </summary>
public sealed record ResolvedKeyBinding(string Gesture, string CommandId);
