namespace Zaide.Services;

/// <summary>
/// Injectable seam wrapping low-level directory enumeration.
/// Exists solely to allow deterministic test injection of a fake filesystem.
/// </summary>
public interface IProjectFileSystem
{
    /// <summary>
    /// Enumerates all files in the given <paramref name="directory"/>
    /// (top-level only, no recursion).
    /// </summary>
    /// <param name="directory">The directory to enumerate.</param>
    /// <returns>An array of full file paths.</returns>
    string[] EnumerateFiles(string directory);
}
