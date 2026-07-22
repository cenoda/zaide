using System;
using System.Collections.Generic;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16FixturePathCanonicalizerTests
{
    [Fact]
    public void NormalizeTree_IsOrderIndependent()
    {
        var left = FixtureTreeHasher.ComputeHash(new Dictionary<string, string>
        {
            ["b.txt"] = "two",
            ["a.txt"] = "one",
        });
        var right = FixtureTreeHasher.ComputeHash(new Dictionary<string, string>
        {
            ["a.txt"] = "one",
            ["b.txt"] = "two",
        });

        Assert.Equal(left, right);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_RejectsEmptyPath(string path)
    {
        var ex = Assert.Throws<ManifestValidationException>(() => FixturePathCanonicalizer.NormalizeOrThrow(path));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/a.txt")]
    [InlineData("\\a.txt")]
    [InlineData("C:\\tmp\\a.txt")]
    public void Normalize_RejectsAbsolutePath(string path)
    {
        var ex = Assert.Throws<ManifestValidationException>(() => FixturePathCanonicalizer.NormalizeOrThrow(path));
        Assert.Contains("relative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("a/../b.txt")]
    [InlineData("a/./b.txt")]
    [InlineData("..")]
    [InlineData(".")]
    public void Normalize_RejectsDotSegments(string path)
    {
        var ex = Assert.Throws<ManifestValidationException>(() => FixturePathCanonicalizer.NormalizeOrThrow(path));
        Assert.Contains("forbidden segment", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("a//b.txt")]
    public void Normalize_RejectsEmptySegment(string path)
    {
        var ex = Assert.Throws<ManifestValidationException>(() => FixturePathCanonicalizer.NormalizeOrThrow(path));
        Assert.Contains("empty segment", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_RejectsRootOnlyPath()
    {
        var ex = Assert.Throws<ManifestValidationException>(() => FixturePathCanonicalizer.NormalizeOrThrow("/"));
        Assert.Contains("relative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeTree_RejectsSlashVariantCollision()
    {
        var ex = Assert.Throws<ManifestValidationException>(() => FixtureTreeHasher.ComputeHash(
            new Dictionary<string, string>
            {
                ["a.txt"] = "one",
                ["/a.txt"] = "two",
            }));

        Assert.True(
            ex.Message.Contains("relative", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Duplicate normalized", StringComparison.OrdinalIgnoreCase),
            ex.Message);
    }

    [Fact]
    public void NormalizeTree_RejectsBackslashVariantCollision()
    {
        var ex = Assert.Throws<ManifestValidationException>(() => FixtureTreeHasher.ComputeHash(
            new Dictionary<string, string>
            {
                ["workspace/a.txt"] = "one",
                ["workspace\\a.txt"] = "two",
            }));

        Assert.Contains("Duplicate normalized", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeTree_RejectsTrailingSlash()
    {
        var ex = Assert.Throws<ManifestValidationException>(() => FixtureTreeHasher.ComputeHash(
            new Dictionary<string, string>
            {
                ["workspace/"] = "one",
            }));

        Assert.Contains("must not end with '/'", ex.Message, StringComparison.Ordinal);
    }
}
