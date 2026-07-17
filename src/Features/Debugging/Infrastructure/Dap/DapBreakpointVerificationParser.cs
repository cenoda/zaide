using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Zaide.Features.Debugging.Application;

namespace Zaide.Features.Debugging.Infrastructure.Dap;

/// <summary>
/// Maps DAP <c>setBreakpoints</c> reply bodies to session-only verification rows.
/// </summary>
internal static class DapBreakpointVerificationParser
{
    public static IReadOnlyList<DebugBreakpointVerification> Parse(
        string sourcePath,
        IReadOnlyList<int> requestedLines,
        JsonElement? body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(requestedLines);

        var normalized = Path.GetFullPath(sourcePath);
        if (requestedLines.Count == 0)
            return Array.Empty<DebugBreakpointVerification>();

        JsonElement breakpoints = default;
        var hasBreakpoints = body is { } root &&
                             root.ValueKind == JsonValueKind.Object &&
                             root.TryGetProperty("breakpoints", out breakpoints) &&
                             breakpoints.ValueKind == JsonValueKind.Array;

        var results = new DebugBreakpointVerification[requestedLines.Count];
        for (var i = 0; i < requestedLines.Count; i++)
        {
            var requestedLine = requestedLines[i];
            if (!hasBreakpoints || i >= breakpoints.GetArrayLength())
            {
                results[i] = new DebugBreakpointVerification(
                    normalized,
                    requestedLine,
                    requestedLine,
                    DebugBreakpointVerificationState.Pending,
                    Message: null);
                continue;
            }

            results[i] = ParseOne(normalized, requestedLine, breakpoints[i]);
        }

        return results;
    }

    private static DebugBreakpointVerification ParseOne(
        string sourcePath,
        int requestedLine,
        JsonElement element)
    {
        var verified = element.ValueKind == JsonValueKind.Object &&
                       element.TryGetProperty("verified", out var verifiedElement) &&
                       verifiedElement.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                       verifiedElement.GetBoolean();

        var actualLine = requestedLine;
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("line", out var lineElement) &&
            lineElement.ValueKind == JsonValueKind.Number &&
            lineElement.TryGetInt32(out var line) &&
            line >= 1)
        {
            actualLine = line;
        }

        string? message = null;
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("message", out var messageElement) &&
            messageElement.ValueKind == JsonValueKind.String)
        {
            message = messageElement.GetString();
            if (string.IsNullOrWhiteSpace(message))
                message = null;
        }

        if (verified)
        {
            return new DebugBreakpointVerification(
                sourcePath,
                requestedLine,
                actualLine,
                DebugBreakpointVerificationState.Verified,
                message);
        }

        // DAP uses verified=false for both "still pending" and "rejected".
        // Adapter messages that explicitly say "pending" stay pending; other
        // non-empty messages are rejected; missing message is pending.
        var state = ClassifyUnverified(message);

        return new DebugBreakpointVerification(
            sourcePath,
            requestedLine,
            actualLine,
            state,
            message);
    }

    private static DebugBreakpointVerificationState ClassifyUnverified(string? message)
    {
        if (message is null)
            return DebugBreakpointVerificationState.Pending;

        if (message.Contains("pending", StringComparison.OrdinalIgnoreCase))
            return DebugBreakpointVerificationState.Pending;

        return DebugBreakpointVerificationState.Rejected;
    }
}
