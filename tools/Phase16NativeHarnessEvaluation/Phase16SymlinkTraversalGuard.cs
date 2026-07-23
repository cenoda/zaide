namespace Phase16NativeHarnessEvaluation;

public static class Phase16SymlinkTraversalGuard
{
    public static void RejectSymlinkEscapeOrThrow(string workspaceRoot, string entryPath)
    {
        var fullRoot = Path.GetFullPath(workspaceRoot);
        var fullEntry = Path.GetFullPath(entryPath);

        if (!Phase16WritableRootGuard.IsUnderRoot(fullRoot, fullEntry))
        {
            throw new ManifestValidationException(
                $"Workspace entry '{entryPath}' resolves outside workspace root.");
        }

        if (!File.Exists(fullEntry) && !Directory.Exists(fullEntry))
        {
            return;
        }

        var attributes = File.GetAttributes(fullEntry);
        if ((attributes & FileAttributes.ReparsePoint) == 0)
        {
            return;
        }

        var linkTarget = ResolveLinkTarget(fullEntry);
        if (linkTarget is null)
        {
            throw new ManifestValidationException(
                $"Unable to resolve symlink target for '{entryPath}'.");
        }

        var resolvedTarget = Path.GetFullPath(
            Path.IsPathRooted(linkTarget)
                ? linkTarget
                : Path.Combine(Path.GetDirectoryName(fullEntry) ?? fullRoot, linkTarget));

        if (!Phase16WritableRootGuard.IsUnderRoot(fullRoot, resolvedTarget))
        {
            throw new ManifestValidationException(
                $"Symlink '{entryPath}' escapes workspace root via target '{linkTarget}'.");
        }
    }

    private static string? ResolveLinkTarget(string path)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            return fileInfo.LinkTarget;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
