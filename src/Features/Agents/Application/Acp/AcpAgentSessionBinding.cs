using System;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Zaide-owned binding between one Agent Session and one ACP runtime session.
/// </summary>
internal sealed class AcpAgentSessionBinding
{
    public AcpAgentSessionBinding(
        AgentSessionId sessionId,
        string expectedAgentName,
        string expectedAgentVersion)
    {
        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(expectedAgentName))
        {
            throw new ArgumentException("Expected agent name is required.", nameof(expectedAgentName));
        }

        if (string.IsNullOrWhiteSpace(expectedAgentVersion))
        {
            throw new ArgumentException("Expected agent version is required.", nameof(expectedAgentVersion));
        }

        SessionId = sessionId;
        ExpectedAgentName = expectedAgentName;
        ExpectedAgentVersion = expectedAgentVersion;
    }

    public AgentSessionId SessionId { get; }

    public string ExpectedAgentName { get; }

    public string ExpectedAgentVersion { get; }

    public string? AcpSessionId { get; set; }

    public bool IsBoundToAcpSession => !string.IsNullOrWhiteSpace(AcpSessionId);
}
