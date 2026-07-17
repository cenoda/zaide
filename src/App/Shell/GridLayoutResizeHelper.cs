using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.App.Shell;
/// <summary>
/// Restores proportional GridLength values after a GridSplitter drag so later
/// window resizes keep behaving naturally instead of preserving stale pixel widths.
/// </summary>
public static class GridLayoutResizeHelper
{
    public static void PreservePixelColumnAndNormalizeStarColumns(
        Grid grid,
        int pixelColumnIndex,
        params int[] starColumnIndices)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (!TryGetColumn(grid, pixelColumnIndex, out var pixelColumn))
        {
            return;
        }

        pixelColumn.Width = new GridLength(Clamp(pixelColumn.ActualWidth, pixelColumn.MinWidth, pixelColumn.MaxWidth));
        NormalizeStarColumns(grid, starColumnIndices);
    }

    public static void NormalizeStarColumns(Grid grid, params int[] columnIndices)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var columns = GetColumns(grid, columnIndices);
        if (columns.Count == 0)
        {
            return;
        }

        var widths = columns
            .Select(column => Math.Max(column.ActualWidth, column.MinWidth))
            .ToArray();

        var totalWidth = widths.Sum();
        if (totalWidth <= 0)
        {
            return;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].Width = new GridLength(widths[i] / totalWidth, GridUnitType.Star);
        }
    }

    private static bool TryGetColumn(Grid grid, int columnIndex, out ColumnDefinition column)
    {
        column = null!;

        if (columnIndex < 0 || columnIndex >= grid.ColumnDefinitions.Count)
        {
            return false;
        }

        column = grid.ColumnDefinitions[columnIndex];
        return true;
    }

    private static List<ColumnDefinition> GetColumns(Grid grid, IEnumerable<int> columnIndices)
    {
        var columns = new List<ColumnDefinition>();

        foreach (var columnIndex in columnIndices.Distinct())
        {
            if (TryGetColumn(grid, columnIndex, out var column))
            {
                columns.Add(column);
            }
        }

        return columns;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return min;
        }

        if (max > 0)
        {
            return Math.Clamp(value, min, max);
        }

        return Math.Max(value, min);
    }
}
