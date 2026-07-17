using Zaide.App.Composition;
using Zaide.Features.Language.Application;

namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// One structured diagnostic parsed from MSBuild / Roslyn CLI build output.
/// UI layers project this data; formatted strings are not the source of truth.
/// </summary>
/// <param name="FilePath">Absolute normalized on-disk path.</param>
/// <param name="Line">One-based line number.</param>
/// <param name="Column">One-based column number; <c>1</c> when the CLI omitted a column.</param>
/// <param name="Severity">Structured severity aligned with LSP diagnostics.</param>
/// <param name="Code">Optional diagnostic code (e.g. CS1002).</param>
/// <param name="Message">Diagnostic message text.</param>
/// <param name="Source">Diagnostic origin label (always <c>build</c> for parsed output).</param>
public sealed record BuildDiagnostic(
    string FilePath,
    int Line,
    int Column,
    LanguageDiagnosticSeverity Severity,
    string? Code,
    string Message,
    string Source = BuildDiagnosticSources.Build);
