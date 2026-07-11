namespace Zaide.Models;

/// <summary>
/// A single field-level validation error produced by <see cref="SettingsValidator"/>.
/// </summary>
/// <param name="PropertyPath">Dot-separated path to the invalid property
/// (e.g. <c>"Editor.CodeFontSize"</c>).</param>
/// <param name="Message">Human-readable description of the problem.</param>
public sealed record SettingsValidationError(
    string PropertyPath,
    string Message);
