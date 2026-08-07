using Avalonia;
using Avalonia.Controls;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Interactive list/tree row host using <see cref="ControlThemeCatalog"/> overlay
/// tokens and selection styling.
/// </summary>
internal static class ListRow
{
    /// <summary>Default row padding aligned with file-tree and source-control rows.</summary>
    internal static Thickness DefaultPadding =>
        LayoutTokens.Inset(
            LayoutTokens.SpacingXs,
            LayoutTokens.SpacingXxs,
            LayoutTokens.SpacingSm,
            LayoutTokens.SpacingXxs);

    /// <summary>
    /// Creates a focusable row <see cref="Border"/> wired to the interactive surface theme.
    /// </summary>
    internal static Border Create(Control child, Thickness? padding = null, object? tag = null)
    {
        var row = new Border
        {
            Child = child,
            Padding = padding ?? DefaultPadding,
            Tag = tag,
            Focusable = true,
            IsTabStop = true,
        };

        ControlThemeCatalog.ApplyInteractiveSurface(row);
        return row;
    }

    /// <summary>Toggles the shared selected overlay class on a row host.</summary>
    internal static void SetSelected(Border row, bool selected)
    {
        if (selected)
            row.Classes.Add(ControlThemeCatalog.SelectedClass);
        else
            row.Classes.Remove(ControlThemeCatalog.SelectedClass);
    }
}
