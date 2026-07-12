using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Deterministic project-file discovery at workspace root.
/// Classifies files by extension, normalises paths, and returns
/// a structured <see cref="ProjectDiscoveryResult"/>.
///
/// Catches expected filesystem exceptions and converts them to the
/// corresponding <see cref="ProjectDiscoveryFailure"/>. Does NOT catch
/// <see cref="OperationCanceledException"/> — cancellation is the caller's
/// responsibility and must not be converted into a Failed result.
/// </summary>
public sealed class ProjectDiscovery : IProjectDiscovery
{
    private readonly IProjectFileSystem _fileSystem;

    public ProjectDiscovery(IProjectFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public async Task<ProjectDiscoveryResult> DiscoverAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string[] files;
        try
        {
            files = _fileSystem.EnumerateFiles(workspaceRoot);
        }
        catch (ArgumentException ex)
        {
            return new ProjectDiscoveryResult(
                Array.Empty<ProjectCandidate>(),
                Array.Empty<string>(),
                new ProjectDiscoveryFailure(ProjectDiscoveryFailureKind.InvalidRoot, ex.Message));
        }
        catch (NotSupportedException ex)
        {
            return new ProjectDiscoveryResult(
                Array.Empty<ProjectCandidate>(),
                Array.Empty<string>(),
                new ProjectDiscoveryFailure(ProjectDiscoveryFailureKind.InvalidRoot, ex.Message));
        }
        catch (DirectoryNotFoundException ex)
        {
            return new ProjectDiscoveryResult(
                Array.Empty<ProjectCandidate>(),
                Array.Empty<string>(),
                new ProjectDiscoveryFailure(ProjectDiscoveryFailureKind.NotFound, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ProjectDiscoveryResult(
                Array.Empty<ProjectCandidate>(),
                Array.Empty<string>(),
                new ProjectDiscoveryFailure(ProjectDiscoveryFailureKind.Unauthorized, ex.Message));
        }
        catch (IOException ex)
        {
            return new ProjectDiscoveryResult(
                Array.Empty<ProjectCandidate>(),
                Array.Empty<string>(),
                new ProjectDiscoveryFailure(ProjectDiscoveryFailureKind.Io, ex.Message));
        }

        // Normalise to absolute, sort by ordinal full path
        var sorted = files
            .Select(Path.GetFullPath)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        var supported = new List<ProjectCandidate>();
        var unsupported = new List<string>();

        foreach (var path in sorted)
        {
            var ext = Path.GetExtension(path);

            if (string.IsNullOrEmpty(ext))
                continue;

            var extLower = ext.ToLowerInvariant();

            if (extLower == ".sln")
                supported.Add(CreateCandidate(path, ProjectKind.Solution));
            else if (extLower == ".slnx")
                supported.Add(CreateCandidate(path, ProjectKind.SolutionX));
            else if (extLower == ".csproj")
                supported.Add(CreateCandidate(path, ProjectKind.CSharpProject));
            else if (KnownUnsupported.Contains(extLower))
                unsupported.Add(path);
            // else: unknown extension → ignored
        }

        return new ProjectDiscoveryResult(supported, unsupported, null);
    }

    private static ProjectCandidate CreateCandidate(string path, ProjectKind kind)
    {
        return new ProjectCandidate(
            FilePath: Path.GetFullPath(path),
            DisplayName: Path.GetFileNameWithoutExtension(path),
            Kind: kind);
    }

    private static readonly HashSet<string> KnownUnsupported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vbproj", ".fsproj", ".vcxproj", ".pyproj", ".dbproj", ".wixproj", ".shproj",
    };
}
