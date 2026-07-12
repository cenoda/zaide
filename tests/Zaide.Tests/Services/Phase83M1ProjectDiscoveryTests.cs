using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 8.3 M1 unit tests for the project-context contracts and
/// root-level discovery implementation.
///
/// Uses a deterministic fake <see cref="IProjectFileSystem"/> so that
/// tests never depend on machine permissions, timing, or real file I/O.
/// </summary>
public sealed class Phase83M1ProjectDiscoveryTests
{
    private static readonly string TestRoot =
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Zaide_M1_Discovery"));

    private static string MakePath(string fileName) =>
        Path.GetFullPath(Path.Combine(TestRoot, fileName));

    // ── Deterministic fake ──────────────────────────────────────────────

    private sealed class FakeFileSystem : IProjectFileSystem
    {
        public string[] Files { get; set; } = Array.Empty<string>();
        public Exception? ExceptionToThrow { get; set; }

        public string[] EnumerateFiles(string directory)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            return Files;
        }
    }

    // ── SUT factory ─────────────────────────────────────────────────────

    private static ProjectDiscovery CreateDiscovery(FakeFileSystem fs)
        => new ProjectDiscovery(fs);

    // ── Tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task MixedCaseSupportedExtensions_AreClassified()
    {
        var fs = new FakeFileSystem
        {
            Files = new[]
            {
                MakePath("Project1.SLN"),
                MakePath("Project2.Sln"),
                MakePath("Project3.sln"),
                MakePath("Solution1.SLNX"),
                MakePath("Solution2.slnx"),
                MakePath("Library1.CSPROJ"),
                MakePath("Library2.CsProj"),
                MakePath("Library3.csproj"),
            },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        Assert.Null(result.Failure);
        Assert.Equal(8, result.SupportedCandidates.Count);
        Assert.Empty(result.UnsupportedFiles);

        var kinds = result.SupportedCandidates.Select(c => c.Kind).ToHashSet();
        Assert.Contains(ProjectKind.Solution, kinds);
        Assert.Contains(ProjectKind.SolutionX, kinds);
        Assert.Contains(ProjectKind.CSharpProject, kinds);
    }

    [Fact]
    public async Task AllSevenUnsupportedExtensions_AreClassified()
    {
        var fs = new FakeFileSystem
        {
            Files = new[]
            {
                MakePath("project.vbproj"),
                MakePath("project.fsproj"),
                MakePath("project.vcxproj"),
                MakePath("project.pyproj"),
                MakePath("project.dbproj"),
                MakePath("project.wixproj"),
                MakePath("project.shproj"),
            },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        Assert.Null(result.Failure);
        Assert.Empty(result.SupportedCandidates);
        Assert.Equal(7, result.UnsupportedFiles.Count);

        var extensions = result.UnsupportedFiles
            .Select(f => Path.GetExtension(f).ToLowerInvariant())
            .OrderBy(e => e)
            .ToArray();

        Assert.Equal(
            new[] { ".dbproj", ".fsproj", ".pyproj", ".shproj", ".vbproj", ".vcxproj", ".wixproj" },
            extensions);
    }

    [Fact]
    public async Task MixedSupportedAndUnsupported_ReturnsBoth()
    {
        var fs = new FakeFileSystem
        {
            Files = new[]
            {
                MakePath("app.csproj"),
                MakePath("old.vbproj"),
            },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        Assert.Null(result.Failure);
        Assert.Single(result.SupportedCandidates);
        Assert.Equal(ProjectKind.CSharpProject, result.SupportedCandidates[0].Kind);
        Assert.Single(result.UnsupportedFiles);
        Assert.Contains("old.vbproj", result.UnsupportedFiles[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownExtensions_AreIgnored()
    {
        var fs = new FakeFileSystem
        {
            Files = new[]
            {
                MakePath("readme.md"),
                MakePath("notes.txt"),
                MakePath("data.json"),
                MakePath("image.png"),
                MakePath("code.py"),     // .py is not .pyproj
                MakePath("noextension"),
                MakePath(".hidden"),
            },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        Assert.Null(result.Failure);
        Assert.Empty(result.SupportedCandidates);
        Assert.Empty(result.UnsupportedFiles);
    }

    [Fact]
    public async Task OrdinalPathOrdering_IsDeterministic()
    {
        // Paths chosen so ordinal ordering differs from case-insensitive:
        // 'A' (65) < 'B' (66) < 'a' (97) < 'c' (99)
        var fs = new FakeFileSystem
        {
            Files = new[]
            {
                MakePath("a.csproj"),
                MakePath("B.csproj"),
                MakePath("c.csproj"),
                MakePath("A.csproj"),
            },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        var fileNames = result.SupportedCandidates
            .Select(c => Path.GetFileName(c.FilePath))
            .ToArray();

        Assert.Equal(new[] { "A.csproj", "B.csproj", "a.csproj", "c.csproj" }, fileNames);
    }

    [Fact]
    public async Task SupportedCandidates_AreNormalisedAbsolutePaths()
    {
        var fs = new FakeFileSystem
        {
            Files = new[] { MakePath("project.csproj") },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        Assert.Single(result.SupportedCandidates);
        var candidate = result.SupportedCandidates[0];

        Assert.True(Path.IsPathFullyQualified(candidate.FilePath));
        Assert.Equal(MakePath("project.csproj"), candidate.FilePath);
    }

    [Fact]
    public async Task SupportedCandidate_HasCorrectDisplayName()
    {
        var fs = new FakeFileSystem
        {
            Files = new[] { MakePath("MyProject.csproj") },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        Assert.Single(result.SupportedCandidates);
        Assert.Equal("MyProject", result.SupportedCandidates[0].DisplayName);
    }

    [Fact]
    public async Task SupportedCandidate_KindIsDerivedFromExtension()
    {
        var fs = new FakeFileSystem
        {
            Files = new[]
            {
                MakePath("app.csproj"),
                MakePath("app.sln"),
                MakePath("app.slnx"),
            },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        Assert.Equal(3, result.SupportedCandidates.Count);

        var csproj = result.SupportedCandidates.Single(c => c.Kind == ProjectKind.CSharpProject);
        var sln = result.SupportedCandidates.Single(c => c.Kind == ProjectKind.Solution);
        var slnx = result.SupportedCandidates.Single(c => c.Kind == ProjectKind.SolutionX);

        Assert.EndsWith(".csproj", csproj.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".sln", sln.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".slnx", slnx.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyRoot_MapsToNoProject()
    {
        var fs = new FakeFileSystem { Files = Array.Empty<string>() };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        Assert.Null(result.Failure);
        Assert.Empty(result.SupportedCandidates);
        Assert.Empty(result.UnsupportedFiles);
    }

    [Fact]
    public async Task UnsupportedOnly_MapsToUnsupported()
    {
        var fs = new FakeFileSystem
        {
            Files = new[] { MakePath("project.vbproj") },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        Assert.Null(result.Failure);
        Assert.Empty(result.SupportedCandidates);
        Assert.NotEmpty(result.UnsupportedFiles);
    }

    [Fact]
    public async Task MissingRoot_ReturnsNotFoundFailure()
    {
        var fs = new FakeFileSystem
        {
            ExceptionToThrow = new DirectoryNotFoundException("Directory not found."),
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("nonexistent");

        Assert.NotNull(result.Failure);
        Assert.Equal(ProjectDiscoveryFailureKind.NotFound, result.Failure.Kind);
        Assert.Empty(result.SupportedCandidates);
        Assert.Empty(result.UnsupportedFiles);
    }

    [Fact]
    public async Task UnauthorizedRoot_ReturnsUnauthorizedFailure()
    {
        var fs = new FakeFileSystem
        {
            ExceptionToThrow = new UnauthorizedAccessException("Access denied."),
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("inaccessible");

        Assert.NotNull(result.Failure);
        Assert.Equal(ProjectDiscoveryFailureKind.Unauthorized, result.Failure.Kind);
        Assert.Empty(result.SupportedCandidates);
        Assert.Empty(result.UnsupportedFiles);
    }

    [Fact]
    public async Task IoErrorRoot_ReturnsIoFailure()
    {
        var fs = new FakeFileSystem
        {
            ExceptionToThrow = new IOException("I/O error occurred."),
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("problematic");

        Assert.NotNull(result.Failure);
        Assert.Equal(ProjectDiscoveryFailureKind.Io, result.Failure.Kind);
        Assert.Empty(result.SupportedCandidates);
        Assert.Empty(result.UnsupportedFiles);
    }

    [Fact]
    public async Task AlreadyCancelledToken_ThrowsImmediately()
    {
        // ProjectDiscovery must honor an already-cancelled token via
        // ThrowIfCancellationRequested BEFORE touching the filesystem.
        var fs = new FakeFileSystem
        {
            // The fake should never be reached because the early check
            // throws first. An exception here would mask a test bug.
            ExceptionToThrow = new InvalidOperationException("Must not be reached."),
        };

        var discovery = CreateDiscovery(fs);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => discovery.DiscoverAsync("root", cts.Token));
    }

    [Fact]
    public async Task CancellationFromFilesystem_PropagatesWithoutCatch()
    {
        // OperationCanceledException thrown by the filesystem seam must
        // propagate through ProjectDiscovery — it is NOT caught by any
        // of the try/catch handlers.
        var fs = new FakeFileSystem
        {
            ExceptionToThrow = new OperationCanceledException("Cancelled."),
        };

        var discovery = CreateDiscovery(fs);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => discovery.DiscoverAsync("root"));

        Assert.Contains("Cancelled", exception.Message);
    }

    [Fact]
    public async Task InvalidRoot_ArgumentException_ReturnsInvalidRootFailure()
    {
        // ArgumentException from EnumerateFiles (e.g. empty root) is
        // converted to InvalidRoot — it does NOT propagate.
        var fs = new FakeFileSystem
        {
            ExceptionToThrow = new ArgumentException("Path cannot be empty."),
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("");

        Assert.NotNull(result.Failure);
        Assert.Equal(ProjectDiscoveryFailureKind.InvalidRoot, result.Failure.Kind);
        Assert.Empty(result.SupportedCandidates);
        Assert.Empty(result.UnsupportedFiles);
    }

    [Fact]
    public async Task InvalidRoot_NotSupportedException_ReturnsInvalidRootFailure()
    {
        // NotSupportedException from EnumerateFiles (e.g. invalid path
        // characters on some platforms) is converted to InvalidRoot.
        var fs = new FakeFileSystem
        {
            ExceptionToThrow = new NotSupportedException("Path format not supported."),
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("invalid:");

        Assert.NotNull(result.Failure);
        Assert.Equal(ProjectDiscoveryFailureKind.InvalidRoot, result.Failure.Kind);
        Assert.Empty(result.SupportedCandidates);
        Assert.Empty(result.UnsupportedFiles);
    }

    [Fact]
    public async Task MixedCaseUnsupportedExtensions_AreClassified()
    {
        var fs = new FakeFileSystem
        {
            Files = new[]
            {
                MakePath("legacy.VBPROJ"),
                MakePath("legacy.FSPROJ"),
                MakePath("old.VCXPROJ"),
            },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        Assert.Null(result.Failure);
        Assert.Empty(result.SupportedCandidates);
        Assert.Equal(3, result.UnsupportedFiles.Count);
    }

    [Fact]
    public async Task UnsupportedFiles_CollectionIsSortedOrdinally()
    {
        var fs = new FakeFileSystem
        {
            Files = new[]
            {
                MakePath("z.vbproj"),
                MakePath("a.vbproj"),
                MakePath("M.vbproj"),
            },
        };

        var discovery = CreateDiscovery(fs);
        var result = await discovery.DiscoverAsync("irrelevant");

        var fileNames = result.UnsupportedFiles
            .Select(f => Path.GetFileName(f))
            .ToArray();

        // 'A' (65) < 'M' (77) < 'a' (97) < 'z' (122) for case-sensitive ordinal
        Assert.Equal(new[] { "M.vbproj", "a.vbproj", "z.vbproj" }, fileNames);
    }
}
