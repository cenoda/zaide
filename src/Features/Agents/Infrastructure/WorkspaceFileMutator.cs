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
/// Zaide-owned bounded mutation adapter for one accepted workspace file
/// proposal. Revalidates the captured workspace root and target containment
/// immediately before apply, enforces optimistic concurrency on the captured
/// base revision, writes through a same-directory temporary file, and
/// atomically replaces or creates the target where the platform supports it.
/// </summary>
internal sealed class WorkspaceFileMutator : IAgentFileMutator
{
    private const uint S_IFMT = 0xF000;
    private const uint S_IFREG = 0x8000;
    private const int StatBufferSize = 256;
    private const int StModeOffset = 24;

    /// <summary>
    /// Test hook invoked after validation succeeds but immediately before the
    /// mutation is applied. Never set in production.
    /// </summary>
    internal Action? OnAfterValidationBeforeApply { get; set; }

    public AgentFileMutationResult Apply(
        WorkspaceActionScope scope,
        AgentFileActionProposal proposal,
        AgentActionPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(payload);

        if (cancellationToken.IsCancellationRequested)
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Cancelled,
                "Mutation was cancelled before it began.");
        }

        if (!ValidateProposalBinding(scope, proposal, payload, out var bindingError))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Failed,
                bindingError!);
        }

        if (!TryValidateWorkspaceRoot(scope, out var rootError, out var canonicalRoot))
        {
            return rootError!;
        }

        var candidate = Path.GetFullPath(Path.Combine(canonicalRoot, proposal.Path.NormalizedPath));
        if (!IsContained(canonicalRoot, candidate))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.PathEscaped,
                "Path resolves outside the workspace root.");
        }

        if (!TryRealpath(candidate, out var canonicalTarget))
        {
            if (proposal.Operation == AgentFileProposalOperation.Create)
            {
                return ApplyCreate(
                    proposal,
                    payload,
                    canonicalRoot,
                    candidate,
                    cancellationToken);
            }

            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.NotFound,
                "File does not exist in the workspace.");
        }

        if (!IsContained(canonicalRoot, canonicalTarget))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.PathEscaped,
                "Path resolves outside the workspace root via a link target.");
        }

        if (Directory.Exists(canonicalTarget))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.NotRegularFile,
                "Path refers to a directory, not a regular file.");
        }

        if (!TryGetMode(canonicalTarget, out var mode))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Unreadable,
                "File metadata could not be read.");
        }

        if ((mode & S_IFMT) != S_IFREG)
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.NotRegularFile,
                "Path refers to a special file, not a regular file.");
        }

        OnAfterValidationBeforeApply?.Invoke();

        if (cancellationToken.IsCancellationRequested)
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Cancelled,
                "Mutation was cancelled before the target was opened.");
        }

        return proposal.Operation switch
        {
            AgentFileProposalOperation.Create => AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Conflict,
                "Create target already exists in the workspace."),
            AgentFileProposalOperation.Replace => ApplyReplace(
                proposal,
                payload,
                canonicalRoot,
                canonicalTarget,
                cancellationToken),
            AgentFileProposalOperation.Delete => ApplyDelete(
                proposal,
                canonicalRoot,
                canonicalTarget,
                cancellationToken),
            _ => AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Failed,
                "Unsupported file mutation operation."),
        };
    }

    private static bool ValidateProposalBinding(
        WorkspaceActionScope scope,
        AgentFileActionProposal proposal,
        AgentActionPayload payload,
        out string? error)
    {
        error = null;

        if (!scope.Identity.Equals(proposal.WorkspaceScope.Identity)
            || !scope.Generation.Equals(proposal.WorkspaceScope.Generation))
        {
            error = "Workspace scope no longer matches the accepted proposal.";
            return false;
        }

        if (!scope.CapturedCanonicalRoot.Equals(
                proposal.WorkspaceScope.CapturedCanonicalRoot,
                StringComparison.Ordinal)
            || scope.CapturedRootDevice != proposal.WorkspaceScope.CapturedRootDevice
            || scope.CapturedRootInode != proposal.WorkspaceScope.CapturedRootInode)
        {
            error = "Captured workspace root no longer matches the accepted proposal.";
            return false;
        }

        if (!proposal.PermissionFingerprintMatchesBase())
        {
            error = "Proposal permission fingerprint does not match its base revision.";
            return false;
        }

        var expectedKind = proposal.Operation switch
        {
            AgentFileProposalOperation.Create => AgentActionKind.CreateFile,
            AgentFileProposalOperation.Replace => AgentActionKind.ReplaceFile,
            AgentFileProposalOperation.Delete => AgentActionKind.DeleteFile,
            _ => (AgentActionKind)(-1),
        };

        if (payload.Kind != expectedKind)
        {
            error = "Action payload kind does not match the accepted proposal.";
            return false;
        }

        switch (payload)
        {
            case AgentCreateFileActionPayload createPayload:
                if (!createPayload.Path.Equals(proposal.Path))
                {
                    error = "Create payload path does not match the accepted proposal.";
                    return false;
                }

                if (!createPayload.ProposedRevision.Equals(proposal.ProposedRevision))
                {
                    error = "Create payload revision does not match the accepted proposal.";
                    return false;
                }

                return true;

            case AgentReplaceFileActionPayload replacePayload:
                if (!replacePayload.Path.Equals(proposal.Path))
                {
                    error = "Replace payload path does not match the accepted proposal.";
                    return false;
                }

                if (proposal.BaseRevision is null
                    || !replacePayload.BaseRevision.Equals(proposal.BaseRevision))
                {
                    error = "Replace payload base revision does not match the accepted proposal.";
                    return false;
                }

                if (!replacePayload.ProposedRevision.Equals(proposal.ProposedRevision))
                {
                    error = "Replace payload revision does not match the accepted proposal.";
                    return false;
                }

                return true;

            case AgentDeleteFileActionPayload deletePayload:
                if (!deletePayload.Path.Equals(proposal.Path))
                {
                    error = "Delete payload path does not match the accepted proposal.";
                    return false;
                }

                if (proposal.BaseRevision is null
                    || !deletePayload.BaseRevision.Equals(proposal.BaseRevision))
                {
                    error = "Delete payload base revision does not match the accepted proposal.";
                    return false;
                }

                return true;

            default:
                error = "Action payload is not a supported file mutation.";
                return false;
        }
    }

    private AgentFileMutationResult ApplyCreate(
        AgentFileActionProposal proposal,
        AgentActionPayload payload,
        string canonicalRoot,
        string candidate,
        CancellationToken cancellationToken)
    {
        if (payload is not AgentCreateFileActionPayload createPayload)
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Failed,
                "Create payload is required for a create mutation.");
        }

        var parentDirectory = Path.GetDirectoryName(candidate);
        if (string.IsNullOrEmpty(parentDirectory))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.PathEscaped,
                "Create target parent directory could not be resolved.");
        }

        if (!TryRealpath(parentDirectory, out var canonicalParent)
            || !IsRootOrContained(canonicalRoot, canonicalParent))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.PathEscaped,
                "Create target parent directory resolves outside the workspace root.");
        }

        if (!Directory.Exists(canonicalParent))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.NotFound,
                "Create target parent directory does not exist.");
        }

        OnAfterValidationBeforeApply?.Invoke();

        if (cancellationToken.IsCancellationRequested)
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Cancelled,
                "Mutation was cancelled before the create was applied.");
        }

        return WriteTextAtomically(
            canonicalRoot,
            candidate,
            createPayload.ProposedText,
            createPayload.ProposedRevision,
            mustNotExist: true,
            cancellationToken);
    }

    private AgentFileMutationResult ApplyReplace(
        AgentFileActionProposal proposal,
        AgentActionPayload payload,
        string canonicalRoot,
        string canonicalTarget,
        CancellationToken cancellationToken)
    {
        if (payload is not AgentReplaceFileActionPayload replacePayload)
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Failed,
                "Replace payload is required for a replace mutation.");
        }

        if (!TryReadCurrentRevision(canonicalRoot, canonicalTarget, cancellationToken, out var currentRevision, out var readError))
        {
            return readError!;
        }

        if (proposal.IsBaseStale(currentRevision))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Conflict,
                "Base content changed before the replace could be applied.");
        }

        return WriteTextAtomically(
            canonicalRoot,
            canonicalTarget,
            replacePayload.ProposedText,
            replacePayload.ProposedRevision,
            mustNotExist: false,
            cancellationToken);
    }

    private AgentFileMutationResult ApplyDelete(
        AgentFileActionProposal proposal,
        string canonicalRoot,
        string canonicalTarget,
        CancellationToken cancellationToken)
    {
        if (!TryReadCurrentRevision(canonicalRoot, canonicalTarget, cancellationToken, out var currentRevision, out var readError))
        {
            return readError!;
        }

        if (proposal.IsBaseStale(currentRevision))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Conflict,
                "Base content changed before the delete could be applied.");
        }

        try
        {
            File.Delete(canonicalTarget);
        }
        catch (UnauthorizedAccessException)
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Unreadable,
                "File could not be deleted due to insufficient permissions.");
        }
        catch (IOException)
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Failed,
                "File could not be deleted.");
        }

        if (File.Exists(canonicalTarget))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Failed,
                "Delete did not remove the target file.");
        }

        return AgentFileMutationResult.DeleteSuccess(
            $"Deleted {proposal.Path.NormalizedPath}.");
    }

    private AgentFileMutationResult WriteTextAtomically(
        string canonicalRoot,
        string targetPath,
        string proposedText,
        AgentContentRevision expectedRevision,
        bool mustNotExist,
        CancellationToken cancellationToken)
    {
        var parentDirectory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrEmpty(parentDirectory))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.PathEscaped,
                "Target parent directory could not be resolved.");
        }

        if (mustNotExist && File.Exists(targetPath))
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Conflict,
                "Create target already exists in the workspace.");
        }

        var tempPath = Path.Combine(
            parentDirectory,
            $".{Path.GetFileName(targetPath)}.zaide-tmp-{Guid.NewGuid():N}");

        try
        {
            var bytes = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetBytes(proposedText);

            if (bytes.Length > AgentActionBudgets.ProposedFileTextMaxBytes)
            {
                return AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Failed,
                    "Proposed file text exceeds the maximum byte budget.");
            }

            var actualRevision = AgentContentRevision.FromBytes(bytes);
            if (!actualRevision.Equals(expectedRevision))
            {
                return AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Failed,
                    "Proposed revision does not match the payload bytes.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.WriteAllBytes(tempPath, bytes);
            }
            catch (UnauthorizedAccessException)
            {
                return AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Unreadable,
                    "Temporary file could not be written due to insufficient permissions.");
            }
            catch (IOException)
            {
                return AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Failed,
                    "Temporary file could not be written.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetHandleRealPathForFile(tempPath, out var tempRealPath)
                || !IsContained(canonicalRoot, tempRealPath))
            {
                return AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.PathEscaped,
                    "Temporary file resolves outside the workspace root.");
            }

            if (!mustNotExist)
            {
                if (!TryReadCurrentRevision(canonicalRoot, targetPath, cancellationToken, out var currentRevision, out var staleError))
                {
                    return staleError!;
                }

                if (currentRevision is null)
                {
                    return AgentFileMutationResult.Rejected(
                        AgentFileMutationOutcome.Conflict,
                        "Replace target disappeared before the mutation could be applied.");
                }
            }

            try
            {
                if (mustNotExist)
                {
                    File.Move(tempPath, targetPath);
                }
                else
                {
                    File.Move(tempPath, targetPath, overwrite: true);
                }
            }
            catch (UnauthorizedAccessException)
            {
                return AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Unreadable,
                    "Target file could not be replaced due to insufficient permissions.");
            }
            catch (IOException exception) when (mustNotExist && File.Exists(targetPath))
            {
                return AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Conflict,
                    $"Create target appeared before the mutation could be applied: {exception.Message}");
            }
            catch (IOException)
            {
                return AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Failed,
                    "Target file could not be replaced atomically.");
            }

            if (!File.Exists(targetPath))
            {
                return AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Failed,
                    "Mutation completed without confirming the target file on disk.");
            }

            if (!TryReadCurrentRevision(canonicalRoot, targetPath, CancellationToken.None, out var confirmedRevision, out var confirmError)
                || confirmedRevision is null
                || !confirmedRevision.Value.Equals(expectedRevision))
            {
                return AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Failed,
                    "Mutation could not be confirmed on disk.");
            }

            var revision = confirmedRevision.Value;
            return AgentFileMutationResult.Success(
                revision,
                bytes.Length,
                $"Wrote {bytes.Length} byte(s); revision {revision.Value}.");
        }
        catch (OperationCanceledException)
        {
            return AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Cancelled,
                "Mutation was cancelled while writing the temporary file.");
        }
        finally
        {
            TryDeleteQuietly(tempPath);
        }
    }

    private static bool TryValidateWorkspaceRoot(
        WorkspaceActionScope scope,
        out AgentFileMutationResult? error,
        out string canonicalRoot)
    {
        canonicalRoot = string.Empty;
        error = null;

        if (!TryRealpath(scope.RootPath, out canonicalRoot))
        {
            error = AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Unreadable,
                "Workspace root is unavailable.");
            return false;
        }

        if (!string.Equals(canonicalRoot, scope.CapturedCanonicalRoot, StringComparison.Ordinal))
        {
            error = AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.PathEscaped,
                "Workspace root has changed since the action scope was captured.");
            return false;
        }

        if (!TryGetDeviceInode(canonicalRoot, out var liveDevice, out var liveInode))
        {
            error = AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Unreadable,
                "Workspace root metadata could not be read.");
            return false;
        }

        if (liveDevice != scope.CapturedRootDevice
            || liveInode != scope.CapturedRootInode)
        {
            error = AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.PathEscaped,
                "Workspace root has been replaced since the action scope was captured.");
            return false;
        }

        return true;
    }

    private static bool TryReadCurrentRevision(
        string canonicalRoot,
        string canonicalTarget,
        CancellationToken cancellationToken,
        out AgentContentRevision? revision,
        out AgentFileMutationResult? error)
    {
        revision = null;
        error = null;

        if (cancellationToken.IsCancellationRequested)
        {
            error = AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Cancelled,
                "Mutation was cancelled while reading the current base revision.");
            return false;
        }

        FileStream stream;
        try
        {
            stream = new FileStream(
                canonicalTarget,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (FileNotFoundException)
        {
            error = AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.NotFound,
                "File does not exist in the workspace.");
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            error = AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.NotFound,
                "File does not exist in the workspace.");
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            error = AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Unreadable,
                "File could not be opened for reading.");
            return false;
        }
        catch (IOException)
        {
            error = AgentFileMutationResult.Rejected(
                AgentFileMutationOutcome.Unreadable,
                "File could not be opened for reading.");
            return false;
        }

        using (stream)
        {
            if (!TryGetHandleRealPath(stream, out var openedRealPath)
                || !IsContained(canonicalRoot, openedRealPath))
            {
                error = AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.PathEscaped,
                    "Opened file resolves outside the workspace root.");
                return false;
            }

            long length;
            try
            {
                length = stream.Length;
            }
            catch (IOException)
            {
                error = AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Unreadable,
                    "File length could not be determined.");
                return false;
            }

            if (length > AgentActionBudgets.RegularFileReadMaxBytes)
            {
                error = AgentFileMutationResult.Rejected(
                    AgentFileMutationOutcome.Failed,
                    "File exceeds the regular-file read budget.");
                return false;
            }

            var buffer = new byte[(int)length];
            var total = 0;
            while (total < buffer.Length)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    error = AgentFileMutationResult.Rejected(
                        AgentFileMutationOutcome.Cancelled,
                        "Mutation was cancelled while reading the current base revision.");
                    return false;
                }

                int read;
                try
                {
                    read = stream.Read(buffer, total, buffer.Length - total);
                }
                catch (OperationCanceledException)
                {
                    error = AgentFileMutationResult.Rejected(
                        AgentFileMutationOutcome.Cancelled,
                        "Mutation was cancelled while reading the current base revision.");
                    return false;
                }
                catch (IOException)
                {
                    error = AgentFileMutationResult.Rejected(
                        AgentFileMutationOutcome.Unreadable,
                        "File could not be read.");
                    return false;
                }

                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            revision = AgentContentRevision.FromBytes(buffer.AsSpan(0, total));
            return true;
        }
    }

    private static bool TryGetHandleRealPathForFile(string path, out string realPath)
    {
        realPath = string.Empty;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return TryGetHandleRealPath(stream, out realPath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDeleteQuietly(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsContained(string canonicalRoot, string canonicalCandidate)
    {
        if (string.Equals(canonicalRoot, canonicalCandidate, StringComparison.Ordinal))
        {
            return false;
        }

        var rootWithSeparator = canonicalRoot.EndsWith(Path.DirectorySeparatorChar)
            ? canonicalRoot
            : canonicalRoot + Path.DirectorySeparatorChar;

        return canonicalCandidate.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }

    private static bool IsRootOrContained(string canonicalRoot, string canonicalCandidate)
    {
        if (string.Equals(canonicalRoot, canonicalCandidate, StringComparison.Ordinal))
        {
            return true;
        }

        return IsContained(canonicalRoot, canonicalCandidate);
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

    private static bool TryGetDeviceInode(
        string path,
        out ulong device,
        out ulong inode)
    {
        device = 0;
        inode = 0;
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

        device = BitConverter.ToUInt64(buffer, 0);
        inode = BitConverter.ToUInt64(buffer, 8);
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
