using System;
using Moq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Infrastructure;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.SourceControl.Application;

public sealed class SourceControlDiffTabServiceTests
{
    static SourceControlDiffTabServiceTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void Format_BinaryFile_ReturnsNotice()
    {
        var change = new FileChange("app.dll", GitChangeType.Modified, isStaged: false);
        var result = new FileDiffResult { FilePath = "app.dll", IsBinary = true };

        Assert.Equal("Binary file — diff not available", SourceControlDiffContent.Format(change, result));
    }

    [Fact]
    public void Format_NullDiff_ReturnsGracefulNotice()
    {
        var change = new FileChange("missing.txt", GitChangeType.Deleted, isStaged: false);

        Assert.Equal(
            "No diff available for missing.txt",
            SourceControlDiffContent.Format(change, diff: null));
    }

    [Fact]
    public void ToVirtualPath_DoesNotCollideWithWorkspacePaths()
    {
        Assert.Equal(
            "zaide-sc-diff://src/Program.cs",
            SourceControlDiffTabKey.ToVirtualPath("src/Program.cs"));
    }

    [Fact]
    public void OpenOrUpdateDiff_CreatesReadOnlyTabViaGateway()
    {
        var (service, editorTabs) = CreateService(
            diffSetup: d => d.Setup(x => x.GetDiff(It.IsAny<string>(), It.IsAny<FileChange>()))
                .Returns(new FileDiffResult
                {
                    FilePath = "a.cs",
                    DiffText = "diff --git a/a.cs b/a.cs\n+hello",
                }));

        service.OpenOrUpdateDiff(new FileChange("a.cs", GitChangeType.Modified, isStaged: false));

        var tab = Assert.Single(editorTabs.OpenTabs);
        Assert.True(tab.IsReadOnly);
        Assert.True(tab.IsSourceControlDiff);
        Assert.Equal("a.cs", tab.SourceControlDiffKey);
        Assert.Equal("Changes", tab.SourceControlComparisonState);
        Assert.Contains("+hello", tab.TextContent, StringComparison.Ordinal);
        Assert.Same(tab, editorTabs.ActiveTab);
    }

    [Fact]
    public void OpenOrUpdateDiff_ReusesExistingTabByReuseKey()
    {
        var (service, editorTabs) = CreateService(
            diffSetup: d => d.SetupSequence(x => x.GetDiff(It.IsAny<string>(), It.IsAny<FileChange>()))
                .Returns(new FileDiffResult { FilePath = "a.cs", DiffText = "first" })
                .Returns(new FileDiffResult { FilePath = "a.cs", DiffText = "second" }));

        var change = new FileChange("a.cs", GitChangeType.Modified, isStaged: false);
        service.OpenOrUpdateDiff(change);
        service.OpenOrUpdateDiff(change);

        Assert.Single(editorTabs.OpenTabs);
        Assert.Contains("second", editorTabs.OpenTabs[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshOpenDiff_UpdatesContentWhenTabOpen()
    {
        var (service, editorTabs) = CreateService(
            diffSetup: d => d.SetupSequence(x => x.GetDiff(It.IsAny<string>(), It.IsAny<FileChange>()))
                .Returns(new FileDiffResult { FilePath = "a.cs", DiffText = "before" })
                .Returns(new FileDiffResult { FilePath = "a.cs", DiffText = "after-stage" }));

        service.OpenOrUpdateDiff(new FileChange("a.cs", GitChangeType.Modified, isStaged: false));
        service.RefreshOpenDiff(
            "a.cs",
            new FileChange("a.cs", GitChangeType.Modified, isStaged: true));

        var tab = Assert.Single(editorTabs.OpenTabs);
        Assert.Contains("after-stage", tab.TextContent, StringComparison.Ordinal);
        Assert.Equal("Staged Changes", tab.SourceControlComparisonState);
    }

    [Fact]
    public void RefreshOpenDiff_NoOpenTab_IsNoOp()
    {
        var (service, editorTabs) = CreateService();

        service.RefreshOpenDiff(
            "missing.cs",
            new FileChange("missing.cs", GitChangeType.Modified, isStaged: false));

        Assert.Empty(editorTabs.OpenTabs);
    }

    [Fact]
    public void SourceControlDiffTabService_HasNoEditorPresentationDependency()
    {
        var source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(
                FindRepoRoot(),
                "src/Features/SourceControl/Application/SourceControlDiffTabService.cs"));

        Assert.DoesNotContain("IServiceProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Zaide.Features.Editor.Presentation", source, StringComparison.Ordinal);
        Assert.Contains("IEditorReadOnlyTabService", source, StringComparison.Ordinal);
    }

    private static (SourceControlDiffTabService Service, EditorTabViewModel EditorTabs) CreateService(
        Action<Mock<IFileDiffService>>? diffSetup = null)
    {
        var workspace = new global::Zaide.Features.Workspace.Domain.Workspace();
        workspace.SetProjectFromPath("/tmp/repo");

        var fileService = new FileService();
        var sessionFactory = new EditorSessionFactory(fileService);
        var editorTabs = new EditorTabViewModel(sessionFactory, fileService, workspace);
        var readOnlyTabs = new EditorReadOnlyTabService(editorTabs, sessionFactory, workspace);

        var diff = new Mock<IFileDiffService>();
        diffSetup?.Invoke(diff);

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/tmp/repo", "/tmp/repo/.git"));

        var service = new SourceControlDiffTabService(
            readOnlyTabs,
            diff.Object,
            workspace,
            git.Object);

        return (service, editorTabs);
    }

    private static string FindRepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Zaide.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
