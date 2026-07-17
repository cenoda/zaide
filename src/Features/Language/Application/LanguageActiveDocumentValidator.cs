using Zaide.Models;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Language.Contracts;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Shared stale-result checks for active-document language requests.
/// </summary>
internal static class LanguageActiveDocumentValidator
{
    /// <summary>
    /// Validates that <paramref name="filePath"/> is the active workspace document,
    /// the session is ready at <paramref name="generation"/>, and the document is
    /// open in the bridge at <paramref name="documentVersion"/>.
    /// </summary>
    public static bool TryValidate(
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        ILanguageSessionService sessionService,
        ILanguageDocumentBridge documentBridge,
        string filePath,
        long generation,
        int documentVersion,
        out LanguageTrackedDocumentInfo tracked)
    {
        tracked = default;

        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var active = workspace.ActiveDocument;
        if (active is null ||
            !string.Equals(active.FilePath, filePath, System.StringComparison.Ordinal))
        {
            return false;
        }

        var snapshot = sessionService.Current;
        if (snapshot.State != LanguageSessionState.Ready || snapshot.Generation != generation)
            return false;

        var uri = LanguageDocumentUri.FromPath(filePath);
        if (!documentBridge.TryGetOpenDocument(uri, out tracked))
            return false;

        if (tracked.Generation != generation || tracked.Version != documentVersion)
            return false;

        return true;
    }
}
