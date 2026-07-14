namespace Zaide.Services;

/// <summary>
/// Options used when launching one NetCoreDbg adapter child process.
/// </summary>
/// <param name="Generation">Session generation captured at creation time.</param>
/// <param name="AdapterPath">Absolute path to the <c>netcoredbg</c> executable.</param>
public sealed record DebugAdapterStartOptions(long Generation, string AdapterPath);
