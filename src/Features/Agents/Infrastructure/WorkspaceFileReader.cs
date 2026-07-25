using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Zaide-owned bounded read adapter for one regular workspace file. This is the
/// action-control read security boundary. It canonicalizes the captured
/// workspace root, resolves the requested path against the live filesystem
/// (following symbolic links), enforces containment, rejects non-regular and
/// special files, binary and oversized content, honors cancellation, and returns
/// an attributable snapshot with a stable SHA-256 digest.
///
/// <para>
/// Time-of-check/time-of-use safety: after the file is opened, the reader
/// re-derives the actual opened path from the open descriptor and re-validates
/// containment, so a symbolic link retargeted between validation and open cannot
/// escape the workspace.
/// </para>
///
/// <para>Linux is the supported platform for canonical containment.</para>
/// </summary>
internal sealed class WorkspaceFileReader : IAgentFileReader
{
    private const uint S_IFMT = 0xF000;
    private const uint S_IFREG = 0x8000;
    private const int StatBufferSize = 256;
    private const int StModeOffset = 24;

    /// <summary>
    /// Test hook invoked after path validation succeeds but immediately before
    /// the file is opened. Used to exercise the retarget/TOCTOU defense
    /// deterministically. Never set in production.
    /// </summary>
    internal Action? OnAfterValidationBeforeOpen { get; set; }

