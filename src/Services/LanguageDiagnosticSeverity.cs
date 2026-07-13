namespace Zaide.Services;

/// <summary>
/// Structured LSP diagnostic severity. Presentation labels are projections only.
/// </summary>
public enum LanguageDiagnosticSeverity
{
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4,
}
