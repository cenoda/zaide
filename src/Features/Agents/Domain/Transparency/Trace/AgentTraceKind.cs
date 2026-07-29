namespace Zaide.Features.Agents.Domain.Transparency.Trace;

/// <summary>
/// Backend-neutral trace evidence taxonomy. M2 only requires that each row
/// declares one kind; the meaning of each kind is owned by the submitting
/// backend source.
/// </summary>
internal enum AgentTraceKind
{
    /// <summary>Backend-supplied model request body.</summary>
    Request = 0,
    /// <summary>Backend-supplied model response body.</summary>
    Response = 1,
    /// <summary>Backend tool-call request emitted by the model.</summary>
    ToolCall = 2,
    /// <summary>Backend tool-call result reported to the model.</summary>
    ToolResult = 3,
    /// <summary>External protocol frame (for example, ACP JSON-RPC envelope).</summary>
    ProtocolFrame = 4,
    /// <summary>Transport or runtime error reported by the backend.</summary>
    Error = 5,
    /// <summary>Loop history record (for example, Native Harness turn record).</summary>
    BackendLoopHistory = 6,
    /// <summary>Context selection or manifest summary.</summary>
    ContextSelection = 7,
    /// <summary>Capability discovery or snapshot observation.</summary>
    CapabilityDiscovery = 8,
    /// <summary>Unavailability marker; backend cannot expose this evidence layer.</summary>
    UnavailableMarker = 9,
}
