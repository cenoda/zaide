namespace Phase16NativeHarnessEvaluation;

public static class FixturePathCanonicalizer
{
    public static IReadOnlyDictionary<string, string> NormalizeTreeOrThrow(
        IReadOnlyDictionary<string, string> relativePathToUtf8Content)
    {
        var normalizedTree = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in relativePathToUtf8Content)
        {
            var normalizedPath = NormalizeOrThrow(entry.Key);
            if (normalizedTree.ContainsKey(normalizedPath))
            {
                throw new ManifestValidationException(
                    $"Duplicate normalized fixture path '{normalizedPath}'.");
            }

            normalizedTree[normalizedPath] = entry.Value;
        }

        return normalizedTree;
    }

    public static string NormalizeOrThrow(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new ManifestValidationException("Fixture path is empty.");
        }

        if (rawPath.StartsWith('/') ||
            rawPath.StartsWith('\\') ||
            (rawPath.Length >= 2 && rawPath[1] == ':'))
        {
            throw new ManifestValidationException($"Fixture path '{rawPath}' must be relative.");
        }

        var normalized = rawPath.Replace('\\', '/');
        if (normalized.StartsWith('/'))
        {
            throw new ManifestValidationException($"Fixture path '{rawPath}' must be relative.");
        }

        if (normalized.EndsWith('/'))
        {
            throw new ManifestValidationException($"Fixture path '{rawPath}' must not end with '/'.");
        }

        var segments = normalized.Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                throw new ManifestValidationException(
                    $"Fixture path '{rawPath}' contains an empty segment.");
            }

            if (segment is "." or "..")
            {
                throw new ManifestValidationException(
                    $"Fixture path '{rawPath}' contains forbidden segment '{segment}'.");
            }
        }

        return normalized;
    }
}
