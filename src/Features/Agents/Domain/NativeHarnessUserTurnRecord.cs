using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// User message admitted for the current run or replayed from prior conversation.
/// </summary>
internal sealed class NativeHarnessUserTurnRecord : NativeHarnessLoopHistoryRecord
{
    public NativeHarnessUserTurnRecord(int turnIndex, DateTimeOffset recordedAtUtc, string text)
        : base(turnIndex, recordedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("User turn text is required.", nameof(text));
        }

        Text = text;
    }

    public string Text { get; }
}
