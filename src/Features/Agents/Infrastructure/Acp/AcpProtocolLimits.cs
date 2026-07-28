using System;
namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Bounded protocol limits for ACP v1 stdio framing and JSON parsing.
/// </summary>
internal static class AcpProtocolLimits
{
    public const int MaxFrameBytes = 4 * 1024 * 1024;

    public const int MaxJsonDepth = 64;

    public const int MaxPromptBlocks = 256;

    public const int MaxSessionUpdatesPerPrompt = 10_000;

    public const int MaxToolCallContentItems = 256;
}
