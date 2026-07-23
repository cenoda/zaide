namespace Phase16NativeHarnessEvaluation;

public static class Phase16EnvironmentPolicy
{
    public static IReadOnlyDictionary<string, string> FilterAllowlistedOrThrow(
        IReadOnlyDictionary<string, string>? hostEnvironment,
        IReadOnlyCollection<string> allowedVariableNames)
    {
        var allowed = new HashSet<string>(allowedVariableNames, StringComparer.Ordinal);
        var filtered = new SortedDictionary<string, string>(StringComparer.Ordinal);

        if (hostEnvironment is null)
        {
            return filtered;
        }

        foreach (var entry in hostEnvironment)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                throw new ManifestValidationException("Environment variable names must be non-empty.");
            }

            if (!allowed.Contains(entry.Key))
            {
                throw new ManifestValidationException(
                    $"Environment variable '{entry.Key}' is not allowlisted.");
            }

            if (filtered.ContainsKey(entry.Key))
            {
                throw new ManifestValidationException(
                    $"Duplicate allowlisted environment variable '{entry.Key}'.");
            }

            filtered[entry.Key] = entry.Value ?? string.Empty;
        }

        return filtered;
    }

    public static IReadOnlyDictionary<string, string> CreateSandboxEnvironment(
        IReadOnlyDictionary<string, string> allowlistedVariables)
    {
        var sandboxEnvironment = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin:/bin",
        };

        foreach (var entry in allowlistedVariables)
        {
            sandboxEnvironment[entry.Key] = entry.Value;
        }

        return sandboxEnvironment;
    }
}
