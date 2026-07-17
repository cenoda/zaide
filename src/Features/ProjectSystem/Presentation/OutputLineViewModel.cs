using System;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Presentation;

/// <summary>
/// One read-only structured output line for the Output panel.
/// </summary>
public sealed class OutputLineViewModel
{
    public OutputLineViewModel(ManagedProcessOutputLine line)
    {
        StreamTag = line.Stream == ProcessStreamKind.StdErr ? "stderr" : "stdout";
        Text = line.Text;
        TimestampText = line.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        DisplayText = $"[{TimestampText}] [{StreamTag}] {Text}";
    }

    public string StreamTag { get; }

    public string Text { get; }

    public string TimestampText { get; }

    public string DisplayText { get; }
}
