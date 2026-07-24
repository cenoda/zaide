using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Run-scoped correlation-key idempotency keyed by exact request fingerprint.
/// </summary>
internal sealed class AgentActionCorrelationRegistry
{
    private readonly Dictionary<CorrelationRecordKey, AgentActionResult> _terminalResults = new();

    public bool TryGetTerminalResult(
        AgentActionCorrelationKey correlationKey,
        AgentActionRequestFingerprint fingerprint,
        out AgentActionResult? terminalResult)
    {
        if (correlationKey == default)
        {
            throw new ArgumentException("Correlation key is required.", nameof(correlationKey));
        }

        if (fingerprint == default)
        {
            throw new ArgumentException("Request fingerprint is required.", nameof(fingerprint));
        }

        return _terminalResults.TryGetValue(
            new CorrelationRecordKey(correlationKey, fingerprint),
            out terminalResult);
    }

    public void RecordTerminalResult(
        AgentActionCorrelationKey correlationKey,
        AgentActionRequestFingerprint fingerprint,
        AgentActionResult terminalResult)
    {
        ArgumentNullException.ThrowIfNull(terminalResult);
        if (!terminalResult.IsTerminal)
        {
            throw new ArgumentException("Only terminal results may be recorded.", nameof(terminalResult));
        }

        _terminalResults[new CorrelationRecordKey(correlationKey, fingerprint)] = terminalResult;
    }

    public bool TryRejectMismatchedFingerprint(
        AgentActionCorrelationKey correlationKey,
        AgentActionRequestFingerprint fingerprint,
        out AgentActionResult? rejection)
    {
        foreach (var entry in _terminalResults)
        {
            if (entry.Key.CorrelationKey != correlationKey)
            {
                continue;
            }

            if (entry.Key.Fingerprint == fingerprint)
            {
                continue;
            }

            rejection = new AgentActionResult(
                entry.Value.ActionId,
                entry.Value.AttemptId,
                AgentActionResultKind.Denied,
                AgentActionFailureKind.CorrelationKeyMismatch,
                "Correlation key was reused with a different request fingerprint.");
            return true;
        }

        rejection = null;
        return false;
    }

    private readonly record struct CorrelationRecordKey(
        AgentActionCorrelationKey CorrelationKey,
        AgentActionRequestFingerprint Fingerprint);
}
