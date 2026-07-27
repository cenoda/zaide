using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Run-scoped Native Harness model/tool execution loop.
/// </summary>
internal sealed class NativeHarnessLoopRunner
{
    private readonly INativeHarnessProviderOptionsSource _optionsSource;
    private readonly INativeHarnessProviderTransport _transport;
    private readonly INativeHarnessPriorConversationReader _priorConversationReader;

    public NativeHarnessLoopRunner(
        INativeHarnessProviderOptionsSource optionsSource,
        INativeHarnessProviderTransport transport,
        INativeHarnessPriorConversationReader priorConversationReader)
    {
        _optionsSource = optionsSource
            ?? throw new ArgumentNullException(nameof(optionsSource));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _priorConversationReader = priorConversationReader
            ?? throw new ArgumentNullException(nameof(priorConversationReader));
    }

    public async Task<NativeHarnessRunOutcome> ExecuteAsync(
        AgentBackendExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        var now = DateTimeOffset.UtcNow;
        var history = NativeHarnessLoopHistory.Empty;
        var turnBudget = NativeHarnessTurnBudget.CreateDefault();
        var cancellationState = NativeHarnessCancellationState.Initial();
        var phase = NativeHarnessTurnPhase.AwaitingModel;

        var systemPrompt = NativeHarnessSystemPromptBuilder.Build(request.ContextManifest);
        history = history.Append(new NativeHarnessSystemPromptRecord(
            turnIndex: 0,
            recordedAtUtc: now,
            text: systemPrompt));

        var replayEntries = _priorConversationReader.SelectReplayEntries(
            new NativeHarnessPriorConversationReplayRequest(
                request.ConversationId,
                request.MessageEntryId,
                NativeHarnessPriorConversationReplayPolicy.CreateStandard()));

        var turnIndex = 0;
        foreach (var replayEntry in replayEntries)
        {
            turnIndex++;
            history = history.Append(replayEntry.Kind switch
            {
                ConversationEntryKind.UserChat => new NativeHarnessUserTurnRecord(
                    turnIndex,
                    now,
                    replayEntry.Text),
                ConversationEntryKind.AssistantResponse => new NativeHarnessAssistantTurnRecord(
                    turnIndex,
                    now,
                    replayEntry.Text),
                _ => throw new InvalidOperationException("Unexpected replay entry kind."),
            });
        }

        turnIndex++;
        history = history.Append(new NativeHarnessUserTurnRecord(
            turnIndex,
            now,
            request.MessageText));

        while (phase != NativeHarnessTurnPhase.Terminal)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationState = cancellationState.WithCancellationRequested();
                return CreateCancelledOutcome(cancellationState);
            }

            if (turnBudget.IsExhausted)
            {
                return new NativeHarnessRunOutcome(
                    NativeHarnessRunTerminationKind.TurnBudgetExceeded,
                    failureReason: "Model turn budget exceeded.");
            }

            turnBudget = turnBudget.ConsumeTurn();
            var currentTurnIndex = turnIndex;

            var options = _optionsSource.ResolveOptions();
            if (options is null)
            {
                return new NativeHarnessRunOutcome(
                    NativeHarnessRunTerminationKind.Failed,
                    failureReason: "Failed to resolve provider configuration.");
            }

            if (!IsConfigured(options))
            {
                return new NativeHarnessRunOutcome(
                    NativeHarnessRunTerminationKind.Failed,
                    failureReason: "Provider configuration is incomplete.");
            }

            NativeHarnessProviderResponse providerResponse;
            try
            {
                providerResponse = await _transport.CompleteChatAsync(
                    options,
                    new NativeHarnessProviderRequest(NativeHarnessChatMessage.FromLoopHistory(history)),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancellationState = cancellationState.WithCancellationRequested();
                return CreateCancelledOutcome(cancellationState);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationState = cancellationState.WithCancellationRequested();
                return CreateCancelledOutcome(cancellationState);
            }

            if (providerResponse.IsFailure)
            {
                return new NativeHarnessRunOutcome(
                    NativeHarnessRunTerminationKind.Failed,
                    failureReason: providerResponse.FailureReason
                        ?? "Provider transport failed.");
            }

            if (providerResponse.HasToolCalls)
            {
                phase = NativeHarnessTurnPhase.ExecutingTools;
                var toolExecution = await ExecuteToolCallsAsync(
                    context.Actions,
                    history,
                    currentTurnIndex,
                    providerResponse.ToolCalls,
                    cancellationToken,
                    cancellationState).ConfigureAwait(false);
                history = toolExecution.History;
                cancellationState = toolExecution.CancellationState;

                if (cancellationState.CancellationRequested)
                {
                    return CreateCancelledOutcome(cancellationState);
                }

                phase = NativeHarnessTurnPhase.AwaitingModel;
                continue;
            }

            if (string.IsNullOrWhiteSpace(providerResponse.AssistantContent))
            {
                return new NativeHarnessRunOutcome(
                    NativeHarnessRunTerminationKind.Failed,
                    failureReason: "Provider returned an empty assistant completion.");
            }

            turnIndex++;
            history = history.Append(new NativeHarnessAssistantTurnRecord(
                turnIndex,
                DateTimeOffset.UtcNow,
                providerResponse.AssistantContent));

            phase = NativeHarnessTurnPhase.Terminal;
            return new NativeHarnessRunOutcome(
                NativeHarnessRunTerminationKind.Completed,
                finalAssistantText: providerResponse.AssistantContent);
        }

