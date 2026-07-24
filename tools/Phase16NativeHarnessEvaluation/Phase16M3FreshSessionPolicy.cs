namespace Phase16NativeHarnessEvaluation;

/// <summary>
/// Fresh-session eligibility for M3 qualification smoke. Each explicit grant
/// must create and evaluate a new session ID. Historical session records may
/// inform preflight requirements but must never substitute for the current
/// session's provider exit or task result.
/// </summary>
public static class Phase16M3FreshSessionPolicy
{
    public const string NoProviderExecutionLabel = "no candidate launch / no provider execution";

    public const string ProviderLaunchAttemptedField = "provider_launch_attempted";
    public const string CandidateExecutionField = "candidate_execution";
    public const string QwenExitSourceField = "qwen_exit_source";
    public const string ProviderExecutionLabelField = "provider_execution_label";

    public static readonly string[] RequiredPreflightTools =
    [
        "bwrap",
        "slirp4netns",
        "nft",
        "curl",
        "unshare",
        "getent",
        "dotnet",
    ];

    public static FreshSessionPreflight EvaluatePreflight(
        bool qualificationGrantActive,
        int lockedMaxSessionTurns,
        IReadOnlyList<HistoricalSessionHint>? historicalHints = null)
    {
        if (!qualificationGrantActive)
        {
            return FreshSessionPreflight.Blocked(
                "qualification grant env PHASE16_M3_QUALIFICATION_GRANT=1 not set");
        }

        if (lockedMaxSessionTurns != Phase16M3QualificationPolicy.MaxSessionTurns)
        {
            return FreshSessionPreflight.Blocked(
                $"locked max session turns mismatch: expected {Phase16M3QualificationPolicy.MaxSessionTurns}");
        }

        var hints = historicalHints ?? Array.Empty<HistoricalSessionHint>();
        var informativeHints = hints
            .Where(static hint => hint.IsInformativeOnly)
            .ToArray();

        // A prior NO-GO (including turn-limit exit 53 under a historical ceiling)
        // must not block a fresh eligible grant.
        return FreshSessionPreflight.Allowed(informativeHints);
    }

    public static void ValidateCurrentSessionEvidenceOrThrow(M3QualificationSessionEvidence evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence.SessionId);

        if (evidence.KeyConsumed && !evidence.ProviderLaunchAttempted)
        {
            if (evidence.QwenExit.HasValue)
            {
                throw new ManifestValidationException(
                    "Consumed key without provider launch must not record a Qwen exit code.");
            }

            if (string.Equals(
                    evidence.QwenExitSource,
                    "historical_session",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ManifestValidationException(
                    "Historical session Qwen exit must not substitute for a fresh session verdict.");
            }

            if (!string.Equals(
                    evidence.ProviderExecutionLabel,
                    NoProviderExecutionLabel,
                    StringComparison.Ordinal))
            {
                throw new ManifestValidationException(
                    $"Consumed key without provider launch must record '{NoProviderExecutionLabel}'.");
            }

            return;
        }

        if (evidence.ProviderLaunchAttempted
            && evidence.QwenExit.HasValue
            && string.Equals(evidence.QwenExitSource, "historical_session", StringComparison.OrdinalIgnoreCase))
        {
            throw new ManifestValidationException(
                "Provider launch attempted but Qwen exit was sourced from a historical session.");
        }

        if (evidence.ProviderLaunchAttempted
            && !evidence.CandidateExecution
            && evidence.QwenExit.HasValue)
        {
            throw new ManifestValidationException(
                "Qwen exit recorded without candidate execution in the current session.");
        }
    }

    public static bool IsInformativeHistoricalHint(HistoricalSessionHint hint)
    {
        return hint.IsInformativeOnly;
    }

    public static HistoricalSessionHint CreateInformativeHint(
        string sessionId,
        string note)
    {
        return new HistoricalSessionHint(sessionId, note, IsInformativeOnly: true);
    }
}

public readonly record struct FreshSessionPreflight(
    bool IsAllowed,
    string? BlockReason,
    IReadOnlyList<HistoricalSessionHint> InformativeHints)
{
    public static FreshSessionPreflight Allowed(IReadOnlyList<HistoricalSessionHint> hints)
    {
        return new FreshSessionPreflight(true, null, hints);
    }

    public static FreshSessionPreflight Blocked(string reason)
    {
        return new FreshSessionPreflight(false, reason, Array.Empty<HistoricalSessionHint>());
    }
}

public readonly record struct HistoricalSessionHint(
    string SessionId,
    string Note,
    bool IsInformativeOnly);

public readonly record struct M3QualificationSessionEvidence(
    string SessionId,
    bool KeyConsumed,
    bool ProviderLaunchAttempted,
    bool CandidateExecution,
    int? QwenExit,
    string? QwenExitSource,
    string? ProviderExecutionLabel);
