using System;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// ACP process lifecycle failure without leaking transport or credential details.
/// </summary>
internal sealed class AcpProcessLifecycleException : Exception
{
    public AcpProcessLifecycleException(AcpProcessLifecycleFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public AcpProcessLifecycleException(
        AcpProcessLifecycleFailureKind kind,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public AcpProcessLifecycleFailureKind Kind { get; }
}
