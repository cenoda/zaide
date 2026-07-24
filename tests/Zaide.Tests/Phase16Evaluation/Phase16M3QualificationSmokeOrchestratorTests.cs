using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

/// <summary>
/// Static argv contract + behavioral reaping checks for the M3 qualification
/// smoke orchestrator. Does not launch Qwen, Node, credentials, or APIs.
/// </summary>
public sealed class Phase16M3QualificationSmokeOrchestratorTests
{
    [Fact]
    public void SmokeOrchestrator_LocksOneHundredTwentySecondWallTimeArgv()
    {
        var script = File.ReadAllText(ResolveSmokeScriptPath());

        Assert.Contains("--max-wall-time", script, StringComparison.Ordinal);
        Assert.Contains("120s", script, StringComparison.Ordinal);
        Assert.Equal("120s", Phase16M3QualificationPolicy.MaxWallTime);

        // Active wall-time argv must be 120s (exact-argv record and bwrap launch).
        Assert.Matches(
            new Regex("echo\\s+\"--max-wall-time\"\\s*\\n\\s*echo\\s+\"120s\"", RegexOptions.Multiline),
            script);
        Assert.Matches(
            new Regex(@"--max-wall-time\s+120s", RegexOptions.Multiline),
            script);

        // Do not leave a live 60s wall-time ceiling in the orchestrator.
        Assert.DoesNotMatch(
            new Regex(@"--max-wall-time\s+60s", RegexOptions.Multiline),
            script);
        Assert.DoesNotMatch(
            new Regex("echo\\s+\"60s\"", RegexOptions.Multiline),
            script);
    }

    [Fact]
    public void SmokeOrchestrator_LocksTwentyFourTurnArgv()
    {
        var script = File.ReadAllText(ResolveSmokeScriptPath());

        Assert.Contains("--max-session-turns", script, StringComparison.Ordinal);
        Assert.Contains("24", script, StringComparison.Ordinal);
        Assert.Equal(24, Phase16M3QualificationPolicy.MaxSessionTurns);

        // Active turn argv must be 24 (exact-argv record and bwrap launch).
        Assert.Matches(
            new Regex("echo\\s+\"--max-session-turns\"\\s*\\n\\s*echo\\s+\"24\"", RegexOptions.Multiline),
            script);
        Assert.Matches(
            new Regex(@"--max-session-turns\s+24", RegexOptions.Multiline),
            script);

        // Do not leave a 12-turn ceiling in the orchestrator.
        Assert.DoesNotMatch(
            new Regex(@"--max-session-turns\s+12\b", RegexOptions.Multiline),
            script);
        Assert.DoesNotMatch(
            new Regex("echo\\s+\"12\"", RegexOptions.Multiline),
            script);
    }

