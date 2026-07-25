using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Production presenter that creates and shows the
/// <see cref="PermissionReviewDialog"/> modally over the owner window.
/// The owner is attached by the application shell after the main window is
/// created (see <c>App.OnFrameworkInitializationCompleted</c>). While no
/// owner is attached the presenter fails closed by throwing, which the
/// broker classifies as <c>PermissionUnavailable</c>.
/// </summary>
internal sealed class PermissionReviewDialogPresenter : IAgentPermissionDialogPresenter
{
    private Window? _owner;

    /// <summary>
    /// Attaches the owner window for modal dialog display. Must be called
    /// from the UI thread after the main window is created.
    /// </summary>
    public void SetOwner(Window owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <inheritdoc/>
    public async Task<bool> ShowAsync(
        AgentActionRequest request,
        AgentActionDisplaySummary displaySummary,
        WorkspaceActionScope? workspaceScope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var owner = _owner
            ?? throw new InvalidOperationException(
                "The permission review surface has no owner window; the request cannot be reviewed.");

        if (Dispatcher.UIThread.CheckAccess())
        {
            return await ShowDialogOnUiThreadAsync(
                owner, request, displaySummary, workspaceScope, cancellationToken)
                .ConfigureAwait(false);
        }

        return await Dispatcher.UIThread.InvokeAsync(
            () => ShowDialogOnUiThreadAsync(
                owner, request, displaySummary, workspaceScope, cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task<bool> ShowDialogOnUiThreadAsync(
        Window owner,
        AgentActionRequest request,
        AgentActionDisplaySummary displaySummary,
        WorkspaceActionScope? workspaceScope,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new PermissionReviewDialog();
        var viewModel = new PermissionReviewViewModel(
            request,
            displaySummary,
            workspaceScope,
            result =>
            {
                // First resolution wins; dismiss after Allow/Deny is a no-op.
                completion.TrySetResult(result);
                dialog.Close();
            });

        dialog.DataContext = viewModel;

        // Cancellation during the dialog wins over deny-on-dismiss: complete
        // as cancelled first, then close the dialog so the dismiss resolver
        // cannot record a user denial that never happened.
        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            completion.TrySetCanceled(cancellationToken);
            Dispatcher.UIThread.Post(dialog.Close);
        });

        _ = dialog.ShowDialog(owner);
        return await completion.Task.ConfigureAwait(true);
    }
}
