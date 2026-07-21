using System;
using System.Collections.Generic;
using System.Threading;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Serialized, read-only normalized agent event feed owned by the session lifecycle service.
/// </summary>
internal sealed class AgentEventStream
{
    private readonly object _gate = new();
    private readonly List<IObserver<AgentEvent>> _observers = new();
    private readonly Queue<AgentEvent> _pendingEvents = new();
    private readonly IObservable<AgentEvent> _events;
    private bool _isDispatching;

    public AgentEventStream()
    {
        _events = new AgentEventSourceObservable(this);
    }

    public IObservable<AgentEvent> Events => _events;

    public void Publish(AgentEvent agentEvent)
    {
        ArgumentNullException.ThrowIfNull(agentEvent);

        lock (_gate)
        {
            _pendingEvents.Enqueue(agentEvent);
            DrainPendingLocked();
        }
    }

    private void DrainPendingLocked()
    {
        if (_isDispatching)
        {
            return;
        }

        _isDispatching = true;
        try
        {
            while (_pendingEvents.Count > 0)
            {
                var agentEvent = _pendingEvents.Dequeue();
                var snapshot = _observers.ToArray();
                foreach (var observer in snapshot)
                {
                    try
                    {
                        observer.OnNext(agentEvent);
                    }
                    catch (Exception)
                    {
                        // Subscriber exceptions must not escape Publish or affect other subscribers.
                    }
                }
            }
        }
        finally
        {
            _isDispatching = false;
        }
    }

    internal IDisposable Subscribe(IObserver<AgentEvent> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        lock (_gate)
        {
            _observers.Add(observer);
        }

        return new Unsubscriber(this, observer);
    }

    private void Unsubscribe(IObserver<AgentEvent> observer)
    {
        lock (_gate)
        {
            _observers.Remove(observer);
        }
    }

    private sealed class AgentEventSourceObservable : IObservable<AgentEvent>
    {
        private readonly AgentEventStream _owner;

        public AgentEventSourceObservable(AgentEventStream owner)
        {
            _owner = owner;
        }

        public IDisposable Subscribe(IObserver<AgentEvent> observer) =>
            _owner.Subscribe(observer);
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly AgentEventStream _owner;
        private readonly IObserver<AgentEvent> _observer;
        private int _disposed;

        public Unsubscriber(AgentEventStream owner, IObserver<AgentEvent> observer)
        {
            _owner = owner;
            _observer = observer;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.Unsubscribe(_observer);
            }
        }
    }
}
