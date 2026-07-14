namespace Zaide.Services;

/// <summary>
/// Parsed DAP scope entry.
/// </summary>
/// <param name="Name">Scope display name.</param>
/// <param name="VariablesReference">DAP variables reference for first-level children.</param>
public sealed record DapScopeInfo(string Name, int VariablesReference);