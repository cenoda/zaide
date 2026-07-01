using System;
using System.Collections.Generic;
using System.IO;

namespace Zaide.Services;

/// <summary>
/// Editor policy for supported file types.
/// Determines which files open in the editor vs show an error.
/// </summary>
public static class SupportedFileTypes
{
    private static readonly HashSet<string> SupportedExtensions = new(
        new[] { ".cs", ".json", ".md", ".txt", ".xml", ".axaml", ".csproj",
                ".sln", ".slnx", ".props", ".targets", ".config",
                ".config", ".gitignore", ".gitattributes", ".yml",
                ".yaml", ".css", ".html", ".js", ".ts", ".fs", ".vb",
                ".xaml", ".resx", ".razor", ".cshtml", ".svg" },
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the file should be opened in the editor.
    /// Returns false for binary or unknown file types.
    /// </summary>
    public static bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path);
        return SupportedExtensions.Contains(ext);
    }

    /// <summary>
    /// Returns a status message for unsupported files, or null if supported.
    /// </summary>
    public static string? GetUnsupportedMessage(string path)
    {
        var ext = Path.GetExtension(path);
        if (SupportedExtensions.Contains(ext))
            return null;

        return ext.Length > 0
            ? $"Unsupported file type: {ext}"
            : "Unsupported file type: (no extension)";
    }
}