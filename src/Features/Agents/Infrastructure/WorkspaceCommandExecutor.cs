using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Zaide-owned bounded command execution adapter for one approved resolved
/// command.
/// </summary>
internal sealed class WorkspaceCommandExecutor : IAgentCommandExecutor
{
    private const int StatBufferSize = 256;
    private const int StDeviceOffset = 0;
    private const int StInoOffset = 8;

    private readonly ISecretStore? _secretStore;

    public WorkspaceCommandExecutor(ISecretStore? secretStore = null)
    {
        _secretStore = secretStore;
    }

    /// <summary>
    /// Test hook invoked after validation succeeds but immediately before
    /// process start. Never set in production.
    /// </summary>
    internal Action? OnAfterValidationBeforeStart { get; set; }

    public AgentCommandExecutionResult Execute(
        WorkspaceActionScope scope,
        AgentResolvedCommand resolvedCommand,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(resolvedCommand);

        if (cancellationToken.IsCancellationRequested)
        {
            return AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.Cancelled,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Command execution was cancelled before it began.");
        }

        if (resolvedCommand.DenylistResult.IsDenied)
        {
            return AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.DeniedExecutable,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Executable is denied by the locked Phase 17 command denylist.");
        }

        if (!TryValidateWorkspaceRoot(scope, out var rootError))
        {
            return rootError!;
        }

        if (!TryResolveWorkingDirectory(
                scope,
                resolvedCommand.WorkingDirectory.NormalizedPath,
                out var canonicalWorkingDirectory,
                out var workingDirectoryError))
        {
            return workingDirectoryError!;
        }

        if (!TryRevalidateExecutable(resolvedCommand, out var executableError))
        {
            return executableError!;
        }

        OnAfterValidationBeforeStart?.Invoke();

        if (cancellationToken.IsCancellationRequested)
        {
            return AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.Cancelled,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Command execution was cancelled before the process started.");
        }

        return RunProcess(
            resolvedCommand,
            canonicalWorkingDirectory,
            cancellationToken);
    }

    private AgentCommandExecutionResult RunProcess(
        AgentResolvedCommand resolvedCommand,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = resolvedCommand.CanonicalAbsoluteExecutablePath,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in resolvedCommand.Arguments)
            {
                psi.ArgumentList.Add(argument);
            }

            foreach (var (name, value) in AgentCommandEnvironmentBuilder.Build(_secretStore))
            {
                psi.Environment[name] = value;
            }

            process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            if (!process.Start())
            {
                return AgentCommandExecutionResult.Terminal(
                    AgentCommandExecutionOutcome.StartupFailed,
                    exitCode: null,
                    AgentCommandStreamCapture.Empty,
                    AgentCommandStreamCapture.Empty,
                    "Command process failed to start.");
            }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(AgentActionBudgets.CommandExecutionTimeout);
            var linkedToken = timeoutSource.Token;

            var stdoutReader = new BoundedCommandStreamReader(
                AgentActionBudgets.CommandStdoutMaxBytes,
                AgentActionBudgets.CommandStdoutMaxLines);
            var stderrReader = new BoundedCommandStreamReader(
                AgentActionBudgets.CommandStderrMaxBytes,
                AgentActionBudgets.CommandStderrMaxLines);

            var stdoutTask = PumpAsync(process.StandardOutput, stdoutReader, linkedToken);
            var stderrTask = PumpAsync(process.StandardError, stderrReader, linkedToken);
            var wasCancelled = false;
            var timedOut = false;
            var budgetExceeded = false;

            using (linkedToken.Register(() => KillProcessTree(process)))
            {
                while (!process.HasExited)
                {
                    if (linkedToken.IsCancellationRequested)
                    {
                        wasCancelled = cancellationToken.IsCancellationRequested;
                        timedOut = !wasCancelled;
                        break;
                    }

                    if (!process.WaitForExit(50))
                    {
                        continue;
                    }
                }
            }

            if (!wasCancelled && cancellationToken.IsCancellationRequested)
            {
                wasCancelled = true;
            }

            try
            {
                Task.WaitAll(
                    new[] { stdoutTask, stderrTask },
                    AgentActionBudgets.ProcessTreeCleanupTimeout);
            }
            catch (AggregateException)
            {
                // Best-effort drain during cleanup.
            }

            KillProcessTree(process);
            WaitForExitBestEffort(process);

            budgetExceeded = stdoutReader.WasTruncated || stderrReader.WasTruncated;
            var stdout = stdoutReader.ToCapture();
            var stderr = stderrReader.ToCapture();

