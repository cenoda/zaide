namespace Zaide.Services;

/// <summary>
/// Synchronous secret boundary. API keys and other sensitive values are stored
/// separately from <c>settings.json</c> via this interface.
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// Returns the value associated with <paramref name="key"/>,
    /// or <c>null</c> if the key does not exist.
    /// </summary>
    string? Get(string key);

    /// <summary>
    /// Stores or updates the value associated with <paramref name="key"/>.
    /// </summary>
    void Set(string key, string value);

    /// <summary>
    /// Removes the value associated with <paramref name="key"/>.
    /// No-op if the key does not exist.
    /// </summary>
    void Delete(string key);
}
