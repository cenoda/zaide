using System.IO;

namespace Zaide.Features.Workspace.Presentation;

internal static class FileIconKeyResolver
{
    public static string GetIconKey(string? path, bool isDirectory = false)
    {
        if (isDirectory)
            return "Icon.Folder";

        var ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".cs" or ".ts" or ".js" or ".jsx" or ".tsx" or ".json" or ".xml" or ".html" or ".css" or ".axaml" => "Icon.Code",
            ".md" or ".txt" or ".log" => "Icon.Text",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" => "Icon.Image",
            ".sln" or ".slnx" or ".csproj" => "Icon.Project",
            ".editorconfig" or ".gitignore" or ".yml" or ".yaml" or ".toml" => "Icon.Config",
            _ => "Icon.Unknown"
        };
    }
}
