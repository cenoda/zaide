namespace Zaide.Services;

/// <summary>
/// Parsed DAP thread entry.
/// </summary>
/// <param name="Id">Adapter thread id.</param>
/// <param name="Name">Display name when supplied by the adapter.</param>
public sealed record DapThreadInfo(int Id, string Name);