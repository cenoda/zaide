using System;
using System.IO;

namespace Zaide.Tests.Infrastructure;

/// <summary>
/// Resolves committed read-only fixture trees under <c>tests/fixtures</c>.
/// Do not write to these paths from tests.
/// </summary>
public static class TestFixturePaths
{
    private static readonly string FixturesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures"));

    public static string FixturesDirectory => FixturesRoot;

    public static string WorkflowConsole => Path.Combine(FixturesRoot, "workflow-console");

    public static string WorkflowConsoleProject =>
        Path.Combine(WorkflowConsole, "WorkflowConsole.csproj");

    public static string WorkflowConsoleProgram =>
        Path.Combine(WorkflowConsole, "Program.cs");

    public static string WorkflowFailBuild => Path.Combine(FixturesRoot, "workflow-fail-build");

    public static string WorkflowFailBuildProject =>
        Path.Combine(WorkflowFailBuild, "WorkflowFailBuild.csproj");

    public static string WorkflowTestsPass => Path.Combine(FixturesRoot, "workflow-tests-pass");

    public static string WorkflowTestsPassProject =>
        Path.Combine(WorkflowTestsPass, "WorkflowTestsPass.csproj");

    public static string WorkflowTestsFail => Path.Combine(FixturesRoot, "workflow-tests-fail");

    public static string WorkflowTestsFailProject =>
        Path.Combine(WorkflowTestsFail, "WorkflowTestsFail.csproj");
}
