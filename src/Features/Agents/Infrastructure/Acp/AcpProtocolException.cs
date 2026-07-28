using System;
namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// ACP protocol failure surfaced to callers without leaking transport details.
/// </summary>
internal sealed class AcpProtocolException : Exception
{
    public AcpProtocolException(string message)
        : base(message)
    {
    }

    public AcpProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public AcpProtocolException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public int? ErrorCode { get; }
}
