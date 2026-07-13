namespace Zaide.Services;

/// <summary>
/// Resolves the csharp-ls server binary from PATH or an optional configured path.
/// </summary>
public interface ILanguageServerBinaryLocator
{
    /// <summary>
    /// Returns the absolute path to a runnable csharp-ls binary, or <c>null</c> when not found.
    /// </summary>
    string? Resolve();
}
