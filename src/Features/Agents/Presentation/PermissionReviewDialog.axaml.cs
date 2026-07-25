using System;
using Avalonia.Controls;
using Avalonia.Input;
using Zaide.Features.Agents.Application;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Permission review dialog window. Allow/Deny resolve through the bound
/// <see cref="PermissionReviewViewModel"/> commands; closing the window by
/// any other means resolves as a denial (deny-on-dismiss). Keyboard focus
/// starts on the Deny control so the fail-safe choice is the default.
/// </summary>
internal sealed partial class PermissionReviewDialog : Window
{
    public PermissionReviewDialog()
    {
        InitializeComponent();

        TransparencyLevelHint = new[]
        {
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur,
            WindowTransparencyLevel.Transparent
        };

        Opened += (_, _) => DenyButton.Focus(NavigationMethod.Tab);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Deny-on-dismiss: resolving is idempotent, so a close following an
        // explicit Allow/Deny does not overwrite the recorded decision.
        if (DataContext is PermissionReviewViewModel viewModel)
        {
            viewModel.ResolveDismiss();
        }
    }
}
