using System;
using System.IO;
using System.Reactive;
using System.Threading;
using ReactiveUI;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// ViewModel for the permission review dialog. Pure MVVM, no UI control
/// references. Displays both the normalized workspace-relative path and the
/// resolved absolute path, re-validating workspace containment before
/// exposing the absolute path.
/// </summary>
internal sealed class PermissionReviewViewModel : ReactiveObject
{
    /// <summary>Displayed when no captured workspace scope is available.</summary>
    internal const string NoWorkspaceScopeText = "(unavailable: no captured workspace scope)";

    /// <summary>Displayed when the resolved path cannot be confirmed beneath the captured root.</summary>
    internal const string EscapedPathText = "(withheld: resolved path is not beneath the captured workspace root)";

    private readonly Action<bool> _resolver;
    private int _resolutionState;

    public PermissionReviewViewModel(
        AgentActionRequest request,
        AgentActionDisplaySummary displaySummary,
        WorkspaceActionScope? workspaceScope,
        Action<bool> resolver)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        DisplaySummary = displaySummary ?? throw new ArgumentNullException(nameof(displaySummary));
        WorkspaceScope = workspaceScope;
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

        AllowCommand = ReactiveCommand.Create(() => Resolve(true));
        DenyCommand = ReactiveCommand.Create(() => Resolve(false));
        DismissCommand = ReactiveCommand.Create(() => Resolve(false)); // Deny-on-dismiss
    }

    public AgentActionRequest Request { get; }

    public AgentActionDisplaySummary DisplaySummary { get; }

    public WorkspaceActionScope? WorkspaceScope { get; }

    public string InitiatingActorId => Request.InitiatingActorId.Value;

    public string TargetActorId => Request.TargetActorId.Value;

    public string BackendId => Request.BackendId.Value;

    public string RunId => Request.RunId.Value;

    public string ActionKind => Request.Payload.Kind.ToString();

    public string TargetText => Request.Payload switch
    {
        AgentReadFileActionPayload r => r.Path.NormalizedPath,
        AgentCreateFileActionPayload c => c.Path.NormalizedPath,
        AgentReplaceFileActionPayload rp => rp.Path.NormalizedPath,
        AgentDeleteFileActionPayload d => d.Path.NormalizedPath,
        AgentExecuteCommandActionPayload cmd => $"{cmd.Executable} {string.Join(" ", cmd.Arguments)}",
        _ => "Unknown",
    };

    public string NormalizedPathText => Request.Payload switch
    {
        AgentReadFileActionPayload r => r.Path.NormalizedPath,
        AgentCreateFileActionPayload c => c.Path.NormalizedPath,
        AgentReplaceFileActionPayload rp => rp.Path.NormalizedPath,
        AgentDeleteFileActionPayload d => d.Path.NormalizedPath,
        AgentExecuteCommandActionPayload cmd => cmd.WorkingDirectory.NormalizedPath,
        _ => string.Empty,
    };

    /// <summary>
    /// Resolved absolute path for display, built from the captured canonical
    /// workspace root. Containment beneath the captured canonical root is
    /// re-validated before the absolute path is displayed; when the scope is
    /// unavailable or containment cannot be confirmed, an explicit
    /// fail-closed marker is displayed instead of a fabricated path.
    /// </summary>
    public string ResolvedPathText
    {
        get
        {
            if (WorkspaceScope is null)
            {
                return NoWorkspaceScopeText;
            }

            try
            {
                var root = WorkspaceScope.CapturedCanonicalRoot;
                var combined = Path.GetFullPath(Path.Combine(root, NormalizedPathText));

                // Re-validate containment before displaying the absolute path.
                var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
                    ? root
                    : root + Path.DirectorySeparatorChar;

                if (combined.Length > rootWithSep.Length
                    && combined.StartsWith(rootWithSep, StringComparison.Ordinal))
                {
                    return combined;
                }
            }
            catch (ArgumentException)
            {
                // Invalid path characters: fall through to the fail-closed marker.
            }

            return EscapedPathText;
        }
    }

    public string DisplaySummaryText => DisplaySummary.DetailText;

    public string ScopeText => "Scope: this exact request only.";

    public string ContainmentDisclosureText => Request.Payload.Kind == AgentActionKind.ExecuteCommand
        ? "Working-directory scope is not filesystem or network sandboxing."
        : string.Empty;

    public ReactiveCommand<Unit, Unit> AllowCommand { get; }

    public ReactiveCommand<Unit, Unit> DenyCommand { get; }

    public ReactiveCommand<Unit, Unit> DismissCommand { get; }

    /// <summary>Deny-on-dismiss entry point invoked when the dialog closes.</summary>
    public void ResolveDismiss() => Resolve(false);

    /// <summary>
    /// Resolves exactly once; the first resolution (explicit Allow, explicit
    /// Deny, or dismiss) wins and later calls are ignored, so a window close
    /// following an explicit choice cannot overwrite the recorded decision.
    /// </summary>
    private void Resolve(bool isAllowed)
    {
        if (Interlocked.Exchange(ref _resolutionState, 1) == 0)
        {
            _resolver(isAllowed);
        }
    }
}
