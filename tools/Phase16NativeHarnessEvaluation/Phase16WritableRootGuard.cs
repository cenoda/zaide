namespace Phase16NativeHarnessEvaluation;

public static class Phase16WritableRootGuard
{
    public static string ResolveUnderWritableRootOrThrow(
        string writableRoot,
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(writableRoot))
        {
            throw new ManifestValidationException("Writable root is required.");
        }

        var normalizedRelative = FixturePathCanonicalizer.NormalizeOrThrow(relativePath);
        var fullRoot = Path.GetFullPath(writableRoot);
        var combined = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelative.Replace('/', Path.DirectorySeparatorChar)));

        if (!IsUnderRoot(fullRoot, combined))
        {
            throw new ManifestValidationException(
                $"Path '{relativePath}' escapes writable root '{writableRoot}'.");
        }

        return combined;
    }

    public static void EnsureWritableRootOrThrow(string path, IReadOnlyCollection<string> writableRoots)
    {
        if (writableRoots.Count == 0)
        {
            throw new ManifestValidationException("At least one writable root is required.");
        }

        var fullPath = Path.GetFullPath(path);
        foreach (var root in writableRoots)
        {
            var fullRoot = Path.GetFullPath(root);
            if (IsUnderRoot(fullRoot, fullPath) || string.Equals(fullRoot, fullPath, StringComparison.Ordinal))
            {
                return;
            }
        }

        throw new ManifestValidationException(
            $"Path '{path}' is outside all declared writable roots.");
    }

    public static bool IsUnderRoot(string rootPath, string candidatePath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        return normalizedCandidate.StartsWith(normalizedRoot, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
    }
}
