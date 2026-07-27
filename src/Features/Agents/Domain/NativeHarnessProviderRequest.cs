using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Provider chat-completions request for one model round.
/// </summary>
internal sealed class NativeHarnessProviderRequest
{
    public NativeHarnessProviderRequest(IEnumerable<NativeHarnessChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var normalized = messages.ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one message is required.", nameof(messages));
        }

        if (normalized.Any(message => message is null))
        {
            throw new ArgumentException("Messages cannot contain null entries.", nameof(messages));
        }

        Messages = Array.AsReadOnly(normalized);
    }

    public IReadOnlyList<NativeHarnessChatMessage> Messages { get; }
}
