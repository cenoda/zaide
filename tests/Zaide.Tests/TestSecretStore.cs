using System.Collections.Generic;
using Zaide.Services;

namespace Zaide.Tests;

/// <summary>
/// Simple in-memory <see cref="ISecretStore"/> for unit tests.
/// </summary>
internal sealed class TestSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _data = new();

    public string? Get(string key) =>
        _data.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string value) =>
        _data[key] = value;

    public void Delete(string key) =>
        _data.Remove(key);
}
