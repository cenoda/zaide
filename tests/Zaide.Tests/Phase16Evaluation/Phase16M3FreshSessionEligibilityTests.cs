using System;
using System.IO;
using System.Text.RegularExpressions;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16M3FreshSessionEligibilityTests
{
    [Fact]
    public void EvaluatePreflight_AllowsFreshGrantDespiteHistoricalNoGoHint()
    {
        var hints = new[]
        {
            Phase16M3FreshSessionPolicy.CreateInformativeHint(
                "m3q-20260724T060109Z-45dd1c5f",
                "historical NO-GO qwen_exit=53 under then-locked 12-turn ceiling"),
        };

        var preflight = Phase16M3FreshSessionPolicy.EvaluatePreflight(
            qualificationGrantActive: true,
            lockedMaxSessionTurns: Phase16M3QualificationPolicy.MaxSessionTurns,
            historicalHints: hints);

        Assert.True(preflight.IsAllowed);
        Assert.Null(preflight.BlockReason);
        Assert.Single(preflight.InformativeHints);
        Assert.True(preflight.InformativeHints[0].IsInformativeOnly);
    }

    [Fact]
    public void EvaluatePreflight_BlocksWithoutQualificationGrant()
    {
        var preflight = Phase16M3FreshSessionPolicy.EvaluatePreflight(
            qualificationGrantActive: false,
            lockedMaxSessionTurns: Phase16M3QualificationPolicy.MaxSessionTurns);

        Assert.False(preflight.IsAllowed);
        Assert.Contains("PHASE16_M3_QUALIFICATION_GRANT", preflight.BlockReason, StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluatePreflight_BlocksLockedTurnMismatch()
    {
        var preflight = Phase16M3FreshSessionPolicy.EvaluatePreflight(
            qualificationGrantActive: true,
            lockedMaxSessionTurns: 12);

        Assert.False(preflight.IsAllowed);
        Assert.Contains("locked max session turns mismatch", preflight.BlockReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCurrentSessionEvidence_RejectsHistoricalQwenExitSubstitution()
    {
        var evidence = new M3QualificationSessionEvidence(
            SessionId: "m3q-20260724T063149Z-example01",
            KeyConsumed: true,
            ProviderLaunchAttempted: true,
            CandidateExecution: false,
            QwenExit: 53,
            QwenExitSource: "historical_session",
            ProviderExecutionLabel: null);

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16M3FreshSessionPolicy.ValidateCurrentSessionEvidenceOrThrow(evidence));
        Assert.Contains("historical session", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCurrentSessionEvidence_RequiresNoProviderExecutionLabelWhenKeyConsumedWithoutLaunch()
    {
        var evidence = new M3QualificationSessionEvidence(
            SessionId: "m3q-20260724T063149Z-example01",
            KeyConsumed: true,
            ProviderLaunchAttempted: false,
            CandidateExecution: false,
            QwenExit: null,
            QwenExitSource: "none",
            ProviderExecutionLabel: Phase16M3FreshSessionPolicy.NoProviderExecutionLabel);

        Phase16M3FreshSessionPolicy.ValidateCurrentSessionEvidenceOrThrow(evidence);
    }

    [Fact]
    public void ValidateCurrentSessionEvidence_RejectsQwenExitWhenKeyConsumedWithoutLaunch()
    {
        var evidence = new M3QualificationSessionEvidence(
            SessionId: "m3q-20260724T063149Z-example01",
            KeyConsumed: true,
            ProviderLaunchAttempted: false,
            CandidateExecution: false,
            QwenExit: 53,
            QwenExitSource: "none",
            ProviderExecutionLabel: Phase16M3FreshSessionPolicy.NoProviderExecutionLabel);

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16M3FreshSessionPolicy.ValidateCurrentSessionEvidenceOrThrow(evidence));
        Assert.Contains("must not record a Qwen exit", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeOrchestrator_DefersCredentialLoadUntilAfterEgressPreflight()
    {
        var script = File.ReadAllText(ResolveSmokeScriptPath());

        var credentialLine = script.IndexOf("DEEPSEEK_API_KEY=\"$(tr -d", StringComparison.Ordinal);
        var egressPreflightLine = script.IndexOf("run_egress_preflight", StringComparison.Ordinal);

        Assert.True(credentialLine >= 0, "Expected one-shot credential load in orchestrator.");
        Assert.True(egressPreflightLine >= 0, "Expected egress preflight helper in orchestrator.");
        Assert.True(
            egressPreflightLine < credentialLine,
            "Credential load must occur only after egress preflight succeeds.");
    }

    [Fact]
    public void SmokeOrchestrator_RecordsNoProviderExecutionLabelOnPostCredentialAbort()
    {
        var script = File.ReadAllText(ResolveSmokeScriptPath());

        Assert.Contains(Phase16M3FreshSessionPolicy.NoProviderExecutionLabel, script, StringComparison.Ordinal);
        Assert.Contains("stop_with_no_provider_execution", script, StringComparison.Ordinal);
        Assert.Contains("provider_launch_attempted=NO", script, StringComparison.Ordinal);
        Assert.Contains("qwen_exit_source=none", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeOrchestrator_DoesNotReadHistoricalQwenResultFromOtherSessions()
    {
        var script = File.ReadAllText(ResolveSmokeScriptPath());

        Assert.Contains("resolve_current_session_qwen_exit", script, StringComparison.Ordinal);
        Assert.Contains("rm -f \"$RUN_DIR/qwen-result.env\"", script, StringComparison.Ordinal);
        Assert.Contains("prior_session_verdict_reused=NO", script, StringComparison.Ordinal);
        Assert.DoesNotContain("m3q-20260724T060109Z-45dd1c5f", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCurrentSessionEvidence_AcceptsCurrentRunQwenExit()
    {
        var evidence = new M3QualificationSessionEvidence(
            SessionId: "m3q-20260724T150000Z-abcd1234",
            KeyConsumed: true,
            ProviderLaunchAttempted: true,
            CandidateExecution: true,
            QwenExit: 0,
            QwenExitSource: "current_run_dir",
            ProviderExecutionLabel: null);

        Phase16M3FreshSessionPolicy.ValidateCurrentSessionEvidenceOrThrow(evidence);
    }

    [Fact]
    public void SmokeOrchestrator_GeneratesFreshSessionIdPerRun()
    {
        var script = File.ReadAllText(ResolveSmokeScriptPath());

        Assert.Matches(
            new Regex("SESSION_ID=\"m3q-\\$\\(date -u \\+%Y%m%dT%H%M%SZ\\)-\\$\\(openssl rand -hex 4\\)\""),
            script);
        Assert.Contains("SESSION_ROOT=\"$PHASE_ROOT/records/m3-qualification/$SESSION_ID\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SESSION_ID=m3q-20260724T060109Z-45dd1c5f", script, StringComparison.Ordinal);
    }

    private static string ResolveSmokeScriptPath()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "Phase16NativeHarnessEvaluation",
            "Scripts",
            "m3-qualification-smoke.sh");
        Assert.True(File.Exists(path), $"Expected smoke orchestrator at {path}");
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Zaide.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (Zaide.slnx).");
    }
}
