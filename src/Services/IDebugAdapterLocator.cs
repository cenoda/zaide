namespace Zaide.Services;

/// <summary>
/// Resolves the NetCoreDbg adapter executable for a debug session.
/// </summary>
public interface IDebugAdapterLocator
{
    /// <summary>
    /// Returns the absolute adapter executable path when found; otherwise <c>null</c>.
    /// </summary>
    string? Resolve();
}
