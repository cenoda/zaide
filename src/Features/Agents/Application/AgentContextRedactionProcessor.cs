using System;
using System.Text;
using System.Text.RegularExpressions;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Fail-closed secret redaction for IDE context items.
/// </summary>
internal static class AgentContextRedactionProcessor
{
    private static readonly (string Class, Regex Pattern)[] Patterns =
    {
        ("api-key", new Regex(
            @"(?<prefix>\bsk-[A-Za-z0-9_-]+|\bghp_[A-Za-z0-9]+|\bAKIA[A-Z0-9]{16}|Bearer\s+\S+|password=\S+|secret=\S+)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("connection-string", new Regex(
            @"(ConnectionString\s*=\s*[^;\r\n]+|Server\s*=[^;\r\n]*Password\s*=[^;\r\n]+)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("private-key", new Regex(
            @"-----BEGIN[\s\S]*?PRIVATE KEY-----[\s\S]*?-----END[\s\S]*?PRIVATE KEY-----",
            RegexOptions.CultureInvariant | RegexOptions.Compiled)),
        ("hex-secret", new Regex(
            @"(?<label>\b(?:key|token|secret)\b\s*[=:]\s*)(?<value>[0-9a-fA-F]{32,})",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    };

    public static AgentContextRedactionOutcome Apply(string content)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(content);
            var normalized = StripUtf8Bom(content);
            if (normalized.Length == 0)
            {
                return AgentContextRedactionOutcome.Unchanged(normalized);
            }

            var redacted = normalized;
            AgentContextRedactionReason? reason = null;
            var replacements = 0;

            foreach (var (secretClass, pattern) in Patterns)
            {
                if (secretClass == "hex-secret")
                {
                    redacted = pattern.Replace(
                        redacted,
                        match =>
                        {
                            replacements++;
                            reason ??= new AgentContextRedactionReason(secretClass, pattern.ToString());
                            return $"{match.Groups["label"].Value}[REDACTED:{secretClass}]";
                        });
                    continue;
                }

                redacted = pattern.Replace(
                    redacted,
                    match =>
                    {
                        replacements++;
                        reason ??= new AgentContextRedactionReason(secretClass, pattern.ToString());
                        return $"[REDACTED:{secretClass}]";
                    });
            }

            if (replacements == 0)
            {
                return AgentContextRedactionOutcome.Unchanged(redacted);
            }

            var state = IsFullyRedacted(redacted)
                ? AgentContextRedactionState.Full
                : AgentContextRedactionState.Partial;

            return new AgentContextRedactionOutcome(
                redacted,
                state,
                reason ?? new AgentContextRedactionReason("unknown", "redaction"));
        }
        catch (Exception)
        {
            return AgentContextRedactionOutcome.Failed();
        }
    }

    private static string StripUtf8Bom(string content)
    {
        if (content.Length >= 1 && content[0] == '\uFEFF')
        {
            return content[1..];
        }

        if (content.StartsWith("\uFEFF", StringComparison.Ordinal))
        {
            return content[1..];
        }

        return content;
    }

    private static bool IsFullyRedacted(string content)
    {
        foreach (var segment in content.Split(
                     new[] { '\r', '\n', ' ', '\t' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (!segment.StartsWith("[REDACTED:", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return content.Length > 0;
    }
}

/// <summary>
/// Redaction result for one serialized context payload.
/// </summary>
internal sealed class AgentContextRedactionOutcome
{
    private AgentContextRedactionOutcome(
        string content,
        AgentContextRedactionState state,
        AgentContextRedactionReason? reason,
        bool didProcessingFail)
    {
        Content = content;
        State = state;
        Reason = reason;
        DidProcessingFail = didProcessingFail;
    }

    public string Content { get; }

    public AgentContextRedactionState State { get; }

    public AgentContextRedactionReason? Reason { get; }

    public bool DidProcessingFail { get; }

    public static AgentContextRedactionOutcome Unchanged(string content) =>
        new(content, AgentContextRedactionState.None, reason: null, didProcessingFail: false);

    public static AgentContextRedactionOutcome Failed() =>
        new(
            string.Empty,
            AgentContextRedactionState.ProcessingFailed,
            reason: null,
            didProcessingFail: true);

    public AgentContextRedactionOutcome(
        string content,
        AgentContextRedactionState state,
        AgentContextRedactionReason reason)
        : this(content, state, reason, didProcessingFail: false)
    {
    }
}
