using System;
using System.Reactive.Subjects;
using Moq;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests;

/// <summary>
/// Shared factory for editor breakpoint projection in composition tests.
/// </summary>
internal static class TestEditorBreakpointFactory
{
    public static EditorBreakpointViewModel Create(
        EditorTabViewModel editorTabs,
        ICommandRegistry? registry = null,
        IBreakpointService? breakpointService = null,
        IDebugSessionService? debugSession = null,
        IProjectContextService? projectContext = null,
        ISettingsService? settings = null)
    {
        var breakpoints = breakpointService ?? CreateBreakpointService();
        var debug = debugSession ?? TestOperationGateFactory.CreateIdleDebugSession().Object;
        var context = projectContext ?? CreateDefaultProjectContext();
        var settingsService = settings ?? CreateDefaultSettings();

        return new EditorBreakpointViewModel(
            editorTabs,
            breakpoints,
            debug,
            context,
            settingsService,
            registry);
    }

    private static IBreakpointService CreateBreakpointService()
    {
        var mock = new Mock<IBreakpointService>();
        mock.Setup(s => s.GetBreakpoints()).Returns(Array.Empty<PersistedBreakpoint>());
        mock.Setup(s => s.MapToDapReplacementBySource(It.IsAny<System.Collections.Generic.IReadOnlyCollection<string>>()))
            .Returns(new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<int>>());
        return mock.Object;
    }

    private static IProjectContextService CreateDefaultProjectContext()
    {
        var mock = new Mock<IProjectContextService>();
        var context = new ProjectContext(
            ProjectContextState.NoProject,
            "/tmp/workspace",
            Array.Empty<ProjectCandidate>(),
            null,
            Array.Empty<string>(),
            null);
        mock.SetupGet(s => s.Current).Returns(context);
        mock.SetupGet(s => s.WhenChanged).Returns(new Subject<ProjectContext>());
        return mock.Object;
    }

    private static ISettingsService CreateDefaultSettings()
    {
        var mock = new Mock<ISettingsService>();
        mock.SetupGet(s => s.Current).Returns(SettingsModel.Defaults);
        mock.SetupGet(s => s.WhenChanged).Returns(new Subject<SettingsModel>());
        return mock.Object;
    }
}