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

    /// <summary>
    /// Normalizes a document URI for stable dictionary keys and comparisons.
    /// </summary>
    public static string Normalize(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        try
        {
            return new Uri(uri).AbsoluteUri;
        }
        catch (UriFormatException)
        {
            return uri;
        }
    }

    /// <summary>
    /// Attempts to convert a <c>file://</c> URI to an absolute local path.
    /// </summary>
    public static bool TryGetPath(string uri, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        try
        {
            var parsed = new Uri(uri);
            if (!parsed.IsFile)
                return false;

            path = parsed.LocalPath;
            return !string.IsNullOrWhiteSpace(path);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}
