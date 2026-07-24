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
