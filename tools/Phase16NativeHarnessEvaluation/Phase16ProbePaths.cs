namespace Phase16NativeHarnessEvaluation;

public static class Phase16ProbePaths
{
    public static string ResolveProbeScript(string scriptFileName)
    {
        var assemblyDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(assemblyDirectory, "Probes", scriptFileName),
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "tools",
                "Phase16NativeHarnessEvaluation",
                "Probes",
                scriptFileName)),
            Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "tools",
                "Phase16NativeHarnessEvaluation",
                "Probes",
                scriptFileName)),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Probe script '{scriptFileName}' was not found.", scriptFileName);
    }
}
