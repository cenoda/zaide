using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Maps legacy <see cref="AgentExecutionResult"/> handlers onto the session backend contract.
/// </summary>
internal sealed class ResultMappingAgentBackend : IAgentBackend
{
    private static readonly AgentBackendId LegacyBackendId =
        AgentBackendId.FromValue(LegacyOpenAiCompatibleAgentBackend.BackendIdValue);

    private readonly Func<string, CancellationToken, Task<AgentExecutionResult>> _handler;

    public ResultMappingAgentBackend(Func<string, Task<AgentExecutionResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = (message, cancellationToken) => handler(message);
    }

    public ResultMappingAgentBackend(
        Func<string, CancellationToken, Task<AgentExecutionResult>> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public AgentBackendId BackendId => LegacyBackendId;

    public string BackendVersion => "result-mapping/1";

    public AgentCapabilitySnapshot CapabilitySnapshot { get; } =
        AgentCapabilitySnapshot.CreateInitial(
            LegacyBackendId,
            new[]
            {
                AgentCapabilityRow.Create(
                    AgentCapabilityId.MessageCompletion,
                    AgentCapabilityState.Create(
                        advertised: AgentCapabilityFactValue.Supported,
                        available: AgentCapabilityFactValue.Supported,
                        configured: AgentCapabilityFactValue.Supported,
                        permitted: AgentCapabilityFactValue.Unknown,
                        degraded: AgentCapabilityFactValue.NotSupported,
                        currentlyUsable: AgentCapabilityFactValue.Supported)),
            });

    public async IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
        AgentBackendRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        AgentBackendEvent? terminalEvent;
        try
        {
            var result = await _handler(request.MessageText, cancellationToken).ConfigureAwait(false);
            terminalEvent = MapResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            terminalEvent = CreateFailure(AgentFailureKind.Cancellation, "The operation was canceled.");
        }
        catch (Exception exception)
        {
            var reason = string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message;
            terminalEvent = CreateFailure(AgentFailureKind.Execution, reason);
        }

        yield return terminalEvent
            ?? CreateFailure(AgentFailureKind.Indeterminate, "Request ended indeterminately.");
    }

    private static AgentBackendEvent MapResult(AgentExecutionResult result)
    {
        if (result.IsSuccess)
        {
            if (string.IsNullOrWhiteSpace(result.ResponseText))
            {
                return CreateFailure(AgentFailureKind.Execution, "Assistant response was empty.");
            }

            return new AgentBackendEvent(
                AgentBackendEventKind.MessageCompleted,
                DateTimeOffset.UtcNow,
                new AgentBackendMessageCompletedPayload(result.ResponseText));
        }

        return CreateFailure(
            AgentFailureKind.Execution,
            string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Request failed." : result.ErrorMessage);
    }

    private static AgentBackendEvent CreateFailure(AgentFailureKind failureKind, string reason) =>
        new(
            AgentBackendEventKind.FailureObserved,
            DateTimeOffset.UtcNow,
            new AgentBackendFailurePayload(failureKind, reason));
}
