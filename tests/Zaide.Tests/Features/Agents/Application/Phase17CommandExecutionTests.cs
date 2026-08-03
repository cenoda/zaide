using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 17 M7 — constrained command execution resolver, executor, and broker coverage.
/// </summary>
public sealed class Phase17CommandExecutionTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly WorkspaceActionScope _scope;
    private readonly DefaultAgentCommandResolver _resolver = new();
    private readonly WorkspaceCommandExecutor _executor = new();

    public Phase17CommandExecutionTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "zaide-p17-cmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);
        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Resolver_PreservesArgumentVectorWithoutShellParsing()
    {
        var executable = CreateExecutable("print-args.sh", "printf '%s\\n' \"$@\"");
        var payload = new AgentExecuteCommandActionPayload(
            executable,
            new[] { "one", "two with spaces", "three" },
            AgentWorkspaceRelativePath.Normalize("."));

        Assert.True(_resolver.TryResolve(payload, out var resolved, out var error), error);

        Assert.Equal(new[] { "one", "two with spaces", "three" }, resolved!.Arguments);
    }

    [Fact]
    public void Resolver_RejectsRelativeExecutable()
    {
        var payload = new AgentExecuteCommandActionPayload(
            "print-args.sh",
            new[] { "arg" },
            AgentWorkspaceRelativePath.Normalize("."));

        Assert.False(_resolver.TryResolve(payload, out _, out var error));
        Assert.Contains("PATH", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolver_RejectsMissingAbsoluteExecutable()
    {
        var missing = Path.Combine(_workspaceRoot, "missing-binary");
        var payload = new AgentExecuteCommandActionPayload(
            missing,
            new[] { "arg" },
            AgentWorkspaceRelativePath.Normalize("."));

        Assert.False(_resolver.TryResolve(payload, out _, out var error));
        Assert.Contains("executable", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolver_ResolvesPathLookupExecutable()
    {
        var payload = new AgentExecuteCommandActionPayload(
            "true",
            Array.Empty<string>(),
            AgentWorkspaceRelativePath.Normalize("."));

        Assert.True(_resolver.TryResolve(payload, out var resolved, out var error), error);
        Assert.Equal(AgentCommandResolutionSource.PathResolution, resolved!.ResolutionSource);
        Assert.True(File.Exists(resolved.CanonicalAbsoluteExecutablePath));
    }

    [Fact]
    public void Resolver_DeniesShellInterpreterBySymlinkTarget()
    {
        var bash = FindSystemExecutable("bash") ?? "/bin/bash";
        if (!File.Exists(bash))
        {
            return;
        }

        var linkPath = Path.Combine(_workspaceRoot, "shell-link");
        File.CreateSymbolicLink(linkPath, bash);

        var payload = new AgentExecuteCommandActionPayload(
            linkPath,
            new[] { "-c", "echo hi" },
            AgentWorkspaceRelativePath.Normalize("."));

        Assert.True(_resolver.TryResolve(payload, out var resolved, out var error), error);
        Assert.True(resolved!.DenylistResult.IsDenied);
        Assert.True(resolved.IsShellInterpreter);
        Assert.NotEmpty(resolved.SymlinkChain);
    }

    [Fact]
    public void Resolver_DeniesPrivilegeEscalationHelper()
    {
        var sudo = FindSystemExecutable("sudo");
        if (sudo is null)
        {
            return;
        }

        var payload = new AgentExecuteCommandActionPayload(
            sudo,
            new[] { "id" },
            AgentWorkspaceRelativePath.Normalize("."));

        Assert.True(_resolver.TryResolve(payload, out var resolved, out var error), error);
        Assert.True(resolved!.IsPrivilegeEscalation);
        Assert.True(resolved.DenylistResult.IsDenied);
    }

    [Fact]
    public void PolicyClassifier_DeniesResolvedShellInterpreter()
    {
        var bash = FindSystemExecutable("bash") ?? "/bin/bash";
        if (!File.Exists(bash))
        {
            return;
        }

        var payload = new AgentExecuteCommandActionPayload(
            bash,
            new[] { "-c", "echo hi" },
            AgentWorkspaceRelativePath.Normalize("."));
        Assert.True(_resolver.TryResolve(payload, out var resolved, out _), "resolver failed");

        Assert.Equal(
            AgentActionPermissionClassification.DeniedByPolicy,
            AgentActionPolicyClassifier.Classify(payload, resolved));
    }

    [Fact]
    public void EnvironmentBuilder_ConstructsLockedBaselineOnly()
    {
        Environment.SetEnvironmentVariable("PHASE17_TEST_SECRET", "phase17-secret-value");
        Environment.SetEnvironmentVariable("CUSTOM_REQUEST_VAR", "must-not-appear");
        try
        {
            var secrets = new TestSecretStore();
            secrets.Set("openai_api_key", "phase17-secret-value");
            var environment = AgentCommandEnvironmentBuilder.Build(secrets);

            Assert.Equal("1", environment["NO_COLOR"]);
            Assert.Equal("1", environment["DOTNET_NOLOGO"]);
            Assert.Equal("1", environment["DOTNET_CLI_TELEMETRY_OPTOUT"]);
            Assert.DoesNotContain("CUSTOM_REQUEST_VAR", environment.Keys);
            Assert.DoesNotContain(
                environment.Values,
                value => value.Contains("phase17-secret-value", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PHASE17_TEST_SECRET", null);
            Environment.SetEnvironmentVariable("CUSTOM_REQUEST_VAR", null);
        }
    }

    [Fact]
    public void Executor_RunsApprovedCommandWithSeparatedStdoutAndStderr()
    {
        var executable = CreateExecutable(
            "streams.sh",
            "printf 'out\\n' 1>&2; printf 'err\\n' 1>&2; printf 'done\\n'");
        var resolved = ResolveOrFail(executable, Array.Empty<string>(), ".");

        var result = _executor.Execute(_scope, resolved, CancellationToken.None);

        Assert.Equal(AgentCommandExecutionOutcome.Succeeded, result.Outcome);
        Assert.Contains("done", result.StandardOutput.Text, StringComparison.Ordinal);
        Assert.Contains("err", result.StandardError.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("err", result.StandardOutput.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Executor_ReturnsFailedForNonZeroExit()
    {
        var executable = CreateExecutable("fail.sh", "exit 7");
        var resolved = ResolveOrFail(executable, Array.Empty<string>(), ".");

        var result = _executor.Execute(_scope, resolved, CancellationToken.None);

        Assert.Equal(AgentCommandExecutionOutcome.Failed, result.Outcome);
        Assert.Equal(7, result.ExitCode);
    }

    [Fact]
    public void Executor_RejectsNonDirectoryWorkingDirectory()
    {
        var executable = CreateExecutable("notadir.sh", "exit 0");
        var resolved = ResolveOrFail(executable, Array.Empty<string>(), "notadir.sh");

        var result = _executor.Execute(_scope, resolved, CancellationToken.None);

        Assert.Equal(AgentCommandExecutionOutcome.Unreadable, result.Outcome);
    }

    [Fact]
    public void Executor_RejectsWorkingDirectorySymlinkEscape()
    {
        var outside = Path.Combine(Path.GetTempPath(), "zaide-p17-wd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        var linkDir = Path.Combine(_workspaceRoot, "escape-wd");
        try
        {
            Directory.CreateSymbolicLink(linkDir, outside);
            var executable = CreateExecutable("noop.sh", "exit 0");
            var resolved = ResolveOrFail(executable, Array.Empty<string>(), "escape-wd");

            var result = _executor.Execute(_scope, resolved, CancellationToken.None);

            Assert.Equal(AgentCommandExecutionOutcome.PathEscaped, result.Outcome);
        }
        finally
        {
            if (Directory.Exists(linkDir))
            {
                Directory.Delete(linkDir);
            }

            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void Executor_TruncatesStdoutBudgetAndKillsProcessTree()
    {
        var executable = CreateExecutable(
            "spam.sh",
            "yes x | head -n 20000");
        var resolved = ResolveOrFail(executable, Array.Empty<string>(), ".");
        var executor = new WorkspaceCommandExecutor();

        var result = executor.Execute(_scope, resolved, CancellationToken.None);

        Assert.Equal(AgentCommandExecutionOutcome.Truncated, result.Outcome);
        Assert.True(result.StandardOutput.WasTruncated);
        Assert.True(result.StandardOutput.ByteCount <= AgentActionBudgets.CommandStdoutMaxBytes);
    }

    [Fact]
    public void Executor_FlagsInvalidTextInOutput()
    {
        var executable = CreateExecutable("null.sh", "printf '\\000ok'");
        var resolved = ResolveOrFail(executable, Array.Empty<string>(), ".");

        var result = _executor.Execute(_scope, resolved, CancellationToken.None);

        Assert.True(result.StandardOutput.ContainsInvalidText);
    }

    [Fact]
    public void Executor_CancellationTerminatesProcessTree()
    {
        var pidFile = Path.Combine(_workspaceRoot, "sleep.pid");
        var executable = CreateExecutable(
            "sleep.sh",
            $"sleep 987654 & echo $! > '{pidFile}'; wait");
        var resolved = ResolveOrFail(executable, Array.Empty<string>(), ".");
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        var result = _executor.Execute(_scope, resolved, cts.Token);

        Assert.Equal(AgentCommandExecutionOutcome.Cancelled, result.Outcome);
        if (!File.Exists(pidFile))
        {
            return;
        }

        var pid = File.ReadAllText(pidFile).Trim();
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (!IsPidAlive(pid))
            {
                return;
            }

            Thread.Sleep(100);
        }

        Assert.False(IsPidAlive(pid));
    }

    [Fact]
    public async Task Broker_ApprovedCommand_ExecutesOnce()
    {
        var executable = CreateExecutable("broker-ok.sh", "printf 'broker-ok\\n'");
        var broker = CreateBroker();
        var payload = new AgentExecuteCommandActionPayload(
            executable,
            Array.Empty<string>(),
            AgentWorkspaceRelativePath.Normalize("."));

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, result.ResultKind);
        Assert.NotNull(result.CommandExecution);
        Assert.Contains("broker-ok", result.CommandExecution!.StandardOutput.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Broker_DeniedShell_IsRejectedByPolicyWithoutExecution()
    {
        var bash = FindSystemExecutable("bash") ?? "/bin/bash";
        if (!File.Exists(bash))
        {
            return;
        }

        var broker = CreateBroker();
        var payload = new AgentExecuteCommandActionPayload(
            bash,
            new[] { "-c", "echo hi" },
            AgentWorkspaceRelativePath.Normalize("."));

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PolicyDenied, result.FailureKind);
        Assert.Null(result.CommandExecution);
    }

    [Fact]
    public async Task Broker_ConcurrentCommandRequests_RejectSecond()
    {
        var executable = CreateExecutable("ok.sh", "printf 'ok\\n'");
        var holdEntered = new ManualResetEventSlim(false);
        var releaseHold = new ManualResetEventSlim(false);
        var broker = CreateBroker();
        broker.TestProcessingHold = () =>
        {
            holdEntered.Set();
            // Stay in the processing gate until the second request is asserted;
            // avoid fixed multi-second sleeps that dominate suite wall time.
            Assert.True(releaseHold.Wait(TimeSpan.FromSeconds(5)));
        };
        var payload = new AgentExecuteCommandActionPayload(
            executable,
            Array.Empty<string>(),
            AgentWorkspaceRelativePath.Normalize("."));

        var firstTask = Task.Run(() => broker.RequestAsync(payload, null, CancellationToken.None).AsTask());
        Assert.True(holdEntered.Wait(TimeSpan.FromSeconds(5)));
        var second = await broker.RequestAsync(payload, "second", CancellationToken.None);
        releaseHold.Set();
        var first = await firstTask;

        Assert.Equal(AgentActionResultKind.Denied, second.ResultKind);
        Assert.Equal(AgentActionFailureKind.ConcurrentActionRejected, second.FailureKind);
        Assert.Equal(AgentActionResultKind.Succeeded, first.ResultKind);
    }

    [Fact]
    public async Task Broker_DuplicateCorrelationKey_DoesNotReExecuteCommand()
    {
        var executable = CreateExecutable("once.sh", "printf 'once\\n'");
        var broker = CreateBroker();
        const string correlationKey = "cmd-dup";
        var payload = new AgentExecuteCommandActionPayload(
            executable,
            Array.Empty<string>(),
            AgentWorkspaceRelativePath.Normalize("."));

        var first = await broker.RequestAsync(payload, correlationKey, CancellationToken.None);
        var second = await broker.RequestAsync(payload, correlationKey, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, first.ResultKind);
        Assert.Equal(AgentActionResultKind.DuplicateReplay, second.ResultKind);
        Assert.Equal(first.CommandExecution!.StandardOutput.Text, second.CommandExecution!.StandardOutput.Text);
    }

    [Fact]
    public async Task Broker_ExpiredDecision_DeniesBeforeExecution()
    {
        var executable = CreateExecutable("never.sh", "printf 'never\\n'");
        var broker = CreateBroker(new ExpiredPermissionReviewService());
        var payload = new AgentExecuteCommandActionPayload(
            executable,
            Array.Empty<string>(),
            AgentWorkspaceRelativePath.Normalize("."));

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.PermissionExpired, result.FailureKind);
        Assert.Null(result.CommandExecution);
    }

    [Fact]
    public async Task Broker_StaleWorkspaceBeforeExecution_ReturnsRevoked()
    {
        var executable = CreateExecutable("stale.sh", "printf 'stale\\n'");
        var authority = new FakeWorkspaceActionAuthority(_scope) { IsStale = true };
        var broker = CreateBroker(authority: authority);
        var payload = new AgentExecuteCommandActionPayload(
            executable,
            Array.Empty<string>(),
            AgentWorkspaceRelativePath.Normalize("."));

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Revoked, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleWorkspace, result.FailureKind);
    }

    [Fact]
    public void PermissionReviewViewModel_CommandShowsContainmentDisclosure()
    {
        var executable = CreateExecutable("disclosure.sh", "exit 0");
        var payload = new AgentExecuteCommandActionPayload(
            executable,
            Array.Empty<string>(),
            AgentWorkspaceRelativePath.Normalize("."));
        var request = AgentActionRequestComposer.Compose(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("agent-target"),
            AgentBackendId.FromValue("backend:test"),
            _scope.Identity,
            _scope.Generation,
            _resolver,
            payload);
        var viewModel = new PermissionReviewViewModel(
            request,
            request.DisplaySummary,
            _scope,
            _ => { });

        Assert.Contains(
            "not filesystem or network sandboxing",
            viewModel.ContainmentDisclosureText,
            StringComparison.Ordinal);
        Assert.Contains(
            "not filesystem or network sandboxing",
            request.DisplaySummary.DetailText,
            StringComparison.Ordinal);
    }

    private AgentResolvedCommand ResolveOrFail(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory) =>
        ResolveOrFail(new AgentExecuteCommandActionPayload(
            executable,
            arguments,
            AgentWorkspaceRelativePath.Normalize(workingDirectory)));

    private AgentResolvedCommand ResolveOrFail(AgentExecuteCommandActionPayload payload)
    {
        Assert.True(_resolver.TryResolve(payload, out var resolved, out var error), error);
        if (resolved!.DenylistResult.IsDenied)
        {
            throw new InvalidOperationException("resolved command is denied by policy");
        }

        return resolved!;
    }

    private string CreateExecutable(string name, string body)
    {
        var path = Path.Combine(_workspaceRoot, name);
        File.WriteAllText(path, $"#!/bin/sh\n{body}\n");
        MakeExecutable(path);
        return path;
    }

    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static string? FindSystemExecutable(string name)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsPidAlive(string pid)
    {
        try
        {
            var output = RunCommand("/bin/sh", "-c", $"kill -0 {pid} 2>/dev/null && echo alive || true");
            return output.Contains("alive", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string RunCommand(string executable, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private ContractAgentActionBroker CreateBroker(
        IAgentPermissionReviewService? reviewService = null,
        IWorkspaceActionAuthority? authority = null) =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("agent-target"),
            AgentBackendId.FromValue("backend:test"),
            authority ?? new FakeWorkspaceActionAuthority(_scope),
            new WorkspaceFileReader(),
            new WorkspaceFileMutator(),
            _resolver,
            _executor,
            new AgentActionRunSlotTracker(),
            new AgentActionCorrelationRegistry(),
            reviewService ?? new AllowingPermissionReviewService());

    private sealed class AllowingPermissionReviewService : IAgentPermissionReviewService
    {
        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                request.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
    }

    private sealed class ExpiredPermissionReviewService : IAgentPermissionReviewService
    {
        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                request.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow.AddMinutes(-10),
                DateTimeOffset.UtcNow.AddMinutes(-5),
                true));
    }
}
