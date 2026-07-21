using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class AgentEventStreamTests
{
    [Fact]
    public void Publish_DeliversEventsInPublishOrder()
    {
        var stream = new AgentEventStream();
        var received = new List<AgentEvent>();
        using var subscription = stream.Events.Subscribe(received.Add);

        var first = CreateSampleEvent(sequence: 1);
        var second = CreateSampleEvent(sequence: 2);

        stream.Publish(first);
        stream.Publish(second);

        Assert.Equal(new[] { first, second }, received);
    }

    [Fact]
    public void Publish_RejectsNullEvent()
    {
        var stream = new AgentEventStream();

        Assert.Throws<ArgumentNullException>(() => stream.Publish(null!));
    }

    [Fact]
    public void Events_ExposesReadOnlyObservable_NotMutableSubject()
    {
        var stream = new AgentEventStream();

        Assert.False(stream.Events is Subject<AgentEvent>);
        Assert.IsNotAssignableFrom<Subject<AgentEvent>>(stream.Events);
    }

    [Fact]
    public async Task Publish_SerializesConcurrentPublication()
    {
        var stream = new AgentEventStream();
        var receivedCount = 0;

        using var subscription = stream.Events.Subscribe(_ =>
            Interlocked.Increment(ref receivedCount));

        var tasks = Enumerable.Range(1, 50)
            .Select(sequence => Task.Run(() => stream.Publish(CreateSampleEvent(sequence))))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(50, receivedCount);
    }

    [Fact]
    public async Task Publish_AllowsConcurrentSubscriptionWhilePublishing()
    {
        var stream = new AgentEventStream();
        var received = new ConcurrentBag<long>();
        var publishStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stopSubscribing = new CancellationTokenSource();

        var publishTask = Task.Run(() =>
        {
            for (var sequence = 1; sequence <= 30; sequence++)
            {
                stream.Publish(CreateSampleEvent(sequence));
                if (sequence == 1)
                {
                    publishStarted.TrySetResult();
                }

                Thread.Sleep(1);
            }
        });

        await publishStarted.Task;

        var subscribeTasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                while (!stopSubscribing.Token.IsCancellationRequested)
                {
                    using var subscription = stream.Events.Subscribe(
                        agentEvent => received.Add(agentEvent.Sequence));
                    Thread.Sleep(2);
                }
            }))
            .ToArray();

        await publishTask;
        stopSubscribing.Cancel();
        await Task.WhenAll(subscribeTasks);

        Assert.NotEmpty(received);
    }

    [Fact]
    public void Publish_PreservesGlobalOrderAcrossReentrantPublication()
    {
        var stream = new AgentEventStream();
        var secondObserverSequences = new List<long>();
        var firstEvent = CreateSampleEvent(sequence: 1);
        var secondEvent = CreateSampleEvent(sequence: 2);

        using var reentrantPublisher = stream.Events.Subscribe(agentEvent =>
        {
            if (agentEvent.Sequence == 1)
            {
                stream.Publish(secondEvent);
            }
        });
        using var orderingObserver = stream.Events.Subscribe(
            agentEvent => secondObserverSequences.Add(agentEvent.Sequence));

        stream.Publish(firstEvent);

        Assert.Equal(new[] { 1L, 2L }, secondObserverSequences);
    }

    [Fact]
    public void Publish_DoesNotStarveHealthySubscribersWhenAnotherThrows()
    {
        var stream = new AgentEventStream();
        var healthy = new List<long>();

        using var failingFirst = stream.Events.Subscribe(_ => throw new InvalidOperationException("boom"));
        using var healthySubscriber = stream.Events.Subscribe(agentEvent => healthy.Add(agentEvent.Sequence));

        Assert.Null(Record.Exception(() => stream.Publish(CreateSampleEvent(1))));
        Assert.Equal(new[] { 1L }, healthy);

        Assert.Null(Record.Exception(() => stream.Publish(CreateSampleEvent(2))));
        Assert.Equal(new[] { 1L, 2L }, healthy);
    }

    private static AgentEvent CreateSampleEvent(long sequence)
    {
        var backendId = AgentBackendId.FromValue("backend:test");
        var runId = ExecutionRunId.New();
        var occurredAt = DateTimeOffset.UtcNow;

        return new AgentEvent(
            AgentEventId.New(),
            AgentEvent.CurrentSchemaVersion,
            AgentSessionId.New(),
            runId,
            ConversationId.NewDirect(),
            backendId,
            sequence,
            occurredAt,
            occurredAt.AddMilliseconds(1),
            causationEventId: null,
            AgentActivityEvidenceLevel.ZaideExecuted,
            AgentEventKind.RunCreated,
            new AgentRunLifecyclePayload(AgentRunStatus.Created));
    }
}
