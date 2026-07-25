using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Focused tests for <see cref="PermissionReviewViewModel"/> path display:
/// the review surface must show both the normalized workspace-relative path
/// and the resolved absolute path, and must re-validate containment beneath
/// the captured canonical root before displaying the absolute path.
/// </summary>
public sealed class Phase17PermissionReviewViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceActionScope _scope;

    public Phase17PermissionReviewViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "zaide-p17-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private AgentActionRequest ComposeRequest(AgentActionPayload payload) =>
        AgentActionRequestComposer.Compose(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("agent-target"),
            AgentBackendId.FromValue("backend:test"),
            _scope.Identity,
            _scope.Generation,
            new FakeTrustedCommandResolver(),
            payload);

    private PermissionReviewViewModel CreateViewModel(
        AgentActionPayload payload,
        WorkspaceActionScope? scope,
        Action<bool>? resolver = null)
    {
        var request = ComposeRequest(payload);
        return new PermissionReviewViewModel(
            request,
            AgentActionDisplaySummaryBuilder.Build(request.Payload),
            scope,
            resolver ?? (_ => { }));
    }

    [Fact]
    public void DisplaysBothNormalizedRelativePathAndResolvedAbsolutePath()
    {
        var viewModel = CreateViewModel(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("src/new.txt"), "content"),
            _scope);

        Assert.Equal("src/new.txt", viewModel.NormalizedPathText);

        var expectedAbsolute = Path.GetFullPath(Path.Combine(_root, "src/new.txt"));
        Assert.Equal(expectedAbsolute, viewModel.ResolvedPathText);
        Assert.True(Path.IsPathRooted(viewModel.ResolvedPathText));
        Assert.StartsWith(
            _scope.CapturedCanonicalRoot + Path.DirectorySeparatorChar,
            viewModel.ResolvedPathText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvedPath_WithoutWorkspaceScope_ShowsFailClosedMarker()
    {
        var viewModel = CreateViewModel(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            scope: null);

        Assert.Equal("new.txt", viewModel.NormalizedPathText);
        Assert.Equal(PermissionReviewViewModel.NoWorkspaceScopeText, viewModel.ResolvedPathText);
        Assert.False(Path.IsPathRooted(viewModel.ResolvedPathText));
    }

    [Fact]
    public void ResolvedPath_ContainmentNotConfirmed_WithholdsAbsolutePath()
    {
        // A non-canonical captured root ("<root>/sub/..") makes the resolved
        // full path fail the ordinal containment re-validation, so the
        // absolute path must be withheld rather than displayed.
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        var nonCanonicalRoot = Path.Combine(_root, "sub", "..");
        var scope = new WorkspaceActionScope(
            WorkspaceIdentity.New(),
            WorkspaceGeneration.Initial,
            _root,
            capturedCanonicalRoot: nonCanonicalRoot,
            capturedRootDevice: _scope.CapturedRootDevice,
            capturedRootInode: _scope.CapturedRootInode);

        var viewModel = CreateViewModel(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            scope);

        Assert.Equal("new.txt", viewModel.NormalizedPathText);
        Assert.Equal(PermissionReviewViewModel.EscapedPathText, viewModel.ResolvedPathText);
    }

    [Fact]
    public void DisplaysFixedScopeAndRequestIdentityFields()
    {
        var viewModel = CreateViewModel(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            _scope);

        Assert.Equal("Scope: this exact request only.", viewModel.ScopeText);
        Assert.Equal("CreateFile", viewModel.ActionKind);
        Assert.Equal(ActorId.HumanUser.Value, viewModel.InitiatingActorId);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.RunId));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.BackendId));
    }

    [Fact]
    public async Task AllowDenyAndDismiss_ResolveExactlyOnce_FirstWins()
    {
        var resolutions = new System.Collections.Generic.List<bool>();
        var allowViewModel = CreateViewModel(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            _scope,
            resolutions.Add);

        await allowViewModel.AllowCommand.Execute();

        // Deny-on-dismiss after an explicit Allow must not overwrite or
        // duplicate the recorded decision (single resolution, first wins).
        allowViewModel.ResolveDismiss();

        Assert.Equal(new[] { true }, resolutions);

        resolutions.Clear();
        var denyViewModel = CreateViewModel(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            _scope,
            resolutions.Add);

        await denyViewModel.DenyCommand.Execute();
        denyViewModel.ResolveDismiss();

        Assert.Equal(new[] { false }, resolutions);

        resolutions.Clear();
        var dismissViewModel = CreateViewModel(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("new.txt"), "content"),
            _scope,
            resolutions.Add);

        dismissViewModel.ResolveDismiss();
        dismissViewModel.ResolveDismiss();

        Assert.Equal(new[] { false }, resolutions);
    }
}
