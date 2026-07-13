namespace Zaide.Services;

/// <summary>
/// User-facing projections of <see cref="LanguageSessionSnapshot"/> state.
/// UI layers must use these helpers rather than raw failure messages or protocol exceptions.
/// </summary>
public static class LanguageSessionStatusPolicy
{
    /// <summary>Short status-bar label for the current session state.</summary>
    public static string MapStatusBarText(LanguageSessionSnapshot snapshot) =>
        snapshot.State switch
        {
            LanguageSessionState.Ready => "C# · Ready",
            LanguageSessionState.Loading => "C# · Loading…",
            LanguageSessionState.Failed => "C# · Failed",
            LanguageSessionState.Cancelled => "C# · Cancelled",
            LanguageSessionState.Unavailable when snapshot.ProjectFilePath is not null =>
                "C# · Unavailable",
            _ => string.Empty,
        };

    /// <summary>Problems-panel status when the session is not ready.</summary>
    public static string? MapProblemsStatusMessage(LanguageDiagnosticsSnapshot snapshot) =>
        snapshot.State switch
        {
            LanguageSessionState.Ready when snapshot.Diagnostics.Count == 0 =>
                "No problems.",
            LanguageSessionState.Ready =>
                null,
            LanguageSessionState.Loading =>
                "Language intelligence loading…",
            LanguageSessionState.Failed =>
                MapFailureMessage(snapshot.Failure),
            LanguageSessionState.Cancelled =>
                "Language session cancelled.",
            _ =>
                "Language intelligence unavailable.",
        };

    /// <summary>Truthful, non-protocol user-facing failure text.</summary>
    public static string MapFailureMessage(LanguageSessionFailure? failure) =>
        failure?.Kind switch
        {
            LanguageSessionFailureKind.MissingServerBinary =>
                "C# language server not found. Install with: dotnet tool install -g csharp-ls",
            LanguageSessionFailureKind.InitializeFailed =>
                "C# language server failed to start.",
            LanguageSessionFailureKind.ServerExited =>
                "C# language server exited unexpectedly.",
            null => "Language server failed.",
            _ => "Language server failed.",
        };
}