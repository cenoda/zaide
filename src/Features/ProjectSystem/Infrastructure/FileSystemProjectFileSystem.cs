using System.IO;
using System.Linq;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Infrastructure;

/// <summary>
/// Production implementation of <see cref="IProjectFileSystem"/> that
/// delegates to <c>Directory.EnumerateFiles</c> with <c>TopDirectoryOnly</c>.
/// Project files always live at workspace root, so recursion is unnecessary.
/// </summary>
public sealed class FileSystemProjectFileSystem : IProjectFileSystem
{
    public string[] EnumerateFiles(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                        .ToArray();
    }
}
