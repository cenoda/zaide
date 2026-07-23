using System.Security.Cryptography;
using System.Text;

namespace Phase16NativeHarnessEvaluation;

public sealed class Phase16WorkspaceEvidence
{
    public required string FixtureHash { get; init; }
    public required string PostTrialInventoryHash { get; init; }
    public required bool WorkspaceDirty { get; init; }
    public required bool ResetSucceeded { get; init; }
    public required IReadOnlyList<string> ChangedRelativePaths { get; init; }
}

public static class Phase16WorkspaceManager
{
    public static string CreateTrialWorkspace(string artifactRoot, string trialId)
    {
        var workspaceRoot = Path.Combine(
            artifactRoot,
            "phase-16",
            "artifacts",
            "trials",
            trialId,
            "workspace");
        Directory.CreateDirectory(workspaceRoot);
        return workspaceRoot;
    }

    public static void MaterializeFixtureOrThrow(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> fixtureTree)
    {
        var normalized = FixturePathCanonicalizer.NormalizeTreeOrThrow(fixtureTree);
        foreach (var entry in normalized)
        {
            var targetPath = Phase16WritableRootGuard.ResolveUnderWritableRootOrThrow(
                workspaceRoot,
                entry.Key);
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.WriteAllText(targetPath, entry.Value);
            Phase16SymlinkTraversalGuard.RejectSymlinkEscapeOrThrow(workspaceRoot, targetPath);
        }
    }

    public static Phase16WorkspaceEvidence CollectEvidenceOrThrow(
        string workspaceRoot,
        string fixtureHash,
        IReadOnlyDictionary<string, string> baselineFixtureTree)
    {
        var normalizedBaseline = FixturePathCanonicalizer.NormalizeTreeOrThrow(baselineFixtureTree);
        var changedPaths = new List<string>();
        foreach (var entry in normalizedBaseline)
        {
            var targetPath = Phase16WritableRootGuard.ResolveUnderWritableRootOrThrow(
                workspaceRoot,
                entry.Key);
            if (!File.Exists(targetPath))
            {
                changedPaths.Add(entry.Key);
                continue;
            }

            var current = File.ReadAllText(targetPath);
            if (!string.Equals(current, entry.Value, StringComparison.Ordinal))
            {
                changedPaths.Add(entry.Key);
            }
        }

        foreach (var extraPath in EnumerateRelativeFiles(workspaceRoot))
        {
            if (!normalizedBaseline.ContainsKey(extraPath))
            {
                changedPaths.Add(extraPath);
            }
        }

        changedPaths.Sort(StringComparer.Ordinal);
        var inventoryHash = ComputeInventoryHash(workspaceRoot);
        return new Phase16WorkspaceEvidence
        {
            FixtureHash = fixtureHash,
            PostTrialInventoryHash = inventoryHash,
            WorkspaceDirty = changedPaths.Count > 0,
            ResetSucceeded = false,
            ChangedRelativePaths = changedPaths,
        };
    }

    public static bool ResetWorkspaceOrThrow(
        string workspaceRoot,
        IReadOnlyDictionary<string, string> baselineFixtureTree)
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }

        Directory.CreateDirectory(workspaceRoot);
        MaterializeFixtureOrThrow(workspaceRoot, baselineFixtureTree);
        return true;
    }

    public static void CleanupTrialDirectoryOrThrow(string trialDirectory)
    {
        if (!Directory.Exists(trialDirectory))
        {
            return;
        }

        Directory.Delete(trialDirectory, recursive: true);
        if (Directory.Exists(trialDirectory))
        {
            throw new IOException($"Failed to delete trial directory '{trialDirectory}'.");
        }
    }

    public static int CountCapturedFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count();
    }

    private static IEnumerable<string> EnumerateRelativeFiles(string workspaceRoot)
    {
        if (!Directory.Exists(workspaceRoot))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(workspaceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(workspaceRoot, file).Replace('\\', '/');
            yield return relative;
        }
    }

    private static string ComputeInventoryHash(string workspaceRoot)
    {
        var builder = new StringBuilder();
        foreach (var relativePath in EnumerateRelativeFiles(workspaceRoot).OrderBy(static path => path, StringComparer.Ordinal))
        {
            var fullPath = Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var content = File.ReadAllText(fullPath);
            builder.Append(relativePath);
            builder.Append('\n');
            builder.Append(content);
            builder.Append("\n---\n");
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