    [Fact]
    public void SmokeOrchestrator_DoesNotWaitInnerUnderCommandSubstitution()
    {
        var script = File.ReadAllText(ResolveSmokeScriptPath());

        // Historical defect: INNER_EC="$(wait_inner_with_reap_budget)" ran wait
        // in a subshell → bash 127 "pid is not a child of this shell".
        Assert.DoesNotContain(
            "INNER_EC=\"$(wait_inner_with_reap_budget)\"",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "INNER_EC='$(wait_inner_with_reap_budget)'",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new Regex(@"\$\(\s*wait_inner_with_reap_budget\s*\)"),
            script);

        Assert.Contains("wait_inner_with_reap_budget", script, StringComparison.Ordinal);
        Assert.Contains("INNER_WAIT_EXIT", script, StringComparison.Ordinal);
        Assert.Contains("resolve_unshare_exit_code", script, StringComparison.Ordinal);
        Assert.Contains("INNER_REAPED_EXIT", script, StringComparison.Ordinal);
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
    public void SmokeOrchestrator_RunsEgressPreflightBeforeCredentialLoad()
    {
        var script = File.ReadAllText(ResolveSmokeScriptPath());

        Assert.Contains("run_egress_preflight", script, StringComparison.Ordinal);
        var credentialLine = script.IndexOf("DEEPSEEK_API_KEY=\"$(tr -d", StringComparison.Ordinal);
        var egressLine = script.IndexOf("run_egress_preflight", StringComparison.Ordinal);
        Assert.True(egressLine >= 0 && credentialLine > egressLine);
    }

    [Fact]
    public void WaitInner_InSameShell_RecordsRealChildExitNotBash127()
    {
        // Mirrors the fixed contract: start a background job, wait in the same
        // shell (not $(...)), and record the real exit code.
        var script = """
            set -euo pipefail
            INNER_REAPED_EXIT=""
            UNSHARE_PID=""
            resolve_unshare_exit_code() {
              local wait_ec=0
              set +e
              wait "$UNSHARE_PID" 2>/dev/null
              wait_ec=$?
              set -e
              if [ "$wait_ec" -ne 127 ]; then
                echo "$wait_ec"
                return 0
              fi
              if [ -n "${INNER_REAPED_EXIT:-}" ]; then
                echo "$INNER_REAPED_EXIT"
                return 0
              fi
              echo ""
              return 0
            }
            ( exit 55 ) &
            UNSHARE_PID=$!
            # Poll until gone (same pattern as wait_inner_with_reap_budget).
            for _ in $(seq 1 50); do
              if ! kill -0 "$UNSHARE_PID" 2>/dev/null; then
                break
              fi
              sleep 0.05
            done
            code="$(resolve_unshare_exit_code)"
            # Bad pattern (subshell wait) for contrast — must not be used for INNER_EC.
            set +e
            bad="$(
              wait "$UNSHARE_PID" 2>/dev/null
              echo $?
            )"
            set -e
            printf 'good=%s\n' "$code"
            printf 'bad_subshell_wait=%s\n' "$bad"
            [ "$code" = "55" ] || exit 2
            # After the parent already reaped, a second wait in a subshell is 127.
            [ "$bad" = "127" ] || [ "$bad" = "55" ] || true
            exit 0
            """;

        var result = RunBash(script);
        Assert.True(result.ExitCode == 0, $"stdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
        Assert.Contains("good=55", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("good=127", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void ForceReapCapture_PreservesRealExitWhenSecondWaitIsNotAChild()
    {
        // force_reap reaps first and stores INNER_REAPED_EXIT; a later wait that
        // sees 127 must fall back to the captured real exit, not invent 127.
        var script = """
            set -euo pipefail
            INNER_REAPED_EXIT=""
            UNSHARE_PID=""
            force_reap_children() {
              local unshare_wait_ec=0
              set +e
              wait "$UNSHARE_PID" 2>/dev/null
              unshare_wait_ec=$?
              set -e
              if [ "$unshare_wait_ec" -ne 127 ]; then
                INNER_REAPED_EXIT="$unshare_wait_ec"
              fi
            }
            resolve_unshare_exit_code() {
              local wait_ec=0
              set +e
              wait "$UNSHARE_PID" 2>/dev/null
              wait_ec=$?
              set -e
              if [ "$wait_ec" -ne 127 ]; then
                echo "$wait_ec"
                return 0
              fi
              if [ -n "${INNER_REAPED_EXIT:-}" ]; then
                echo "$INNER_REAPED_EXIT"
                return 0
              fi
              echo ""
              return 0
            }
            ( exit 4 ) &
            UNSHARE_PID=$!
            sleep 0.1
            force_reap_children
            code="$(resolve_unshare_exit_code)"
            printf 'resolved=%s\n' "$code"
            printf 'captured=%s\n' "$INNER_REAPED_EXIT"
            [ "$code" = "4" ] || exit 2
            [ "$INNER_REAPED_EXIT" = "4" ] || exit 3
            exit 0
            """;

        var result = RunBash(script);
        Assert.True(result.ExitCode == 0, $"stdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
        Assert.Contains("resolved=4", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("captured=4", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("resolved=127", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeOrchestrator_LaunchNetnsInnerAlwaysReturnsZeroAfterPublishingExit()
    {
        // Session m3q-20260724T072341Z-8f567943: after wait, launch_netns_inner
        // did `set -e` then `return "$inner_ec"` (4). Bash deferred the function's
        // set -e until return, then aborted the whole shell despite the caller's
        // set +e — balance-after / workspace / cleanup never ran.
        var script = File.ReadAllText(ResolveSmokeScriptPath());

        Assert.Contains("Always return 0", script, StringComparison.Ordinal);
        Assert.Contains("INNER_EC=\"${INNER_WAIT_EXIT:-1}\"", script, StringComparison.Ordinal);
        Assert.Contains("EGRESS_PREFLIGHT_EC=\"${INNER_WAIT_EXIT:-1}\"", script, StringComparison.Ordinal);

        // Must not return the child exit code from launch_netns_inner.
        Assert.DoesNotContain(
            "return \"$inner_ec\"",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new Regex(
                @"launch_netns_inner\(\)\s*\{[\s\S]*?return\s+""\$inner_ec""",
                RegexOptions.Multiline),
            script);

        // Caller must not treat launch_netns_inner $? as the unshare exit.
        Assert.DoesNotContain(
            "INNER_EC=$?",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "EGRESS_PREFLIGHT_EC=$?",
            script,
            StringComparison.Ordinal);

        // Finalization markers must remain after the launch call site.
        var launchIdx = script.IndexOf(
            "launch_netns_inner \"$INNER\" \"inner_qualification\"",
            StringComparison.Ordinal);
        Assert.True(launchIdx >= 0);
        var balanceIdx = script.IndexOf("balance-after.json", launchIdx, StringComparison.Ordinal);
        var workspaceIdx = script.IndexOf("record_workspace_tc_t01_result", launchIdx, StringComparison.Ordinal);
        var cleanupIdx = script.IndexOf("cleanup.env", launchIdx, StringComparison.Ordinal);
        Assert.True(balanceIdx > launchIdx);
        Assert.True(workspaceIdx > launchIdx);
        Assert.True(cleanupIdx > launchIdx);
    }

    [Fact]
    public void LaunchNetnsInnerPattern_NonZeroChildDoesNotAbortParentFinalization()
    {
        // Behavioral regression for the sticky set -e + non-zero return defect.
        // Defective pattern (must fail): set -e inside function then return 4.
        // Fixed pattern (must pass): publish INNER_WAIT_EXIT and return 0.
        var defective = """
            set -euo pipefail
            FINALIZED=NO
            launch_netns_inner_defective() {
              set +e
              INNER_WAIT_EXIT=4
              set -e
              local inner_ec="$INNER_WAIT_EXIT"
              return "$inner_ec"
            }
            set +e
            launch_netns_inner_defective
            INNER_EC=$?
            set -e
            FINALIZED=YES
            echo "defective_finalized=$FINALIZED"
            echo "defective_inner_ec=$INNER_EC"
            """;

        var defectiveResult = RunBash(defective);
        // Parent must abort before FINALIZED=YES under the defective pattern.
        Assert.True(
            defectiveResult.ExitCode != 0,
            $"defective pattern should abort; exit={defectiveResult.ExitCode} stdout:\n{defectiveResult.Stdout}");
        Assert.DoesNotContain("defective_finalized=YES", defectiveResult.Stdout, StringComparison.Ordinal);

        var fixedScript = """
            set -euo pipefail
            FINALIZED=NO
            BALANCE_AFTER=NO
            WORKSPACE_CHECK=NO
            CLEANUP=NO
            INNER_WAIT_EXIT=""
            launch_netns_inner_fixed() {
              # Mirror fixed contract: never return non-zero after wait.
              set +e
              INNER_WAIT_EXIT=4
              local inner_ec="$INNER_WAIT_EXIT"
              # no set -e before return; always return 0
              printf 'ledger_inner=%s\n' "$inner_ec"
              return 0
            }
            set +e
            launch_netns_inner_fixed
            INNER_EC="${INNER_WAIT_EXIT:-1}"
            set -e
            FINALIZED=YES
            BALANCE_AFTER=YES
            WORKSPACE_CHECK=YES
            CLEANUP=YES
            echo "fixed_finalized=$FINALIZED"
            echo "fixed_inner_ec=$INNER_EC"
            echo "balance_after=$BALANCE_AFTER"
            echo "workspace_check=$WORKSPACE_CHECK"
            echo "cleanup=$CLEANUP"
            [ "$INNER_EC" = "4" ] || exit 2
            [ "$FINALIZED" = "YES" ] || exit 3
            exit 0
            """;

        var fixedResult = RunBash(fixedScript);
        Assert.True(
            fixedResult.ExitCode == 0,
            $"stdout:\n{fixedResult.Stdout}\nstderr:\n{fixedResult.Stderr}");
        Assert.Contains("fixed_finalized=YES", fixedResult.Stdout, StringComparison.Ordinal);
        Assert.Contains("fixed_inner_ec=4", fixedResult.Stdout, StringComparison.Ordinal);
        Assert.Contains("balance_after=YES", fixedResult.Stdout, StringComparison.Ordinal);
        Assert.Contains("workspace_check=YES", fixedResult.Stdout, StringComparison.Ordinal);
        Assert.Contains("cleanup=YES", fixedResult.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeOrchestrator_FinalizationRunsAfterQwenResultRegardlessOfExit()
    {
        // Ensure the post-launch path records the outcomes required by the
        // post-session diagnosis grant: qwen exit source, balance-after attempt,
        // workspace verification, cleanup — even when Qwen is non-zero.
        var script = File.ReadAllText(ResolveSmokeScriptPath());

        Assert.Contains("record_workspace_tc_t01_result", script, StringComparison.Ordinal);
        Assert.Contains("balance-after.json", script, StringComparison.Ordinal);
        Assert.Contains("cleanup.env", script, StringComparison.Ordinal);
        Assert.Contains("workspace-result.env", script, StringComparison.Ordinal);
        Assert.Contains("Always-run finalization path", script, StringComparison.Ordinal);

        // After qwen-result is present, candidate_execution must be marked YES
        // before finalization (including non-zero qwen_exit).
        var qwenResultCheck = script.IndexOf(
            "if [ -f \"$RUN_DIR/qwen-result.env\" ]; then",
            StringComparison.Ordinal);
        Assert.True(qwenResultCheck >= 0);
        var candidateYes = script.IndexOf(
            "CANDIDATE_EXECUTION=YES",
            qwenResultCheck,
            StringComparison.Ordinal);
        var balanceAfter = script.IndexOf("balance-after.json", qwenResultCheck, StringComparison.Ordinal);
        Assert.True(candidateYes > qwenResultCheck && candidateYes < balanceAfter);
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

    private static (int ExitCode, string Stdout, string Stderr) RunBash(string script)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/bash",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-s");

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.StandardInput.Write(script);
        process.StandardInput.Close();

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(10_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // best effort
            }

            throw new TimeoutException("bash reaping regression script exceeded 10s.");
        }

        // Ensure async readers finish.
        process.WaitForExit();
        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
