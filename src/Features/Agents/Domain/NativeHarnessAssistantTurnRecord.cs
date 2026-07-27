using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Assistant text emitted by the model within one turn.
/// </summary>
internal sealed class NativeHarnessAssistantTurnRecord : NativeHarnessLoopHistoryRecord
{
    public NativeHarnessAssistantTurnRecord(int turnIndex, DateTimeOffset recordedAtUtc, string text)
        : base(turnIndex, recordedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Assistant turn text is required.", nameof(text));
        }

        Text = text;
    }

    public string Text { get; }
}
