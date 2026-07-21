using System;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Narrow success/failure result for a single OpenAI-compatible execution.
/// </summary>
public sealed class AgentExecutionResult
{
    private AgentExecutionResult() { }

    /// <summary>
    /// True when the request completed and returned valid assistant text.
    /// </summary>
    public bool IsSuccess { get; private init; }

    /// <summary>
    /// The assistant response text. Non-null only when <see cref="IsSuccess"/> is true.
    /// </summary>
    public string? ResponseText { get; private init; }

    /// <summary>
    /// Human-readable failure reason. Non-null only when <see cref="IsSuccess"/> is false.
    /// </summary>
    public string? ErrorMessage { get; private init; }

    /// <summary>
    /// Typed failure category when produced by infrastructure. Null for success and
    /// for legacy <see cref="Failure(string)"/> callers that omit classification.
    /// </summary>
    internal AgentFailureKind? FailureKind { get; private init; }

    /// <summary>
    /// Creates a success result with the assistant's response text.
    /// </summary>
    public static AgentExecutionResult Success(string responseText) =>
        new() { IsSuccess = true, ResponseText = responseText ?? throw new ArgumentNullException(nameof(responseText)) };

    /// <summary>
    /// Creates a failure result with a descriptive error message.
    /// </summary>
    public static AgentExecutionResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage)) };

    internal static AgentExecutionResult Failure(string errorMessage, AgentFailureKind failureKind) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage)),
            FailureKind = failureKind,
        };
}
