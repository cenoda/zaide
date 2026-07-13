using System;
using System.IO;
using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// Phase 10 eligibility rules for language document synchronization.
/// </summary>
internal static class LanguageDocumentSyncPolicy
{
    /// <summary>
    /// Returns whether <paramref name="document"/> should participate in LSP sync.
    /// </summary>
    public static bool IsEligible(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(document.FilePath))
            return false;

        return string.Equals(Path.GetExtension(document.FilePath), ".cs", StringComparison.OrdinalIgnoreCase);
    }
}
