using System;
using System.Collections.Generic;
using System.Threading;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Run-scoped correlation-key idempotency keyed by exact request fingerprint.
/// </summary>
internal sealed class AgentActionCorrelationRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<CorrelationRecordKey, AgentActionResult> _terminalResults = new();
    private readonly Dictionary<AgentActionCorrelationKey, AgentActionRequestFingerprint> _inFlightFingerprints = new();
    private volatile bool _revoked;

    /// <summary>
    /// Minimum polling interval used by cancellation-aware waits.
    /// Balances responsiveness against lock contention.
    /// </summary>
    private static readonly TimeSpan WaitPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Signals all waiting threads that the registry has been revoked and
    /// they should stop waiting.
    /// </summary>
    public void Revoke()
    {
        _revoked = true;
        lock (_gate)
        {
            Monitor.PulseAll(_gate);
        }
    }

    public bool IsRevoked => _revoked;

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

        lock (_gate)
        {
            return _terminalResults.TryGetValue(
                new CorrelationRecordKey(correlationKey, fingerprint),
                out terminalResult);
        }
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

        lock (_gate)
        {
            _terminalResults[new CorrelationRecordKey(correlationKey, fingerprint)] = terminalResult;
            _inFlightFingerprints.Remove(correlationKey);
            Monitor.PulseAll(_gate);
        }
    }

    public bool TryRejectMismatchedFingerprint(
        AgentActionCorrelationKey correlationKey,
        AgentActionRequestFingerprint fingerprint,
        out AgentActionResult? rejection)
    {
        lock (_gate)
        {
            if (TryRejectMismatchedTerminalFingerprint(correlationKey, fingerprint, out rejection))
            {
                return true;
            }

            if (_inFlightFingerprints.TryGetValue(correlationKey, out var inFlightFingerprint)
                && inFlightFingerprint != fingerprint)
            {
                rejection = CreateCorrelationKeyMismatchResult();
                return true;
            }
        }

        rejection = null;
        return false;
    }

    /// <summary>
    /// Waits for an in-flight correlation to complete, with cancellation and
    /// revocation awareness.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Monitor.Wait(object, int)"/> with a bounded polling
    /// interval instead of an unbounded <see cref="Monitor.Wait(object)"/>.
    /// This ensures the calling thread can observe cancellation, revocation,
    /// and timeout without risking indefinite blocking when a processing
    /// thread fails to call <see cref="Monitor.PulseAll"/>.
    /// </remarks>
    public bool TryWaitForInFlightReplay(
        AgentActionCorrelationKey correlationKey,
        AgentActionRequestFingerprint fingerprint,
        CancellationToken cancellationToken,
        out AgentActionResult? replay)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            replay = null;
            return false;
        }

        lock (_gate)
        {
            while (_inFlightFingerprints.TryGetValue(correlationKey, out var inFlightFingerprint))
            {
                if (inFlightFingerprint != fingerprint)
                {
                    replay = CreateCorrelationKeyMismatchResult();
                    return true;
                }

                if (_terminalResults.TryGetValue(
                        new CorrelationRecordKey(correlationKey, fingerprint),
                        out var terminalResult))
                {
                    replay = terminalResult;
                    return true;
                }

                if (_revoked)
                {
                    replay = null;
                    return false;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    replay = null;
                    return false;
                }

                // Bounded wait: sleeps at most WaitPollInterval, then
                // re-evaluates the loop condition. This replaces the
                // unbounded Monitor.Wait(_gate) call.
                var waitSatisfied = Monitor.Wait(_gate, WaitPollInterval);
                if (!waitSatisfied)
                {
                    // Timed out — loop re-evaluates revocation, cancellation,
                    // and terminal result conditions.
                }
            }
        }

        replay = null;
        return false;
    }

    public void BeginInFlightCorrelation(
        AgentActionCorrelationKey correlationKey,
        AgentActionRequestFingerprint fingerprint)
    {
        lock (_gate)
        {
            _inFlightFingerprints[correlationKey] = fingerprint;
        }
    }

    public void ClearInFlightCorrelation(AgentActionCorrelationKey correlationKey)
    {
        lock (_gate)
        {
            _inFlightFingerprints.Remove(correlationKey);
            Monitor.PulseAll(_gate);
        }
    }

    private bool TryRejectMismatchedTerminalFingerprint(
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

            rejection = CreateCorrelationKeyMismatchResult(entry.Value);
            return true;
        }

        rejection = null;
        return false;
    }

    private static AgentActionResult CreateCorrelationKeyMismatchResult(AgentActionResult? source = null) =>
        new(
            source?.ActionId ?? AgentActionId.New(),
            source?.AttemptId ?? AgentActionAttemptId.New(),
            AgentActionResultKind.Denied,
            AgentActionFailureKind.CorrelationKeyMismatch,
            "Correlation key was reused with a different request fingerprint.");

    private readonly record struct CorrelationRecordKey(
        AgentActionCorrelationKey CorrelationKey,
        AgentActionRequestFingerprint Fingerprint);
}
