namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Redaction outcome for one context item. Processing failures are fail-closed:
/// the item must be dropped rather than passed through unredacted.
/// </summary>
internal enum AgentContextRedactionState
{
    None,
    Partial,
    Full,
    ProcessingFailed,
}
