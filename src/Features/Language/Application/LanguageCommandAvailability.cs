using System;
using Zaide.Features.Language.Contracts;
using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Shared rules for Phase 10 command and surface availability.
/// Matches negotiated server capability, session readiness, and active-document eligibility.
/// </summary>
public static class LanguageCommandAvailability
{
    public static bool HasActiveEligibleDocument(string? documentId) =>
        !string.IsNullOrWhiteSpace(documentId) &&
        !documentId.StartsWith("__untitled_", StringComparison.Ordinal) &&
        LanguageDocumentSyncPolicy.IsEligiblePath(documentId);

    public static bool IsSessionReady(ILanguageSessionService sessionService, out ILanguageServerSession? session)
    {
        session = null;
        var snapshot = sessionService.Current;
        if (snapshot.State != LanguageSessionState.Ready)
            return false;

        session = sessionService.TryGetReadySession(snapshot.Generation);
        return session is not null;
    }

    public static bool CanUseActiveDocumentFeature(
        ILanguageSessionService sessionService,
        string? documentId,
        Func<LanguageServerCapabilities, bool> capabilityCheck)
    {
        if (!HasActiveEligibleDocument(documentId))
            return false;

        if (!IsSessionReady(sessionService, out var session))
            return false;

        return capabilityCheck(session!.Capabilities);
    }

    public static bool CanUseWorkspaceSymbols(ILanguageSessionService sessionService) =>
        IsSessionReady(sessionService, out var session) &&
        session!.Capabilities.WorkspaceSymbolSupported;
}