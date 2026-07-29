namespace Zaide.Features.Agents.Domain.Transparency.Trace;

/// <summary>
/// Optional scope references carried by one trace row. Mirrors the
/// backend-neutral identifier vocabulary from M1 so cross-record queries
/// (audit, usage, recovery, memory) can correlate by run/session/conversation.
/// </summary>
internal readonly struct AgentTraceRecordScope
{
    public AgentTraceRecordScope(
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
