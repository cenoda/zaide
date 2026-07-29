namespace Zaide.Features.Agents.Domain.Transparency.Usage;

internal readonly struct AgentUsageRecordScope
{
    public AgentUsageRecordScope(
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
