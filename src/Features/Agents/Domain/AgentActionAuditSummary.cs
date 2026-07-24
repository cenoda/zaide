using System;
using System.Linq;
using System.Text;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Bounded, redacted audit summary text for one action fact.
/// </summary>
internal sealed class AgentActionAuditSummary
{
    public AgentActionAuditSummary(string text, bool wasTruncated = false, bool wasRedacted = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        var redactedText = RedactSecrets(text, out var redacted);
        var bounded = BoundText(redactedText, out var truncated);
        Text = bounded;
        WasTruncated = wasTruncated || truncated;
        WasRedacted = wasRedacted || redacted;
    }

    public string Text { get; }

    public bool WasTruncated { get; }

    public bool WasRedacted { get; }

    public static AgentActionAuditSummary FromParts(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var joined = string.Join("; ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
        return new AgentActionAuditSummary(joined);
    }

    private static string RedactSecrets(string text, out bool wasRedacted)
    {
        wasRedacted = text.Contains("api_key=", StringComparison.OrdinalIgnoreCase)
            || text.Contains("password=", StringComparison.OrdinalIgnoreCase)
            || text.Contains("token=", StringComparison.OrdinalIgnoreCase);

        if (wasRedacted)
        {
            return "[redacted]";
        }

        return text;
    }

    private static string BoundText(string text, out bool truncated)
    {
        truncated = false;
        if (AgentActionBudgets.GetUtf8ByteCount(text) <= AgentActionBudgets.StoredAuditSummaryMaxBytes)
        {
            return text;
        }

        truncated = true;
        var encoding = Encoding.UTF8;
        var builder = new StringBuilder();
        var usedBytes = 0;
        foreach (var character in text)
        {
            var charBytes = encoding.GetByteCount(new[] { character });
            if (usedBytes + charBytes > AgentActionBudgets.StoredAuditSummaryMaxBytes)
            {
                break;
            }

            builder.Append(character);
            usedBytes += charBytes;
        }

        return builder.ToString();
    }
}
