using Zaide.Services;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.Language.Application;

namespace Zaide.Features.ProjectSystem.Presentation;

/// <summary>
/// One Problems-list projection of a structured language or build diagnostic.
/// </summary>
public sealed class ProblemItemViewModel
{
    public ProblemItemViewModel(LanguageDiagnostic diagnostic)
    {
        Kind = ProblemKind.Language;
        Diagnostic = diagnostic;
        BuildDiagnostic = null;
        Severity = diagnostic.Severity;
        Message = diagnostic.Message;
        Code = diagnostic.Code;
        Source = diagnostic.Source ?? "csharp-ls";
        FilePath = diagnostic.FilePath;
        FileName = System.IO.Path.GetFileName(diagnostic.FilePath);
        DocumentUri = diagnostic.DocumentUri;
        Line = diagnostic.Range.StartLine + 1;
        Column = diagnostic.Range.StartCharacter + 1;
        StartOffset = diagnostic.StartOffset;
        EndOffset = diagnostic.EndOffset;
        DocumentVersion = diagnostic.DocumentVersion;
        SessionGeneration = diagnostic.SessionGeneration;
        BuildGeneration = 0;
    }

    public ProblemItemViewModel(BuildDiagnostic diagnostic, long buildGeneration)
    {
        Kind = ProblemKind.Build;
        Diagnostic = null;
        BuildDiagnostic = diagnostic;
        Severity = diagnostic.Severity;
        Message = diagnostic.Message;
        Code = diagnostic.Code;
        Source = diagnostic.Source;
        FilePath = diagnostic.FilePath;
        FileName = System.IO.Path.GetFileName(diagnostic.FilePath);
        DocumentUri = string.Empty;
        Line = diagnostic.Line;
        Column = diagnostic.Column;
        StartOffset = 0;
        EndOffset = 0;
        DocumentVersion = 0;
        SessionGeneration = 0;
        BuildGeneration = buildGeneration;
    }

    public ProblemKind Kind { get; }
    public LanguageDiagnostic? Diagnostic { get; }
    public BuildDiagnostic? BuildDiagnostic { get; }
    public LanguageDiagnosticSeverity Severity { get; }
    public string Message { get; }
    public string? Code { get; }
    public string Source { get; }
    public string FilePath { get; }
    public string FileName { get; }
    public string DocumentUri { get; }
    public int Line { get; }
    public int Column { get; }
    public int StartOffset { get; }
    public int EndOffset { get; }
    public int DocumentVersion { get; }
    public long SessionGeneration { get; }
    public long BuildGeneration { get; }

    /// <summary>Single-line summary for the list surface.</summary>
    public string DisplayText
    {
        get
        {
            var severityLabel = Severity switch
            {
                LanguageDiagnosticSeverity.Error => "Error",
                LanguageDiagnosticSeverity.Warning => "Warning",
                LanguageDiagnosticSeverity.Information => "Info",
                LanguageDiagnosticSeverity.Hint => "Hint",
                _ => "Issue",
            };

            var codePart = string.IsNullOrEmpty(Code) ? string.Empty : $" {Code}";
            return $"{severityLabel}{codePart}: {Message}  —  {FileName}:{Line}:{Column}  [{Source}]";
        }
    }
}
