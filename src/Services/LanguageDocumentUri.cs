using System;
using System.IO;

namespace Zaide.Services;

/// <summary>
/// Converts on-disk paths to LSP <c>file://</c> document URIs.
/// </summary>
internal static class LanguageDocumentUri
{
    /// <summary>
    /// Returns the absolute <c>file://</c> URI for <paramref name="path"/>.
    /// </summary>
    public static string FromPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith('/'))
            return new Uri(fullPath).AbsoluteUri;

        return "file://" + fullPath;
    }
}