            if (wasCancelled)
            {
                return AgentCommandExecutionResult.Terminal(
                    AgentCommandExecutionOutcome.Cancelled,
                    process.HasExited ? process.ExitCode : null,
                    stdout,
                    stderr,
                    "Command execution was cancelled.");
            }

            if (timedOut)
            {
                return AgentCommandExecutionResult.Terminal(
                    AgentCommandExecutionOutcome.TimedOut,
                    process.HasExited ? process.ExitCode : null,
                    stdout,
                    stderr,
                    "Command execution exceeded the locked time budget.");
            }

            if (budgetExceeded)
            {
                return AgentCommandExecutionResult.Terminal(
                    AgentCommandExecutionOutcome.Truncated,
                    process.HasExited ? process.ExitCode : null,
                    stdout,
                    stderr,
                    "Command output exceeded the locked byte or line budget.");
            }

            if (!process.HasExited)
            {
                return AgentCommandExecutionResult.Terminal(
                    AgentCommandExecutionOutcome.IndeterminateCleanup,
                    exitCode: null,
                    stdout,
                    stderr,
                    "Command process did not terminate within the cleanup budget.");
            }

            if (process.ExitCode != 0)
            {
                return AgentCommandExecutionResult.Terminal(
                    AgentCommandExecutionOutcome.Failed,
                    process.ExitCode,
                    stdout,
                    stderr,
                    $"Command exited with status {process.ExitCode}.");
            }

