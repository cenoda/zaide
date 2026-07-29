using System;
using System.Text;
using System.Text.RegularExpressions;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Application.Transparency.Trace;

/// <summary>
/// Fail-closed secret redaction for backend-exposed trace evidence. The
/// processor never admits the original payload into the durable store; on
/// failure it produces a bounded failure marker that the sink records with
/// <see cref="AgentTraceCaptureState.Failed"/>.
/// </summary>
internal static class AgentTraceRedactionProcessor
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

    public static AgentTraceRedactionOutcome Apply(string payload)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(payload);
            var normalized = StripUtf8Bom(payload);
            if (normalized.Length == 0)
            {
                return AgentTraceRedactionOutcome.Unchanged(normalized);
            }

            var redacted = normalized;
            AgentTraceRedactionReason? reason = null;
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
                            reason ??= new AgentTraceRedactionReason(secretClass, pattern.ToString());
                            return $"{match.Groups["label"].Value}[REDACTED:{secretClass}]";
                        });
                    continue;
                }

                redacted = pattern.Replace(
                    redacted,
                    match =>
                    {
                        replacements++;
                        reason ??= new AgentTraceRedactionReason(secretClass, pattern.ToString());
                        return $"[REDACTED:{secretClass}]";
                    });
            }

            if (replacements == 0)
            {
                return AgentTraceRedactionOutcome.Unchanged(redacted);
            }

            var state = IsFullyRedacted(redacted)
                ? AgentTraceCaptureState.Redacted
                : AgentTraceCaptureState.Redacted;

            return new AgentTraceRedactionOutcome(
                redacted,
                state,
                reason ?? new AgentTraceRedactionReason("unknown", "redaction"));
        }
        catch (Exception)
        {
            return AgentTraceRedactionOutcome.Failed();
        }
    }

    private static string StripUtf8Bom(string content)
    {
        if (content.Length >= 1 && content[0] == '﻿')
        {
            return content[1..];
        }

        if (content.StartsWith("﻿", StringComparison.Ordinal))
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
/// Redaction result for one trace payload.
/// </summary>
internal sealed class AgentTraceRedactionOutcome
{
    private AgentTraceRedactionOutcome(
        string content,
        AgentTraceCaptureState state,
        AgentTraceRedactionReason? reason,
        bool didProcessingFail)
    {
        Content = content;
        State = state;
        Reason = reason;
        DidProcessingFail = didProcessingFail;
    }

    public string Content { get; }

    public AgentTraceCaptureState State { get; }

    public AgentTraceRedactionReason? Reason { get; }

    public bool DidProcessingFail { get; }

    public int ByteCount => Encoding.UTF8.GetByteCount(Content);

    public static AgentTraceRedactionOutcome Unchanged(string content) =>
        new(content, AgentTraceCaptureState.Captured, reason: null, didProcessingFail: false);

    public static AgentTraceRedactionOutcome Failed() =>
        new(
            "{\"state\":\"failed\",\"reason\":\"redaction-processing-failed\"}",
            AgentTraceCaptureState.Failed,
            reason: null,
            didProcessingFail: true);

    public AgentTraceRedactionOutcome(
        string content,
        AgentTraceCaptureState state,
        AgentTraceRedactionReason reason)
        : this(content, state, reason, didProcessingFail: false)
    {
    }
}

/// <summary>
/// Human-readable redaction reason attached to one redacted trace payload.
/// </summary>
internal sealed class AgentTraceRedactionReason
{
    public AgentTraceRedactionReason(string secretClass, string patternId)
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
