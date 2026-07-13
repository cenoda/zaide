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

        return IsEligiblePath(document.FilePath);
    }

    /// <summary>Returns whether <paramref name="filePath"/> is a synchronized C# document.</summary>
    public static bool IsEligiblePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        return string.Equals(Path.GetExtension(filePath), ".cs", StringComparison.OrdinalIgnoreCase);
    }
}
