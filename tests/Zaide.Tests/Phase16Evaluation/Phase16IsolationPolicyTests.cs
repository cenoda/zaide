using System;
using System.Collections.Generic;
using System.IO;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

[Collection("Phase16Isolation")]
public sealed class Phase16IsolationPolicyTests
{
    [Fact]
    public void EnvironmentPolicy_RejectsNonAllowlistedVariables()
    {
        var hostEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PHASE16_FORBIDDEN"] = "value",
        };

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16EnvironmentPolicy.FilterAllowlistedOrThrow(
                hostEnvironment,
                ["PHASE16_ALLOWED"]));

        Assert.Contains("PHASE16_FORBIDDEN", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentPolicy_AllowsExactAllowlist()
    {
        var filtered = Phase16EnvironmentPolicy.FilterAllowlistedOrThrow(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PHASE16_ALLOWED"] = "value",
            },
            ["PHASE16_ALLOWED"]);

        Assert.Equal("value", filtered["PHASE16_ALLOWED"]);
    }

    [Fact]
    public void WritableRootGuard_RejectsTraversalSegments()
    {
        var root = Path.Combine(Path.GetTempPath(), $"phase16-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var ex = Assert.Throws<ManifestValidationException>(() =>
                Phase16WritableRootGuard.ResolveUnderWritableRootOrThrow(root, "../escape.txt"));
            Assert.Contains("forbidden segment", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SymlinkTraversalGuard_RejectsWorkspaceEscape()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var workspace = Path.Combine(Path.GetTempPath(), $"phase16-ws-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        var linkPath = Path.Combine(workspace, "escape-link");
        try
        {
            File.CreateSymbolicLink(linkPath, "/etc/passwd");
            var ex = Assert.Throws<ManifestValidationException>(() =>
                Phase16SymlinkTraversalGuard.RejectSymlinkEscapeOrThrow(workspace, linkPath));
            Assert.Contains("escapes workspace root", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(linkPath))
            {
                File.Delete(linkPath);
            }

            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void OutputRedactor_RemovesCredentialPatterns()
    {
        var redacted = Phase16OutputRedactor.RedactOrThrow(
            "api_key=super-secret\nAuthorization: Bearer abc.def.ghi");

        Assert.DoesNotContain("super-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc.def.ghi", redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanupGate_BlocksSubsequentRunsAfterFailure()
    {
        Phase16CleanupGate.ResetForTesting();
        try
        {
            Phase16CleanupGate.RecordCleanupFailure("trial directory remained on disk");
            Assert.True(Phase16CleanupGate.IsBlocked);

            var ex = Assert.Throws<Phase16CleanupBlockedException>(() =>
                Phase16CleanupGate.EnsureNotBlockedOrThrow());
            Assert.Contains("trial directory remained on disk", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Phase16CleanupGate.ResetForTesting();
        }
    }

    [Fact]
    public void WorkspaceManager_RecordsDirtyAndResetEvidence()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"phase16-ws-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        var fixtureTree = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["README.md"] = "baseline",
        };

        try
        {
            Phase16WorkspaceManager.MaterializeFixtureOrThrow(workspace, fixtureTree);
            File.WriteAllText(Path.Combine(workspace, "dirty.txt"), "changed");

            var evidence = Phase16WorkspaceManager.CollectEvidenceOrThrow(
                workspace,
                "fixture-hash",
                fixtureTree);
            Assert.True(evidence.WorkspaceDirty);
            Assert.Contains("dirty.txt", evidence.ChangedRelativePaths);

            Phase16WorkspaceManager.ResetWorkspaceOrThrow(workspace, fixtureTree);
            var resetEvidence = Phase16WorkspaceManager.CollectEvidenceOrThrow(
                workspace,
                "fixture-hash",
                fixtureTree);
            Assert.False(resetEvidence.WorkspaceDirty);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }
}
