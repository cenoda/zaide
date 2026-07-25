using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Bounded capture of one redirected command stream.
/// </summary>
internal sealed class AgentCommandStreamCapture
{
    private AgentCommandStreamCapture(
        string text,
        int byteCount,
        int lineCount,
        bool wasTruncated,
        bool containsInvalidText)
    {
        Text = text;
        ByteCount = byteCount;
        LineCount = lineCount;
        WasTruncated = wasTruncated;
        ContainsInvalidText = containsInvalidText;
    }

    public string Text { get; }

    public int ByteCount { get; }

    public int LineCount { get; }

    public bool WasTruncated { get; }

    public bool ContainsInvalidText { get; }

    public static AgentCommandStreamCapture Empty { get; } = Create(string.Empty);

    public static AgentCommandStreamCapture Create(
        string text,
        bool wasTruncated = false,
        bool containsInvalidText = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        var byteCount = AgentActionBudgets.GetUtf8ByteCount(text);
        var lineCount = CountLines(text);
        return new AgentCommandStreamCapture(
            text,
            byteCount,
            lineCount,
            wasTruncated,
            containsInvalidText);
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var lines = 1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }
}
