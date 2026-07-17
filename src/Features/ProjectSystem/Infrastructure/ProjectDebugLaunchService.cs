using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.App.Composition;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Features.ProjectSystem.Infrastructure;

/// <summary>
/// UI-independent build-to-debug handoff owner. Holds the debug handoff lease across
/// build, <c>TargetPath</c> resolution, and adapter launch.
/// </summary>
public sealed class ProjectDebugLaunchService : IProjectDebugLaunchService
{
    private readonly IProjectContextService _projectContext;
    private readonly IProjectOperationGate _operationGate;
    private readonly IProjectWorkflowService _workflow;
    private readonly IProjectDebugTargetResolver _targetResolver;
    private readonly IDebugSessionService _debugSession;
    private readonly IBreakpointService _breakpointService;
    private readonly ILogger<ProjectDebugLaunchService> _logger;

    public ProjectDebugLaunchService(
        IProjectContextService projectContext,
        IProjectOperationGate operationGate,
        IProjectWorkflowService workflow,
        IProjectDebugTargetResolver targetResolver,
        IDebugSessionService debugSession,
        IBreakpointService breakpointService,
        ILogger<ProjectDebugLaunchService> logger)
    {
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
        _operationGate = operationGate ?? throw new ArgumentNullException(nameof(operationGate));
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        _debugSession = debugSession ?? throw new ArgumentNullException(nameof(debugSession));
        _breakpointService = breakpointService ?? throw new ArgumentNullException(nameof(breakpointService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<DebugSessionOperationResult> StartDebuggingAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = _projectContext.Current;
        if (!IsDebugEligible(context))
        {
            return new DebugSessionOperationResult(
                false,
                DebugSessionOutcomeKind.RejectedContext,
                "The selected project is not eligible for debugging.");
        }

        var acquire = await _operationGate.TryAcquireDebugHandoffAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!acquire.IsSuccess)
        {
            return new DebugSessionOperationResult(
                false,
                DebugSessionOutcomeKind.RejectedConcurrent,
                acquire.Message);
        }

        var handoffLease = (IProjectOperationHandoffLease)acquire.Lease!;
        try
        {
            var buildResult = await _workflow
                .StartBuildForDebugHandoffAsync(handoffLease, cancellationToken)
                .ConfigureAwait(false);

            if (buildResult.Outcome != ProjectWorkflowOutcomeKind.Succeeded)
            {
                var buildMessage = MapBuildFailureMessage(buildResult.Outcome);
                await _debugSession
                    .ReportPreLaunchFailureAsync(
                        DebugSessionOutcomeKind.BuildFailed,
                        buildMessage,
                        cancellationToken)
                    .ConfigureAwait(false);
                return new DebugSessionOperationResult(
                    false,
                    DebugSessionOutcomeKind.BuildFailed,
                    buildMessage);
            }

            var csprojPath = buildResult.TargetFilePath
                ?? context.SelectedProject!.FilePath;
            var resolution = await _targetResolver
                .ResolveTargetPathAsync(csprojPath, cancellationToken)
                .ConfigureAwait(false);

            if (!resolution.IsSuccess)
            {
                var targetMessage = resolution.Message ?? "Debug target could not be resolved.";
                await _debugSession
                    .ReportPreLaunchFailureAsync(
                        DebugSessionOutcomeKind.UnsupportedLaunchTarget,
                        targetMessage,
                        cancellationToken)
                    .ConfigureAwait(false);
                return new DebugSessionOperationResult(
                    false,
                    DebugSessionOutcomeKind.UnsupportedLaunchTarget,
                    targetMessage);
            }

            var workingDirectory = Path.GetDirectoryName(Path.GetFullPath(csprojPath))
                ?? Path.GetFullPath(csprojPath);

            var breakpoints = _breakpointService.GetBreakpoints()
                .Where(bp => bp.Enabled)
                .Select(bp => new DebugBreakpointRequest(bp.SourcePath, bp.Line))
                .ToArray();

            var launch = new DebugLaunchRequest(
                resolution.TargetPath!,
                workingDirectory,
                StopAtEntry: true,
                breakpoints);

            var launchResult = await _debugSession
                .StartLaunchAsync(launch, cancellationToken)
                .ConfigureAwait(false);

            return launchResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Debug launch handoff failed.");
            return new DebugSessionOperationResult(
                false,
                DebugSessionOutcomeKind.StartupFailed,
                "Debug launch failed.");
        }
        finally
        {
            handoffLease.Dispose();
        }
    }

    private static bool IsDebugEligible(ProjectContext context) =>
        ProjectTargetResolver.IsEligible(context) &&
        context.SelectedProject?.Kind == ProjectKind.CSharpProject;

    private static string MapBuildFailureMessage(ProjectWorkflowOutcomeKind outcome) =>
        outcome switch
        {
            ProjectWorkflowOutcomeKind.Failed => "Build failed.",
            ProjectWorkflowOutcomeKind.StartupFailed => "Build could not start.",
            ProjectWorkflowOutcomeKind.Cancelled => "Build was cancelled.",
            ProjectWorkflowOutcomeKind.RejectedConcurrent => ProjectOperationGateMessages.WorkflowBusy,
            ProjectWorkflowOutcomeKind.RejectedContext => "The selected project is not eligible for debugging.",
            _ => "Build failed.",
        };
}