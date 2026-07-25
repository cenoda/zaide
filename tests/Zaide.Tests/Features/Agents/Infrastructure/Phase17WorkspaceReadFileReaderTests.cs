using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents.Infrastructure;

/// <summary>
/// Phase 17 M2 — bounded read-only workspace file access and its containment,
/// symbolic-link, TOCTOU, type, size, binary, and cancellation defenses.
/// Linux is the supported platform for canonical containment.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class Phase17WorkspaceReadFileReaderTests : IDisposable
{
    private readonly string _outsideRoot;
    private readonly string _workspaceRoot;
    private readonly WorkspaceActionScope _scope;
    private readonly WorkspaceFileReader _reader = new();

    public Phase17WorkspaceReadFileReaderTests()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "zaide-p17-read-" + Guid.NewGuid().ToString("N"));
        _workspaceRoot = Path.Combine(baseDir, "wsroot");
        _outsideRoot = Path.Combine(baseDir, "outside");
        Directory.CreateDirectory(_workspaceRoot);
        Directory.CreateDirectory(_outsideRoot);

        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot);
    }

    public void Dispose()
    {
        try
        {
            var parent = Directory.GetParent(_workspaceRoot)?.FullName;
            if (parent is not null && Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private AgentFileReadResult Read(string relativePath, CancellationToken cancellationToken = default) =>
        _reader.Read(_scope, AgentWorkspaceRelativePath.Normalize(relativePath), cancellationToken);

    private string WorkspaceFile(string name) => Path.Combine(_workspaceRoot, name);

    [Fact]
    public void Read_RegularTextFile_ReturnsAttributableSnapshot()
    {
        const string content = "hello workspace";
        File.WriteAllText(WorkspaceFile("note.txt"), content);

        var result = Read("note.txt");

        Assert.Equal(AgentFileReadOutcome.Succeeded, result.Outcome);
        Assert.Equal(content, result.Content);
        Assert.Equal(Encoding.UTF8.GetByteCount(content), result.ByteLength);
        Assert.Equal(AgentContentRevision.FromUtf8Text(content), result.Revision);
    }

    [Fact]
    public void Read_EmptyFile_Succeeds()
    {
        File.WriteAllText(WorkspaceFile("empty.txt"), string.Empty);

        var result = Read("empty.txt");

        Assert.Equal(AgentFileReadOutcome.Succeeded, result.Outcome);
        Assert.Equal(string.Empty, result.Content);
        Assert.Equal(0, result.ByteLength);
    }

    [Fact]
    public void Read_SameFileTwice_ProducesStableDigest()
    {
        File.WriteAllText(WorkspaceFile("stable.txt"), "deterministic");

        var first = Read("stable.txt");
        var second = Read("stable.txt");

        Assert.Equal(AgentFileReadOutcome.Succeeded, first.Outcome);
        Assert.Equal(first.Revision, second.Revision);
    }

    [Fact]
    public void Read_ChangedFile_ProducesDifferentDigestReflectingBytes()
    {
        var path = WorkspaceFile("changing.txt");
        File.WriteAllText(path, "before");
        var before = Read("changing.txt");

        File.WriteAllText(path, "after change");
        var after = Read("changing.txt");

        Assert.NotEqual(before.Revision, after.Revision);
        Assert.Equal(AgentContentRevision.FromUtf8Text("after change"), after.Revision);
    }

    [Fact]
    public void Read_MissingFile_ReturnsNotFound()
    {
        var result = Read("does-not-exist.txt");

        Assert.Equal(AgentFileReadOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void Read_Directory_ReturnsNotRegularFile()
    {
        Directory.CreateDirectory(WorkspaceFile("subdir"));

        var result = Read("subdir");

        Assert.Equal(AgentFileReadOutcome.NotRegularFile, result.Outcome);
    }

    [Fact]
    public void Read_NestedFileViaBackslashSeparator_NormalizesAndSucceeds()
    {
        Directory.CreateDirectory(WorkspaceFile("nested"));
        File.WriteAllText(Path.Combine(_workspaceRoot, "nested", "inner.txt"), "inner");

        var result = Read("nested\\inner.txt");

        Assert.Equal(AgentFileReadOutcome.Succeeded, result.Outcome);
        Assert.Equal("inner", result.Content);
    }

    [Fact]
    public void Read_WrongCase_IsCaseSensitiveOnLinux_ReturnsNotFound()
    {
        File.WriteAllText(WorkspaceFile("CaseSensitive.txt"), "x");

        var result = Read("casesensitive.txt");

        Assert.Equal(AgentFileReadOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void Normalize_TraversalPath_IsRejectedAtBoundary()
    {
        Assert.Throws<ArgumentException>(() => AgentWorkspaceRelativePath.Normalize("../outside.txt"));
    }

    [Fact]
    public void Normalize_AbsolutePath_IsRejectedAtBoundary()
    {
        Assert.Throws<ArgumentException>(() => AgentWorkspaceRelativePath.Normalize("/etc/passwd"));
    }

    [Fact]
    public void Read_SymlinkToContainedFile_Succeeds()
    {
        File.WriteAllText(WorkspaceFile("target.txt"), "linked content");
        File.CreateSymbolicLink(WorkspaceFile("link.txt"), WorkspaceFile("target.txt"));

        var result = Read("link.txt");

        Assert.Equal(AgentFileReadOutcome.Succeeded, result.Outcome);
        Assert.Equal("linked content", result.Content);
    }

    [Fact]
    public void Read_FileSymlinkEscapingWorkspace_ReturnsPathEscaped()
    {
        var outsideFile = Path.Combine(_outsideRoot, "secret.txt");
        File.WriteAllText(outsideFile, "top secret");
        File.CreateSymbolicLink(WorkspaceFile("escape.txt"), outsideFile);

        var result = Read("escape.txt");

        Assert.Equal(AgentFileReadOutcome.PathEscaped, result.Outcome);
        Assert.Null(result.Content);
    }

    [Fact]
    public void Read_DirectorySymlinkEscapingWorkspace_ReturnsPathEscaped()
    {
        var outsideDir = Path.Combine(_outsideRoot, "data");
        Directory.CreateDirectory(outsideDir);
        File.WriteAllText(Path.Combine(outsideDir, "secret.txt"), "top secret");
        Directory.CreateSymbolicLink(WorkspaceFile("linkdir"), outsideDir);

        var result = Read("linkdir/secret.txt");

        Assert.Equal(AgentFileReadOutcome.PathEscaped, result.Outcome);
    }

    [Fact]
    public void Read_SiblingSharingTextualPrefix_ReturnsPathEscaped()
    {
        // A sibling directory whose path shares only a textual prefix with the
        // workspace root must not be treated as contained.
        var sibling = _workspaceRoot + "-sibling";
        Directory.CreateDirectory(sibling);
        var siblingSecret = Path.Combine(sibling, "secret.txt");
        File.WriteAllText(siblingSecret, "prefix trap");
        File.CreateSymbolicLink(WorkspaceFile("prefix-link.txt"), siblingSecret);

        var result = Read("prefix-link.txt");

        Assert.Equal(AgentFileReadOutcome.PathEscaped, result.Outcome);
    }

    [Fact]
    public void Read_SymlinkRetargetedBetweenValidationAndOpen_ReturnsPathEscaped()
    {
        var insidePath = WorkspaceFile("inside.txt");
        File.WriteAllText(insidePath, "safe");
        var outsidePath = Path.Combine(_outsideRoot, "evil.txt");
        File.WriteAllText(outsidePath, "escaped");

        var linkPath = WorkspaceFile("toctou.txt");
        File.CreateSymbolicLink(linkPath, insidePath);

        _reader.OnAfterValidationBeforeOpen = () =>
        {
            File.Delete(linkPath);
            File.CreateSymbolicLink(linkPath, outsidePath);
        };

        var result = Read("toctou.txt");

        Assert.Equal(AgentFileReadOutcome.PathEscaped, result.Outcome);
    }

    [Fact]
    public void Read_SpecialFile_ReturnsNotRegularFile()
    {
        var fifoPath = WorkspaceFile("pipe");
        Assert.Equal(0, Mkfifo(fifoPath, 0b110_100_100));

        var result = Read("pipe");

        Assert.Equal(AgentFileReadOutcome.NotRegularFile, result.Outcome);
    }

    [Fact]
    public void Read_FileWithNulByte_ReturnsBinary()
    {
        File.WriteAllBytes(WorkspaceFile("binary.bin"), new byte[] { 0x68, 0x00, 0x69 });

        var result = Read("binary.bin");

        Assert.Equal(AgentFileReadOutcome.Binary, result.Outcome);
    }

    [Fact]
    public void Read_InvalidUtf8_ReturnsBinary()
    {
        File.WriteAllBytes(WorkspaceFile("invalid.bin"), new byte[] { 0xFF, 0xFE, 0xFD });

        var result = Read("invalid.bin");

        Assert.Equal(AgentFileReadOutcome.Binary, result.Outcome);
    }

    [Fact]
    public void Read_FileAtBudget_Succeeds()
    {
        var content = new string('a', AgentActionBudgets.RegularFileReadMaxBytes);
        File.WriteAllText(WorkspaceFile("atbudget.txt"), content);

        var result = Read("atbudget.txt");

        Assert.Equal(AgentFileReadOutcome.Succeeded, result.Outcome);
        Assert.Equal(AgentActionBudgets.RegularFileReadMaxBytes, result.ByteLength);
    }

    [Fact]
    public void Read_OversizedFile_ReturnsTooLarge()
    {
        var content = new string('a', AgentActionBudgets.RegularFileReadMaxBytes + 1);
        File.WriteAllText(WorkspaceFile("oversized.txt"), content);

        var result = Read("oversized.txt");

        Assert.Equal(AgentFileReadOutcome.TooLarge, result.Outcome);
    }

    [Fact]
    public void Read_UnreadableFile_ReturnsUnreadable()
    {
        if (Geteuid() == 0)
        {
            // Running as root bypasses permission bits; the check is not meaningful.
            return;
        }

        var path = WorkspaceFile("locked.txt");
        File.WriteAllText(path, "secret");
        File.SetUnixFileMode(path, UnixFileMode.None);

        try
        {
            var result = Read("locked.txt");
            Assert.Equal(AgentFileReadOutcome.Unreadable, result.Outcome);
        }
        finally
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void Read_CancelledBeforeOpen_ReturnsCancelled()
    {
        File.WriteAllText(WorkspaceFile("cancel.txt"), "content");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = Read("cancel.txt", cts.Token);

        Assert.Equal(AgentFileReadOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public void Read_CancelledAfterValidation_ReturnsCancelled()
    {
        File.WriteAllText(WorkspaceFile("cancel-late.txt"), "content");
        using var cts = new CancellationTokenSource();
        _reader.OnAfterValidationBeforeOpen = cts.Cancel;

        var result = Read("cancel-late.txt", cts.Token);

        Assert.Equal(AgentFileReadOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public void Read_RootSymlinkRetargeted_ReturnsPathEscaped()
    {
        // Capture the workspace scope before tampering with the root.
        StatDeviceInode(_workspaceRoot, out var dev, out var ino);
        var capturedScope = new WorkspaceActionScope(
            _scope.Identity,
            _scope.Generation,
            _workspaceRoot,
            capturedCanonicalRoot: _workspaceRoot,
            capturedRootDevice: dev,
            capturedRootInode: ino);

        var insideFile = WorkspaceFile("root-toctou.txt");
        File.WriteAllText(insideFile, "safe");

        // Replace the workspace root directory with a symlink to outside.
        Directory.Delete(_workspaceRoot, recursive: true);
        try
        {
            Directory.CreateSymbolicLink(_workspaceRoot, _outsideRoot);

            var result = _reader.Read(
                capturedScope,
                AgentWorkspaceRelativePath.Normalize("root-toctou.txt"),
                CancellationToken.None);

            Assert.Equal(AgentFileReadOutcome.PathEscaped, result.Outcome);
        }
        finally
        {
            // Restore the workspace root as a real directory so other tests
            // (and Dispose) see the expected layout.
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot);
            }

            Directory.CreateDirectory(_workspaceRoot);
        }
    }

    [Fact]
    public void Read_RootReplacedWithNewDirectory_ReturnsPathEscaped()
    {
        // Capture the scope with the original device and inode identity.
        StatDeviceInode(_workspaceRoot, out var origDev, out var origIno);
        var capturedScope = new WorkspaceActionScope(
            _scope.Identity,
            _scope.Generation,
            _workspaceRoot,
            capturedCanonicalRoot: _workspaceRoot,
            capturedRootDevice: origDev,
            capturedRootInode: origIno);

        var insideFile = WorkspaceFile("root-replace.txt");
        File.WriteAllText(insideFile, "safe");

        // Delete and recreate the root at the same path (new directory inode).
        Directory.Delete(_workspaceRoot, recursive: true);
        try
        {
            Directory.CreateDirectory(_workspaceRoot);

            var result = _reader.Read(
                capturedScope,
                AgentWorkspaceRelativePath.Normalize("root-replace.txt"),
                CancellationToken.None);

            Assert.Equal(AgentFileReadOutcome.PathEscaped, result.Outcome);
        }
        finally
        {
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }

            Directory.CreateDirectory(_workspaceRoot);
        }
    }

    [Fact]
    public void Read_CapturedCanonicalRootMismatch_ReturnsPathEscaped()
    {
        // Capture a scope whose CapturedCanonicalRoot deliberately mismatches
        // the live root so the revalidation guard is exercised independently
        // of filesystem tampering. Device and inode must be valid for the
        // scope to be accepted; we stat the live root so the stat check
        // succeeds and only the path comparison triggers the rejection.
        StatDeviceInode(_workspaceRoot, out var dev, out var ino);
        var mismatchedScope = new WorkspaceActionScope(
            _scope.Identity,
            _scope.Generation,
            _workspaceRoot,
            capturedCanonicalRoot: _outsideRoot, // Deliberately wrong path.
            capturedRootDevice: dev,
            capturedRootInode: ino);

        File.WriteAllText(WorkspaceFile("mismatch.txt"), "content");

        var result = _reader.Read(
            mismatchedScope,
            AgentWorkspaceRelativePath.Normalize("mismatch.txt"),
            CancellationToken.None);

        Assert.Equal(AgentFileReadOutcome.PathEscaped, result.Outcome);
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "mkfifo")]
    private static extern int Mkfifo(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        uint mode);

    private static void StatDeviceInode(string path, out ulong device, out ulong inode)
    {
        var buffer = new byte[256];
        var result = Stat(path, buffer);
        if (result != 0)
        {
            throw new InvalidOperationException($"stat failed for '{path}'.");
        }

        device = BitConverter.ToUInt64(buffer, 0);
        inode = BitConverter.ToUInt64(buffer, 8);
    }

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint Geteuid();

    [DllImport("libc", SetLastError = true, EntryPoint = "stat")]
    private static extern int Stat(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        byte[] statBuffer);
}
