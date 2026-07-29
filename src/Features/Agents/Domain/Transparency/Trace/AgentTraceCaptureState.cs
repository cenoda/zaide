namespace Zaide.Features.Agents.Domain.Transparency.Trace;

/// <summary>
/// Explicit per-record capture state for one redacted trace evidence row.
/// Honest missing evidence is reported as <see cref="Unavailable"/>; never as
/// <see cref="Captured"/> or <see cref="Redacted"/>.
/// </summary>
internal enum AgentTraceCaptureState
{
    /// <summary>Capture is disabled for the originating backend/workspace.</summary>
    Disabled = 0,
    /// <summary>Backend did not expose this evidence layer.</summary>
    Unavailable = 1,
    /// <summary>Captured as-is; no redaction was required.</summary>
    Captured = 2,
    /// <summary>Captured and redacted; secrets were replaced before retention.</summary>
    Redacted = 3,
    /// <summary>Sampled subset of original evidence was retained.</summary>
    Sampled = 4,
    /// <summary>Payload exceeded the size bound and was truncated to a marker.</summary>
    Truncated = 5,
    /// <summary>Captured evidence was condensed to a summary.</summary>
    Summarized = 6,
    /// <summary>Redaction or processing failed; a bounded failure marker was retained.</summary>
    Failed = 7,
}
