using System;
using System.Collections.Generic;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 11 M6 / U6: workflow status text distinguishes Build, Run, and Test.
/// </summary>
public sealed class ProjectWorkflowStatusPolicyTests
{
    [Theory]
    [InlineData(ProjectWorkflowOperation.Build, "Building")]
    [InlineData(ProjectWorkflowOperation.Run, "Running")]
    [InlineData(ProjectWorkflowOperation.Test, "Testing")]
    public void MapOutputStatusMessage_InProgress_UsesOperationVerb(
        ProjectWorkflowOperation operation,
        string expectedVerb)
    {
        var snapshot = Active(operation, ProjectWorkflowOperationState.Running);

        var message = ProjectWorkflowStatusPolicy.MapOutputStatusMessage(snapshot);

        Assert.NotNull(message);
        Assert.StartsWith(expectedVerb, message, StringComparison.Ordinal);
        Assert.Contains("/tmp/app.csproj", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProjectWorkflowOperation.Build, ProjectWorkflowOutcomeKind.Succeeded, "Build succeeded.")]
    [InlineData(ProjectWorkflowOperation.Build, ProjectWorkflowOutcomeKind.Failed, "Build failed.")]
    [InlineData(ProjectWorkflowOperation.Build, ProjectWorkflowOutcomeKind.StartupFailed, "Build could not start.")]
    [InlineData(ProjectWorkflowOperation.Build, ProjectWorkflowOutcomeKind.Cancelled, "Build cancelled.")]
    [InlineData(ProjectWorkflowOperation.Run, ProjectWorkflowOutcomeKind.Succeeded, "Run succeeded.")]
    [InlineData(ProjectWorkflowOperation.Run, ProjectWorkflowOutcomeKind.Failed, "Run failed.")]
    [InlineData(ProjectWorkflowOperation.Run, ProjectWorkflowOutcomeKind.StartupFailed, "Run could not start.")]
    [InlineData(ProjectWorkflowOperation.Run, ProjectWorkflowOutcomeKind.Cancelled, "Run cancelled.")]
    [InlineData(ProjectWorkflowOperation.Test, ProjectWorkflowOutcomeKind.Succeeded, "Tests succeeded.")]
    [InlineData(ProjectWorkflowOperation.Test, ProjectWorkflowOutcomeKind.Failed, "Tests failed.")]
    [InlineData(ProjectWorkflowOperation.Test, ProjectWorkflowOutcomeKind.StartupFailed, "Tests could not start.")]
    [InlineData(ProjectWorkflowOperation.Test, ProjectWorkflowOutcomeKind.Cancelled, "Tests cancelled.")]
    public void MapOutputStatusMessage_Terminal_UsesLastOperationNoun(
        ProjectWorkflowOperation lastOperation,
        ProjectWorkflowOutcomeKind outcome,
        string expected)
    {
        var snapshot = Terminal(outcome, lastOperation);

        Assert.Equal(expected, ProjectWorkflowStatusPolicy.MapOutputStatusMessage(snapshot));
    }

    [Theory]
    [InlineData(ProjectWorkflowOperation.Build, "Cancel build")]
    [InlineData(ProjectWorkflowOperation.Run, "Cancel run")]
    [InlineData(ProjectWorkflowOperation.Test, "Cancel tests")]
    public void MapCancelAutomationName_InProgress_UsesActiveOperation(
        ProjectWorkflowOperation operation,
        string expected)
    {
        var snapshot = Active(operation, ProjectWorkflowOperationState.Running);

        Assert.Equal(expected, ProjectWorkflowStatusPolicy.MapCancelAutomationName(snapshot));
    }

    [Theory]
    [InlineData(ProjectWorkflowOperation.Build, "Cancel build")]
    [InlineData(ProjectWorkflowOperation.Run, "Cancel run")]
    [InlineData(ProjectWorkflowOperation.Test, "Cancel tests")]
    public void MapCancelAutomationName_Terminal_UsesLastOperation(
        ProjectWorkflowOperation lastOperation,
        string expected)
    {
        var snapshot = Terminal(ProjectWorkflowOutcomeKind.Cancelled, lastOperation);

        Assert.Equal(expected, ProjectWorkflowStatusPolicy.MapCancelAutomationName(snapshot));
    }

    [Fact]
    public void MapOutputStatusMessage_IdleWithoutOutcome_ReturnsNull()
    {
        var snapshot = new ProjectOutputSnapshot(
            Generation: 0,
            ProjectWorkflowOperationState.Idle,
            ActiveOperation: null,
            LastOutcome: null,
            TargetFilePath: null,
            Lines: Array.Empty<ManagedProcessOutputLine>(),
            LastOperation: null);

        Assert.Null(ProjectWorkflowStatusPolicy.MapOutputStatusMessage(snapshot));
    }

    [Fact]
    public void MapOutputStatusMessage_TerminalWithoutLastOperation_DefaultsToBuildNoun()
    {
        // Pre-M6 snapshots or synthetic idle cancels without LastOperation.
        var snapshot = new ProjectOutputSnapshot(
            Generation: 1,
            ProjectWorkflowOperationState.Idle,
            ActiveOperation: null,
            LastOutcome: ProjectWorkflowOutcomeKind.Cancelled,
            TargetFilePath: "/tmp/app.csproj",
            Lines: Array.Empty<ManagedProcessOutputLine>(),
            LastOperation: null);

        Assert.Equal("Build cancelled.", ProjectWorkflowStatusPolicy.MapOutputStatusMessage(snapshot));
    }

    private static ProjectOutputSnapshot Active(
        ProjectWorkflowOperation operation,
        ProjectWorkflowOperationState state) =>
        new(
            Generation: 1,
            state,
            ActiveOperation: operation,
            LastOutcome: null,
            TargetFilePath: "/tmp/app.csproj",
            Lines: Array.Empty<ManagedProcessOutputLine>(),
            LastOperation: operation);

    private static ProjectOutputSnapshot Terminal(
        ProjectWorkflowOutcomeKind outcome,
        ProjectWorkflowOperation lastOperation) =>
        new(
            Generation: 1,
            ProjectWorkflowOperationState.Idle,
            ActiveOperation: null,
            LastOutcome: outcome,
            TargetFilePath: "/tmp/app.csproj",
            Lines: Array.Empty<ManagedProcessOutputLine>(),
            LastOperation: lastOperation);
}