            return AgentCommandExecutionResult.Success(
                process.ExitCode,
                stdout,
                stderr,
                "Command completed successfully.");
        }
        catch (Win32Exception)
        {
            return AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.StartupFailed,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Command process failed to start.");
        }
        catch (InvalidOperationException)
        {
            return AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.StartupFailed,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Command process failed to start.");
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static async Task PumpAsync(
        StreamReader reader,
        BoundedCommandStreamReader capture,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        while (!cancellationToken.IsCancellationRequested)
        {
            int charsRead;
            try
            {
                charsRead = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }

            if (charsRead == 0)
            {
                break;
            }

            capture.Append(buffer.AsSpan(0, charsRead));
            if (capture.WasTruncated)
            {
                break;
            }
        }
    }

    private static void KillProcessTree(Process? process)
    {
        if (process is null || process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may already be gone.
        }
    }

    private static void WaitForExitBestEffort(Process? process)
    {
        if (process is null || process.HasExited)
        {
            return;
        }

        try
        {
            process.WaitForExit((int)AgentActionBudgets.ProcessTreeCleanupTimeout.TotalMilliseconds);
        }
        catch
        {
            // Best effort.
        }
    }

    private static bool TryValidateWorkspaceRoot(
        WorkspaceActionScope scope,
        out AgentCommandExecutionResult? error)
    {
        error = null;
        if (!AgentCommandPathSupport.TryRealpath(scope.RootPath, out var canonicalRoot))
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.Unreadable,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Workspace root is unavailable.");
            return false;
        }

        if (!string.Equals(canonicalRoot, scope.CapturedCanonicalRoot, StringComparison.Ordinal))
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.PathEscaped,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Workspace root has changed since the action scope was captured.");
            return false;
        }

        if (!TryGetDeviceInode(canonicalRoot, out var liveDevice, out var liveInode))
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.Unreadable,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Workspace root metadata could not be read.");
            return false;
        }

        if (liveDevice != scope.CapturedRootDevice || liveInode != scope.CapturedRootInode)
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.PathEscaped,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Workspace root has been replaced since the action scope was captured.");
            return false;
        }

        return true;
    }

    private static bool TryResolveWorkingDirectory(
        WorkspaceActionScope scope,
        string relativeWorkingDirectory,
        out string canonicalWorkingDirectory,
        out AgentCommandExecutionResult? error)
    {
        canonicalWorkingDirectory = string.Empty;
        error = null;

        var candidate = Path.GetFullPath(
            Path.Combine(scope.CapturedCanonicalRoot, relativeWorkingDirectory));

        if (!AgentCommandPathSupport.IsContained(scope.CapturedCanonicalRoot, candidate))
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.PathEscaped,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Working directory resolves outside the workspace root.");
            return false;
        }

        if (!AgentCommandPathSupport.TryRealpath(candidate, out canonicalWorkingDirectory))
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.Unreadable,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Working directory could not be resolved.");
            return false;
        }

        if (!AgentCommandPathSupport.IsContained(scope.CapturedCanonicalRoot, canonicalWorkingDirectory))
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.PathEscaped,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Working directory resolves outside the workspace root via a link target.");
            return false;
        }

        if (!AgentCommandPathSupport.IsDirectory(canonicalWorkingDirectory))
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.Unreadable,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Working directory is not a directory.");
            return false;
        }

        return true;
    }

    private static bool TryRevalidateExecutable(
        AgentResolvedCommand resolvedCommand,
        out AgentCommandExecutionResult? error)
    {
        error = null;
        var canonicalPath = resolvedCommand.CanonicalAbsoluteExecutablePath;
        if (!AgentCommandPathSupport.TryRealpath(canonicalPath, out var liveCanonical)
            || !string.Equals(liveCanonical, canonicalPath, StringComparison.Ordinal))
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.Unreadable,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Executable identity changed since permission review.");
            return false;
        }

        if (!AgentCommandPathSupport.IsRegularExecutableFile(canonicalPath))
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.Unreadable,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Executable is no longer a regular executable file.");
            return false;
        }

        if (AgentCommandDenylist.Classify(canonicalPath).IsDenied)
        {
            error = AgentCommandExecutionResult.Terminal(
                AgentCommandExecutionOutcome.DeniedExecutable,
                exitCode: null,
                AgentCommandStreamCapture.Empty,
                AgentCommandStreamCapture.Empty,
                "Executable is denied by the locked Phase 17 command denylist.");
            return false;
        }

        return true;
    }

    private static bool TryGetDeviceInode(string path, out ulong device, out ulong inode)
    {
        device = 0;
        inode = 0;
        var buffer = new byte[StatBufferSize];
        if (Stat(path, buffer) != 0)
        {
            return false;
        }

        device = BitConverter.ToUInt64(buffer, StDeviceOffset);
        inode = BitConverter.ToUInt64(buffer, StInoOffset);
        return true;
    }

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int Stat(string path, byte[] buffer);

    private sealed class BoundedCommandStreamReader
    {
        private readonly int _maxBytes;
        private readonly int _maxLines;
        private readonly StringBuilder _builder = new();
        private readonly UTF8Encoding _encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
        private bool _containsInvalidText;

        public BoundedCommandStreamReader(int maxBytes, int maxLines)
        {
            _maxBytes = maxBytes;
            _maxLines = maxLines;
        }

        public bool WasTruncated { get; private set; }

        public void Append(ReadOnlySpan<char> chars)
        {
            if (WasTruncated)
            {
                return;
            }

            foreach (var character in chars)
            {
                if (character == '\0')
                {
                    _containsInvalidText = true;
                }
            }

            var proposed = _builder.ToString() + chars.ToString();
            var byteCount = AgentActionBudgets.GetUtf8ByteCount(proposed);
            var lineCount = CountLines(proposed);
            if (byteCount > _maxBytes || lineCount > _maxLines)
            {
                WasTruncated = true;
                var bounded = TruncateToBudget(proposed);
                _builder.Clear();
                _builder.Append(bounded);
                return;
            }

            _builder.Append(chars);
        }

        public AgentCommandStreamCapture ToCapture()
        {
            var text = _builder.ToString();
            if (_containsInvalidText)
            {
                text = text.Replace('\0', '\uFFFD');
            }

            try
            {
                _ = _encoding.GetBytes(text);
            }
            catch (EncoderFallbackException)
            {
                _containsInvalidText = true;
            }

            return AgentCommandStreamCapture.Create(text, WasTruncated, _containsInvalidText);
        }

        private string TruncateToBudget(string text)
        {
            var builder = new StringBuilder();
            var usedBytes = 0;
            var lines = 0;
            foreach (var character in text)
            {
                var charBytes = _encoding.GetByteCount(new[] { character });
                if (usedBytes + charBytes > _maxBytes)
                {
                    WasTruncated = true;
                    break;
                }

                builder.Append(character);
                usedBytes += charBytes;
                if (character == '\n')
                {
                    lines++;
                    if (lines > _maxLines)
                    {
                        WasTruncated = true;
                        break;
                    }
                }
            }

            return builder.ToString();
        }

        private static int CountLines(string text)
        {
            if (text.Length == 0)
            {
                return 0;
            }

            var lines = 1;
            foreach (var character in text)
            {
                if (character == '\n')
                {
                    lines++;
                }
            }

            return lines;
        }
    }
}
