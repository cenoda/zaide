namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Optional scope references carried by a durable record envelope.
/// </summary>
internal readonly struct AgentDurableRecordScopeReferences
{
    public AgentDurableRecordScopeReferences(
        string? conversationId = null,
        string? sessionId = null,
        string? runId = null,
        string? backendId = null)
    {
        ConversationId = conversationId;
        SessionId = sessionId;
        RunId = runId;
        BackendId = backendId;
    }

    public string? ConversationId { get; }

    public string? SessionId { get; }

    public string? RunId { get; }

    public string? BackendId { get; }
}
