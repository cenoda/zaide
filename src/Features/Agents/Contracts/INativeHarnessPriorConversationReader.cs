using System.Collections.Generic;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Read-only seam for bounded prior-conversation replay into model context.
/// M3 implementations read from <see cref="IConversationStore"/> without mutation.
/// </summary>
internal interface INativeHarnessPriorConversationReader
{
    IReadOnlyList<NativeHarnessPriorConversationReplayEntry> SelectReplayEntries(
        NativeHarnessPriorConversationReplayRequest request);
}
