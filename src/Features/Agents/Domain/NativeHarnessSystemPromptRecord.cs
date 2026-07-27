using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// System prompt content embedded once per run from Phase 18 manifest assembly.
/// </summary>
internal sealed class NativeHarnessSystemPromptRecord : NativeHarnessLoopHistoryRecord
{
    public NativeHarnessSystemPromptRecord(int turnIndex, DateTimeOffset recordedAtUtc, string text)
        : base(turnIndex, recordedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("System prompt text is required.", nameof(text));
        }

        Text = text;
    }

    public string Text { get; }
}