    public AgentFileReadResult Read(
        WorkspaceActionScope scope,
        AgentWorkspaceRelativePath path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(path);

        if (cancellationToken.IsCancellationRequested)
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.Cancelled,
                "Read was cancelled before it began.");
        }

        if (!TryRealpath(scope.RootPath, out var canonicalRoot))
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.Unreadable,
                "Workspace root is unavailable.");
        }

        var candidate = Path.GetFullPath(Path.Combine(canonicalRoot, path.NormalizedPath));

        // Defense in depth: reject an obvious textual escape before touching disk.
        if (!IsContained(canonicalRoot, candidate))
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.PathEscaped,
                "Path resolves outside the workspace root.");
        }

        // Resolve the real target, following every symbolic link (including
        // intermediate directory links). A missing component yields no path.
        if (!TryRealpath(candidate, out var canonicalTarget))
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.NotFound,
                "File does not exist in the workspace.");
        }

        if (!IsContained(canonicalRoot, canonicalTarget))
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.PathEscaped,
                "Path resolves outside the workspace root via a link target.");
        }

        if (Directory.Exists(canonicalTarget))
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.NotRegularFile,
                "Path refers to a directory, not a regular file.");
        }

        if (!TryGetMode(canonicalTarget, out var mode))
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.Unreadable,
                "File metadata could not be read.");
        }

        if ((mode & S_IFMT) != S_IFREG)
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.NotRegularFile,
                "Path refers to a special file, not a regular file.");
        }

        OnAfterValidationBeforeOpen?.Invoke();

        if (cancellationToken.IsCancellationRequested)
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.Cancelled,
                "Read was cancelled before the file was opened.");
        }

        return ReadOpenedFile(canonicalRoot, candidate, cancellationToken);
    }

    private static AgentFileReadResult ReadOpenedFile(
        string canonicalRoot,
        string candidate,
        CancellationToken cancellationToken)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(
                candidate,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (FileNotFoundException)
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.NotFound,
                "File does not exist in the workspace.");
        }
        catch (DirectoryNotFoundException)
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.NotFound,
                "File does not exist in the workspace.");
        }
        catch (UnauthorizedAccessException)
        {
            if (Directory.Exists(candidate))
            {
                return AgentFileReadResult.Rejected(
                    AgentFileReadOutcome.NotRegularFile,
                    "Path refers to a directory, not a regular file.");
            }

            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.Unreadable,
                "File could not be opened for reading.");
        }
        catch (IOException)
        {
            return AgentFileReadResult.Rejected(
                AgentFileReadOutcome.Unreadable,
                "File could not be opened for reading.");
        }

        using (stream)
        {
            // TOCTOU: verify the descriptor we actually opened is still contained.
            if (!TryGetHandleRealPath(stream, out var openedRealPath))
            {
                return AgentFileReadResult.Rejected(
                    AgentFileReadOutcome.Unreadable,
                    "Opened file path could not be verified.");
            }

            if (!IsContained(canonicalRoot, openedRealPath))
            {
                return AgentFileReadResult.Rejected(
                    AgentFileReadOutcome.PathEscaped,
                    "Opened file resolves outside the workspace root.");
            }

            long length;
            try
            {
                length = stream.Length;
            }
            catch (IOException)
            {
                return AgentFileReadResult.Rejected(
                    AgentFileReadOutcome.Unreadable,
                    "File length could not be determined.");
            }

            if (length > AgentActionBudgets.RegularFileReadMaxBytes)
            {
                return AgentFileReadResult.Rejected(
                    AgentFileReadOutcome.TooLarge,
                    "File exceeds the regular-file read budget.");
            }

            // Read at most budget + 1 bytes so a file that grows during the read
            // is still rejected rather than silently truncated.
            var buffer = new byte[AgentActionBudgets.RegularFileReadMaxBytes + 1];
            var total = 0;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return AgentFileReadResult.Rejected(
                        AgentFileReadOutcome.Cancelled,
                        "Read was cancelled while streaming the file.");
                }

                int read;
                try
                {
                    read = stream.Read(buffer, total, buffer.Length - total);
                }
                catch (OperationCanceledException)
                {
                    return AgentFileReadResult.Rejected(
                        AgentFileReadOutcome.Cancelled,
                        "Read was cancelled while streaming the file.");
                }
                catch (IOException)
                {
                    return AgentFileReadResult.Rejected(
                        AgentFileReadOutcome.Unreadable,
                        "File could not be read.");
                }

                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > AgentActionBudgets.RegularFileReadMaxBytes)
                {
                    return AgentFileReadResult.Rejected(
                        AgentFileReadOutcome.TooLarge,
                        "File exceeds the regular-file read budget.");
                }
            }

            for (var i = 0; i < total; i++)
            {
                if (buffer[i] == 0)
                {
                    return AgentFileReadResult.Rejected(
                        AgentFileReadOutcome.Binary,
                        "File contains binary content and cannot be read as text.");
                }
            }

            string content;
            try
            {
                content = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true).GetString(buffer, 0, total);
            }
            catch (DecoderFallbackException)
            {
                return AgentFileReadResult.Rejected(
                    AgentFileReadOutcome.Binary,
                    "File is not valid UTF-8 text.");
            }

            var revision = AgentContentRevision.FromBytes(buffer.AsSpan(0, total));
            return AgentFileReadResult.Success(content, revision, total);
        }
    }

    private static bool IsContained(string canonicalRoot, string canonicalCandidate)
    {
        if (string.Equals(canonicalRoot, canonicalCandidate, StringComparison.Ordinal))
        {
            // The root itself is never a regular-file read target.
            return false;
        }

        var rootWithSeparator = canonicalRoot.EndsWith(Path.DirectorySeparatorChar)
            ? canonicalRoot
            : canonicalRoot + Path.DirectorySeparatorChar;

        return canonicalCandidate.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }

    private static bool TryGetHandleRealPath(FileStream stream, out string realPath)
    {
        realPath = string.Empty;
        var handle = stream.SafeFileHandle;
        if (handle is null || handle.IsInvalid)
        {
            return false;
        }

        var fileDescriptor = (int)handle.DangerousGetHandle();
        return TryRealpath($"/proc/self/fd/{fileDescriptor}", out realPath);
    }

    private static bool TryRealpath(string path, out string resolved)
    {
        resolved = string.Empty;
        IntPtr pointer;
        try
        {
            pointer = Realpath(path, IntPtr.Zero);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }

        if (pointer == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            resolved = Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
        }
        finally
        {
            Free(pointer);
        }

        return resolved.Length > 0;
    }

    private static bool TryGetMode(string path, out uint mode)
    {
        mode = 0;
        var buffer = new byte[StatBufferSize];
        int result;
        try
        {
            result = Stat(path, buffer);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }

        if (result != 0)
        {
            return false;
        }

        mode = BitConverter.ToUInt32(buffer, StModeOffset);
        return true;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "realpath")]
    private static extern IntPtr Realpath(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        IntPtr resolved);

    [DllImport("libc", EntryPoint = "free")]
    private static extern void Free(IntPtr pointer);

    [DllImport("libc", SetLastError = true, EntryPoint = "stat")]
    private static extern int Stat(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        byte[] statBuffer);
}
