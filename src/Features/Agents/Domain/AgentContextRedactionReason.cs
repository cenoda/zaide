using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Human-readable redaction reason attached to one context item.
/// </summary>
internal sealed class AgentContextRedactionReason
{
    public AgentContextRedactionReason(string secretClass, string patternId)
    {
        if (string.IsNullOrWhiteSpace(secretClass))
        {
            throw new ArgumentException("Secret class is required.", nameof(secretClass));
        }

        if (string.IsNullOrWhiteSpace(patternId))
        {
            throw new ArgumentException("Pattern id is required.", nameof(patternId));
        }

        SecretClass = secretClass;
        PatternId = patternId;
    }

    public string SecretClass { get; }

    public string PatternId { get; }
}
