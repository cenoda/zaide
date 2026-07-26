using System;

namespace Zaide.Features.Terminal.Application;

/// <summary>
/// Contract-level terminal surface summary for IDE context assembly. Raw
/// scrollback content is intentionally excluded from this snapshot shape.
/// </summary>
public sealed class TerminalSurfaceSnapshot
{
    public TerminalSurfaceSnapshot(
        long generation,
        int activeTabCount = 0,
        string? activeTabTitle = null,
        bool isActiveTabRunning = false,
        int visibleRowCount = 0,
        int visibleColumnCount = 0)
    {
        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generation),
                generation,
                "Generation cannot be negative.");
        }

        if (activeTabCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeTabCount),
                activeTabCount,
                "Active tab count cannot be negative.");
        }

        if (visibleRowCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visibleRowCount),
                visibleRowCount,
                "Visible row count cannot be negative.");
        }

        if (visibleColumnCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visibleColumnCount),
                visibleColumnCount,
                "Visible column count cannot be negative.");
        }

        Generation = generation;
        ActiveTabCount = activeTabCount;
        ActiveTabTitle = activeTabTitle;
        IsActiveTabRunning = isActiveTabRunning;
        VisibleRowCount = visibleRowCount;
        VisibleColumnCount = visibleColumnCount;
    }

    public long Generation { get; }

    public int ActiveTabCount { get; }

    public string? ActiveTabTitle { get; }

    public bool IsActiveTabRunning { get; }

    public int VisibleRowCount { get; }

    public int VisibleColumnCount { get; }
}
