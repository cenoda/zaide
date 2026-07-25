using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Infrastructure contract that resolves raw command payloads into Zaide-verified
/// canonical executable identities.
/// </summary>
/// <remarks>
/// Phase 17 M1 ships a default implementation that validates absolute-path
/// executables and binds denylist results before permission approval. Later
/// infrastructure (M2+) replaces it with real filesystem PATH search, symlink
/// resolution, and executable file-attribute verification without changing the
/// broker or fingerprint pipeline — only the resolver implementation changes.
/// </remarks>
internal interface IAgentCommandResolver
{
    /// <summary>
    /// Resolves a raw command payload into an immutable resolved command identity
    /// suitable for fingerprinting, denylist binding, and permission review.
    /// </summary>
    /// <param name="payload">
    /// The raw action payload from the agent backend. The executable string may
    /// be an absolute path, a bare name resolvable via PATH, or a backend-provided
    /// canonical path.
    /// </param>
    /// <param name="resolvedCommand">
    /// When this method returns <c>true</c>, contains the resolved command with
    /// canonical absolute executable path, denylist result, resolution metadata,
    /// and symlink chain (if applicable).
    /// </param>
    /// <param name="error">
    /// When this method returns <c>false</c>, contains a human-readable error
    /// message suitable for inclusion in a denied <see cref="AgentActionResult"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if resolution succeeded and the denylist result is bound;
    /// <c>false</c> if the executable could not be resolved or is inherently
    /// unresolvable.
    /// </returns>
    bool TryResolve(
        AgentExecuteCommandActionPayload payload,
        out AgentResolvedCommand? resolvedCommand,
        out string? error);
}
