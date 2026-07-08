using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Narrow service interface for one OpenAI-compatible non-streaming execution path.
/// </summary>
public interface IAgentExecutionService
{
    /// <summary>
    /// Sends a single user message to the configured OpenAI-compatible endpoint
    /// and returns the assistant response or an explicit failure.
    /// </summary>
    /// <param name="userMessage">The user's message text. Must not be null or empty.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success with response text, or failure with an error message.</returns>
    Task<AgentExecutionResult> ExecuteAsync(string userMessage, CancellationToken ct = default);
}