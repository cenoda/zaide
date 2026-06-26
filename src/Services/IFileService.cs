using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Abstracts file I/O so ViewModels never touch System.IO directly.
/// Async by default — no blocking on the UI thread.
/// </summary>
public interface IFileService
{
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string contents);
}
