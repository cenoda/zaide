using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Infrastructure;

/// <summary>
/// Phase 17 M5 — safe workspace mutation behind accepted immutable proposals.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class Phase17WorkspaceMutationMutatorTests : IDisposable
{
    private readonly string _outsideRoot;
    private readonly string _workspaceRoot;
    private readonly WorkspaceActionScope _scope;
    private readonly WorkspaceFileReader _reader = new();
    private readonly WorkspaceFileMutator _mutator = new();

    public Phase17WorkspaceMutationMutatorTests()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "zaide-p17-mut-" + Guid.NewGuid().ToString("N"));
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
    }

    [Fact]
    public void Apply_Create_WritesFileWhenAbsent()
    {
        const string text = "created content";
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("created.txt"),
            text);
        var proposal = CreateProposal(payload);

        var result = _mutator.Apply(_scope, proposal, payload, CancellationToken.None);

        Assert.Equal(AgentFileMutationOutcome.Succeeded, result.Outcome);
        Assert.Equal(text, File.ReadAllText(Path.Combine(_workspaceRoot, "created.txt")));
        Assert.Equal(payload.ProposedRevision, result.Revision);
        Assert.False(Directory.EnumerateFiles(_workspaceRoot, "*.zaide-tmp-*").GetEnumerator().MoveNext());
    }

    [Fact]
    public void Apply_Create_ConflictsWhenTargetExists()
    {
        File.WriteAllText(Path.Combine(_workspaceRoot, "exists.txt"), "existing");
        var path = AgentWorkspaceRelativePath.Normalize("exists.txt");
        var payload = new AgentCreateFileActionPayload(path, "new");
        var proposal = BuildManualProposal(
            AgentFileProposalOperation.Create,
            path,
            baseExists: false,
            baseRevision: null,
            proposedRevision: payload.ProposedRevision);

        var result = _mutator.Apply(_scope, proposal, payload, CancellationToken.None);

        Assert.Equal(AgentFileMutationOutcome.Conflict, result.Outcome);
        Assert.Equal("existing", File.ReadAllText(Path.Combine(_workspaceRoot, "exists.txt")));
    }

    [Fact]
    public void Apply_Replace_RequiresMatchingBaseRevision()
    {
        const string original = "original";
        const string replacement = "replacement";
        var path = AgentWorkspaceRelativePath.Normalize("replace.txt");
        File.WriteAllText(Path.Combine(_workspaceRoot, path.NormalizedPath), original);
        var baseRevision = AgentContentRevision.FromUtf8Text(original);
        var payload = new AgentReplaceFileActionPayload(path, baseRevision, replacement);
        var proposal = CreateProposal(payload);

        var result = _mutator.Apply(_scope, proposal, payload, CancellationToken.None);

        Assert.Equal(AgentFileMutationOutcome.Succeeded, result.Outcome);
        Assert.Equal(replacement, File.ReadAllText(Path.Combine(_workspaceRoot, path.NormalizedPath)));
    }

    [Fact]
    public void Apply_Replace_ConflictsWhenBaseChanged()
    {
        var path = AgentWorkspaceRelativePath.Normalize("stale-replace.txt");
        File.WriteAllText(Path.Combine(_workspaceRoot, path.NormalizedPath), "changed on disk");
        var capturedBase = AgentContentRevision.FromUtf8Text("captured base");
        var payload = new AgentReplaceFileActionPayload(path, capturedBase, "replacement");
        var proposal = BuildManualProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: capturedBase,
            proposedRevision: payload.ProposedRevision);

        var result = _mutator.Apply(_scope, proposal, payload, CancellationToken.None);

        Assert.Equal(AgentFileMutationOutcome.Conflict, result.Outcome);
        Assert.Equal("changed on disk", File.ReadAllText(Path.Combine(_workspaceRoot, path.NormalizedPath)));
    }

    [Fact]
    public void Apply_Delete_RemovesFileWhenBaseMatches()
    {
        var path = AgentWorkspaceRelativePath.Normalize("delete-me.txt");
        const string content = "delete me";
        File.WriteAllText(Path.Combine(_workspaceRoot, path.NormalizedPath), content);
        var payload = new AgentDeleteFileActionPayload(
            path,
            AgentContentRevision.FromUtf8Text(content));
        var proposal = CreateProposal(payload);

        var result = _mutator.Apply(_scope, proposal, payload, CancellationToken.None);

        Assert.Equal(AgentFileMutationOutcome.Succeeded, result.Outcome);
        Assert.False(File.Exists(Path.Combine(_workspaceRoot, path.NormalizedPath)));
    }

    [Fact]
    public void Apply_Delete_ConflictsWhenBaseChanged()
    {
        var path = AgentWorkspaceRelativePath.Normalize("stale-delete.txt");
        File.WriteAllText(Path.Combine(_workspaceRoot, path.NormalizedPath), "newer");
        var capturedBase = AgentContentRevision.FromUtf8Text("older");
        var payload = new AgentDeleteFileActionPayload(path, capturedBase);
        var proposal = BuildManualProposal(
            AgentFileProposalOperation.Delete,
            path,
            baseExists: true,
            baseRevision: capturedBase,
            proposedRevision: null);

        var result = _mutator.Apply(_scope, proposal, payload, CancellationToken.None);

        Assert.Equal(AgentFileMutationOutcome.Conflict, result.Outcome);
        Assert.True(File.Exists(Path.Combine(_workspaceRoot, path.NormalizedPath)));
    }

    [Fact]
    public void Apply_StaleWorkspaceRoot_ReturnsPathEscaped()
    {
        StatDeviceInode(_workspaceRoot, out var dev, out var ino);
        var staleScope = new WorkspaceActionScope(
            _scope.Identity,
            _scope.Generation,
            _workspaceRoot,
            capturedCanonicalRoot: _workspaceRoot,
            capturedRootDevice: dev,
            capturedRootInode: ino);
        Directory.Delete(_workspaceRoot, recursive: true);
        Directory.CreateSymbolicLink(_workspaceRoot, _outsideRoot);
        try
        {
            var path = AgentWorkspaceRelativePath.Normalize("root-swap.txt");
            var payload = new AgentCreateFileActionPayload(path, "content");
            var proposal = BuildManualProposal(
                AgentFileProposalOperation.Create,
                path,
                baseExists: false,
                baseRevision: null,
                proposedRevision: payload.ProposedRevision,
                workspaceScope: staleScope);

            var result = _mutator.Apply(staleScope, proposal, payload, CancellationToken.None);

            Assert.Equal(AgentFileMutationOutcome.PathEscaped, result.Outcome);
        }
        finally
        {
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot);
            }

            Directory.CreateDirectory(_workspaceRoot);
        }
    }

    [Fact]
    public void Apply_SymlinkEscape_ReturnsPathEscaped()
    {
        var outsideFile = Path.Combine(_outsideRoot, "secret.txt");
        File.WriteAllText(outsideFile, "escaped");
        var linkPath = Path.Combine(_workspaceRoot, "escape-link.txt");
        File.CreateSymbolicLink(linkPath, outsideFile);

        var path = AgentWorkspaceRelativePath.Normalize("escape-link.txt");
        var payload = new AgentReplaceFileActionPayload(
            path,
            AgentContentRevision.FromUtf8Text("escaped"),
            "replacement");
        var proposal = BuildManualProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: payload.BaseRevision,
            proposedRevision: payload.ProposedRevision);

        var result = _mutator.Apply(_scope, proposal, payload, CancellationToken.None);

        Assert.Equal(AgentFileMutationOutcome.PathEscaped, result.Outcome);
        Assert.Equal("escaped", File.ReadAllText(outsideFile));
    }

    [Fact]
    public void Apply_CancelledBeforeApply_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("cancelled.txt"),
            "content");
        var proposal = CreateProposal(payload);

        var result = _mutator.Apply(_scope, proposal, payload, cts.Token);

        Assert.Equal(AgentFileMutationOutcome.Cancelled, result.Outcome);
        Assert.False(File.Exists(Path.Combine(_workspaceRoot, "cancelled.txt")));
    }

    [Fact]
    public void Apply_CancelledDuringWrite_ReturnsCancelledAndCleansTemp()
    {
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("cancel-write.txt"),
            "content");
        var proposal = CreateProposal(payload);
        using var cts = new CancellationTokenSource();
        _mutator.OnAfterValidationBeforeApply = cts.Cancel;

        var result = _mutator.Apply(_scope, proposal, payload, cts.Token);

        Assert.Equal(AgentFileMutationOutcome.Cancelled, result.Outcome);
        Assert.False(File.Exists(Path.Combine(_workspaceRoot, "cancel-write.txt")));
        Assert.Empty(Directory.GetFiles(_workspaceRoot, "*.zaide-tmp-*"));
    }

    private AgentFileActionProposal CreateProposal(AgentActionPayload payload)
    {
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("mutation-test");
        var generated = AgentFileProposalGenerator.CreateProposal(
            _scope,
            payload,
            _reader,
            fingerprint,
            CancellationToken.None);
        Assert.True(generated.IsSuccess);
        return generated.Proposal!;
    }

    private AgentFileActionProposal BuildManualProposal(
        AgentFileProposalOperation operation,
        AgentWorkspaceRelativePath path,
        bool baseExists,
        AgentContentRevision? baseRevision,
        AgentContentRevision? proposedRevision,
        WorkspaceActionScope? workspaceScope = null)
    {
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("mutation-test");
        return new AgentFileActionProposal(
            AgentFileProposalId.New(),
            new AgentFileProposal(
                operation,
                path,
                baseExists,
                baseRevision,
                proposedRevision,
                boundedChangeSummary: $"{operation} {path.NormalizedPath}"),
            workspaceScope ?? _scope,
            fingerprint,
            baseRevision);
    }

    private static void StatDeviceInode(string path, out ulong device, out ulong inode)
    {
        var buffer = new byte[256];
        Assert.Equal(0, Stat(path, buffer));
        device = BitConverter.ToUInt64(buffer, 0);
        inode = BitConverter.ToUInt64(buffer, 8);
    }

    [System.Runtime.InteropServices.DllImport("libc", SetLastError = true, EntryPoint = "stat")]
    private static extern int Stat(
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path,
        byte[] statBuffer);
}
