namespace Zaide.Features.Debugging.Infrastructure.Dap;

/// <summary>
/// Parsed first-level DAP variable entry.
/// </summary>
/// <param name="Name">Variable name.</param>
/// <param name="Value">Rendered value text from the adapter.</param>
/// <param name="Type">Optional type name from the adapter.</param>
public sealed record DapVariableInfo(string Name, string Value, string? Type);