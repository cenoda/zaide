using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// One Problems-list projection of a structured <see cref="LanguageDiagnostic"/>.
/// </summary>
public sealed class ProblemItemViewModel
{
    public ProblemItemViewModel(LanguageDiagnostic diagnostic)
    {
        Diagnostic = diagnostic;
        Severity = diagnostic.Severity;
        Message = diagnostic.Message;
        Code = diagnostic.Code;
        FilePath = diagnostic.FilePath;
        FileName = System.IO.Path.GetFileName(diagnostic.FilePath);
        DocumentUri = diagnostic.DocumentUri;
        // Display as 1-based line/column for the user-facing list.
        Line = diagnostic.Range.StartLine + 1;
        Column = diagnostic.Range.StartCharacter + 1;
        StartOffset = diagnostic.StartOffset;
        EndOffset = diagnostic.EndOffset;
        DocumentVersion = diagnostic.DocumentVersion;
        SessionGeneration = diagnostic.SessionGeneration;
    }

    public LanguageDiagnostic Diagnostic { get; }
    public LanguageDiagnosticSeverity Severity { get; }
    public string Message { get; }
    public string? Code { get; }
    public string FilePath { get; }
    public string FileName { get; }
    public string DocumentUri { get; }
    public int Line { get; }
    public int Column { get; }
    public int StartOffset { get; }
    public int EndOffset { get; }
    public int DocumentVersion { get; }
    public long SessionGeneration { get; }

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
            return $"{severityLabel}{codePart}: {Message}  —  {FileName}:{Line}:{Column}";
        }
    }
}