        return new NativeHarnessRunOutcome(
            NativeHarnessRunTerminationKind.Indeterminate,
            failureReason: "Harness loop reached an unexpected terminal state.");
    }

    private async Task<ToolExecutionResult> ExecuteToolCallsAsync(
        IAgentActionBroker broker,
        NativeHarnessLoopHistory history,
        int turnIndex,
        IReadOnlyList<NativeHarnessProviderToolCall> toolCalls,
        CancellationToken cancellationToken,
        NativeHarnessCancellationState cancellationState)
    {
        foreach (var providerToolCall in toolCalls)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ToolExecutionResult(
                    history,
                    cancellationState.WithCancellationRequested());
            }

            if (!NativeHarnessToolArgumentMapper.TryCreateDescriptor(
                    providerToolCall.ToolCallId,
                    providerToolCall.ModelToolName,
                    providerToolCall.ArgumentsJson,
                    out var descriptor,
                    out var descriptorError))
            {
                history = history.Append(new NativeHarnessToolCallRecord(
                    turnIndex,
                    DateTimeOffset.UtcNow,
                    providerToolCall.ToolCallId,
                    AgentActionKind.ReadFile,
                    providerToolCall.ModelToolName,
                    providerToolCall.ArgumentsJson));

                history = history.Append(new NativeHarnessToolResultRecord(
                    turnIndex,
                    DateTimeOffset.UtcNow,
                    providerToolCall.ToolCallId,
                    AgentActionResultKind.Failed,
                    NativeHarnessToolResultFormatter.FormatValidationError(descriptorError)));

                continue;
            }

            history = history.Append(new NativeHarnessToolCallRecord(
                turnIndex,
                DateTimeOffset.UtcNow,
                descriptor!.ToolCallId,
                descriptor.ActionKind,
                descriptor.ModelToolName,
                descriptor.ArgumentsJson));

            if (!NativeHarnessToolArgumentMapper.TryMapToPayload(
                    descriptor,
                    out var payload,
                    out var payloadError))
            {
                history = history.Append(new NativeHarnessToolResultRecord(
                    turnIndex,
                    DateTimeOffset.UtcNow,
                    descriptor.ToolCallId,
                    AgentActionResultKind.Failed,
                    NativeHarnessToolResultFormatter.FormatValidationError(payloadError)));

                continue;
            }

            AgentActionResult result;
            try
            {
                result = await broker.RequestAsync(
                    payload!,
                    descriptor.CorrelationKey,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancellationState = cancellationState.WithCancellationRequested();
                history = history.Append(new NativeHarnessToolResultRecord(
                    turnIndex,
                    DateTimeOffset.UtcNow,
                    descriptor.ToolCallId,
                    AgentActionResultKind.Cancelled,
                    NativeHarnessToolResultFormatter.FormatCancellation()));
                return new ToolExecutionResult(history, cancellationState);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationState = cancellationState
                    .WithCancellationRequested()
                    .WithLateCompletionObserved(
                        NativeHarnessLateCompletionDisposition.ObservedAndDiscarded);
            }

            history = history.Append(new NativeHarnessToolResultRecord(
                turnIndex,
                DateTimeOffset.UtcNow,
                descriptor.ToolCallId,
                result.ResultKind,
                NativeHarnessToolResultFormatter.Format(result)));
        }

        return new ToolExecutionResult(history, cancellationState);
    }

    private readonly record struct ToolExecutionResult(
        NativeHarnessLoopHistory History,
        NativeHarnessCancellationState CancellationState);

    private static bool IsConfigured(AgentExecutionOptions options) =>
        !string.IsNullOrWhiteSpace(options.ApiKey)
        && !string.IsNullOrWhiteSpace(options.BaseUrl)
        && !string.IsNullOrWhiteSpace(options.Model);

    private static NativeHarnessRunOutcome CreateCancelledOutcome(
        NativeHarnessCancellationState cancellationState)
    {
        if (cancellationState.HasLateCompletion)
        {
            return new NativeHarnessRunOutcome(
                NativeHarnessRunTerminationKind.Indeterminate,
                failureReason: "Run was cancelled after late tool completion was observed.",
                lateCompletionDisposition: cancellationState.LateCompletionDisposition);
        }

        return new NativeHarnessRunOutcome(
            NativeHarnessRunTerminationKind.Cancelled,
            failureReason: "Run was cancelled.");
    }
}
