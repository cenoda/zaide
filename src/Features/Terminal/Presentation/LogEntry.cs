using System;

namespace Zaide.Features.Terminal.Presentation;

/// <summary>
/// Represents a single categorized log entry in the terminal/logs panel.
/// </summary>
public enum LogCategory
{
    Build,
    Agent,
    Log
}

/// <summary>
/// A single categorized log entry with content, category, timestamp, and warning state.
/// </summary>
public class LogEntry
{
    public int Id { get; }
    public LogCategory Category { get; }
    public string Content { get; }
    public DateTimeOffset Timestamp { get; }
    public bool HasWarning { get; }

    public LogEntry(int id, LogCategory category, string content, DateTimeOffset timestamp, bool hasWarning = false)
    {
        Id = id;
        Category = category;
        Content = content;
        Timestamp = timestamp;
        HasWarning = hasWarning;
    }

    /// <summary>
    /// Returns the tag string for display, e.g. "[BUILD]", "[AGENT]", "[LOG]".
    /// </summary>
    public string Tag => Category switch
    {
        LogCategory.Build => "[BUILD]",
        LogCategory.Agent => "[AGENT]",
        LogCategory.Log => "[LOG]",
        _ => "[LOG]"
    };
}