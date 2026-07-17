using System.IO;
using System.Threading.Tasks;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Infrastructure;

/// <summary>
/// Production implementation — delegates to System.IO.File async methods.
/// </summary>
public class FileService : IFileService
{
    public Task<string> ReadAllTextAsync(string path)
        => File.ReadAllTextAsync(path);

    public Task WriteAllTextAsync(string path, string contents)
        => File.WriteAllTextAsync(path, contents);
}
