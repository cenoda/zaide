using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Contracts.Transparency.Trace;

/// <summary>
/// Nonblocking admission surface for the deepest truthful backend-exposed trace
/// layer. All inputs pass through mandatory redaction and bounded queue
/// admission before any durable write, render, export, log, index, backup, or
/// cross-process transfer.
/// </summary>
internal interface IAgentTraceCaptureSink
{
    /// <summary>
    /// Attempts to admit one trace capture request. Never blocks the agent
    /// event pipeline. Returns a result describing the redaction outcome, the
    /// queue admission, and the durable write status.
    /// </summary>
    AgentTraceCaptureResult TrySubmit(AgentTraceCaptureRequest request);
}
