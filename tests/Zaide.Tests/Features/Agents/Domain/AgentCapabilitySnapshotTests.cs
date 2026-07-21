using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Xunit;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class AgentCapabilitySnapshotTests
{
    private static readonly AgentBackendId BackendId =
        AgentBackendId.FromValue("backend:legacy-openai-compatible");

    [Fact]
    public void AgentCapabilityState_KeepsAdvertisedAvailableConfiguredPermittedDegradedAndUsabilityDistinct()
    {
        var state = AgentCapabilityState.Create(
            advertised: AgentCapabilityFactValue.Supported,
            available: AgentCapabilityFactValue.Unavailable,
            configured: AgentCapabilityFactValue.Supported,
            permitted: AgentCapabilityFactValue.Unknown,
            degraded: AgentCapabilityFactValue.NotSupported,
            currentlyUsable: AgentCapabilityFactValue.NotSupported);

        Assert.Equal(AgentCapabilityFactValue.Supported, state.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Unavailable, state.Available);
        Assert.Equal(AgentCapabilityFactValue.Supported, state.Configured);
        Assert.Equal(AgentCapabilityFactValue.Unknown, state.Permitted);
        Assert.Equal(AgentCapabilityFactValue.NotSupported, state.Degraded);
        Assert.Equal(AgentCapabilityFactValue.NotSupported, state.CurrentlyUsable);
    }

    [Fact]
    public void AgentCapabilitySnapshot_CreateInitial_StartsAtVersionOne()
    {
        var snapshot = CreateSnapshot(version: 1);

        Assert.Equal(1, snapshot.Version);
        Assert.Equal(BackendId, snapshot.BackendId);
        Assert.True(snapshot.TryGetState(AgentCapabilityId.MessageCompletion, out var state));
        Assert.Equal(AgentCapabilityFactValue.Supported, state.Advertised);
    }

    [Fact]
    public void AgentCapabilitySnapshot_WithRow_RequiresMonotonicVersionIncrease()
    {
        var initial = CreateSnapshot(version: 1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            initial.WithRow(
                AgentCapabilityRow.Create(
                    AgentCapabilityId.Streaming,
                    AgentCapabilityState.Create(
                        advertised: AgentCapabilityFactValue.Supported,
                        available: AgentCapabilityFactValue.NotSupported,
                        configured: AgentCapabilityFactValue.Unknown,
                        permitted: AgentCapabilityFactValue.Unknown,
                        degraded: AgentCapabilityFactValue.NotSupported,
                        currentlyUsable: AgentCapabilityFactValue.NotSupported)),
                version: 1));

        Assert.Equal("version", exception.ParamName);
    }

    [Fact]
    public void AgentCapabilitySnapshot_WithRow_PreservesBackendIdentity()
    {
        var initial = CreateSnapshot(version: 1);
        var updated = initial.WithRow(
            AgentCapabilityRow.Create(
                AgentCapabilityId.Streaming,
                AgentCapabilityState.Create(
                    advertised: AgentCapabilityFactValue.Supported,
                    available: AgentCapabilityFactValue.NotSupported,
                    configured: AgentCapabilityFactValue.Unknown,
                    permitted: AgentCapabilityFactValue.Unknown,
                    degraded: AgentCapabilityFactValue.NotSupported,
                    currentlyUsable: AgentCapabilityFactValue.NotSupported)),
            version: 2);

        Assert.Equal(initial.BackendId, updated.BackendId);
        Assert.Equal(2, updated.Version);
    }

    [Fact]
    public void AgentCapabilitySnapshot_DoesNotSynthesizeSupportedWhenUnknown()
    {
        var state = AgentCapabilityState.Create(
            advertised: AgentCapabilityFactValue.Unknown,
            available: AgentCapabilityFactValue.Unknown,
            configured: AgentCapabilityFactValue.Unknown,
            permitted: AgentCapabilityFactValue.Unknown,
            degraded: AgentCapabilityFactValue.Unknown,
            currentlyUsable: AgentCapabilityFactValue.Unknown);

        Assert.DoesNotContain(
            AgentCapabilityFactValue.Supported,
            new[]
            {
                state.Advertised,
                state.Available,
                state.Configured,
                state.Permitted,
                state.Degraded,
                state.CurrentlyUsable,
            });
    }

    [Fact]
    public void AgentCapabilityRow_RejectsDefaultCapabilityId()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AgentCapabilityRow.Create(
                default,
                AgentCapabilityState.Create(
                    advertised: AgentCapabilityFactValue.Supported,
                    available: AgentCapabilityFactValue.Supported,
                    configured: AgentCapabilityFactValue.Supported,
                    permitted: AgentCapabilityFactValue.Unknown,
                    degraded: AgentCapabilityFactValue.NotSupported,
                    currentlyUsable: AgentCapabilityFactValue.Supported)));

        Assert.Equal("capabilityId", exception.ParamName);
    }

    [Fact]
    public void AgentCapabilitySnapshot_CreateInitial_OrdersRowsCanonically()
    {
        var streaming = AgentCapabilityRow.Create(
            AgentCapabilityId.Streaming,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.NotSupported,
                configured: AgentCapabilityFactValue.Unknown,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.NotSupported));
        var completion = AgentCapabilityRow.Create(
            AgentCapabilityId.MessageCompletion,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.Supported,
                configured: AgentCapabilityFactValue.Supported,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.Supported));

        var snapshot = AgentCapabilitySnapshot.CreateInitial(
            BackendId,
            new[] { streaming, completion });

        Assert.Equal(AgentCapabilityId.MessageCompletion, snapshot.Rows[0].CapabilityId);
        Assert.Equal(AgentCapabilityId.Streaming, snapshot.Rows[1].CapabilityId);
    }

    [Fact]
    public void AgentCapabilityState_RejectsUndefinedFactValue()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgentCapabilityState.Create(
                advertised: (AgentCapabilityFactValue)999,
                available: AgentCapabilityFactValue.Supported,
                configured: AgentCapabilityFactValue.Supported,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.Supported));

        Assert.Equal("advertised", exception.ParamName);
    }

    [Fact]
    public void AgentCapabilitySnapshot_RowsExposeReadOnlyWrapper()
    {
        var snapshot = CreateSnapshot(version: 1);

        Assert.IsType<ReadOnlyCollection<AgentCapabilityRow>>(snapshot.Rows);
    }

    [Fact]
    public void AgentCapabilitySnapshot_RowsCannotBeMutatedThroughCollectionInterfaces()
    {
        var snapshot = CreateSnapshot(version: 1);
        var rows = snapshot.Rows;

        Assert.Throws<InvalidCastException>(() => _ = (AgentCapabilityRow[])rows);
        Assert.Throws<InvalidCastException>(() => _ = (List<AgentCapabilityRow>)rows);

        var mutableList = (IList<AgentCapabilityRow>)rows;
        var replacement = AgentCapabilityRow.Create(
            AgentCapabilityId.Streaming,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.NotSupported,
                configured: AgentCapabilityFactValue.Unknown,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.NotSupported));

        Assert.Throws<NotSupportedException>(() => mutableList[0] = replacement);
        Assert.Throws<NotSupportedException>(() => ((IList)mutableList).Add(replacement));
        Assert.Throws<NotSupportedException>(() => mutableList.Clear());
        Assert.Throws<NotSupportedException>(() => mutableList.RemoveAt(0));
    }

    [Fact]
    public void AgentCapabilitySnapshot_TryGetState_ReturnsNullWhenMissing()
    {
        var snapshot = CreateSnapshot(version: 1);

        Assert.False(snapshot.TryGetState(AgentCapabilityId.Streaming, out var state));
        Assert.Null(state);
    }

    [Fact]
    public void AgentCapabilityRow_RejectsDuplicateCapabilityInSnapshot()
    {
        var row = AgentCapabilityRow.Create(
            AgentCapabilityId.MessageCompletion,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.Supported,
                configured: AgentCapabilityFactValue.Supported,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.Supported));

        var exception = Assert.Throws<ArgumentException>(() =>
            AgentCapabilitySnapshot.CreateInitial(
                BackendId,
                new[] { row, row }));

        Assert.Equal("rows", exception.ParamName);
    }

    private static AgentCapabilitySnapshot CreateSnapshot(int version) =>
        AgentCapabilitySnapshot.CreateInitial(
            BackendId,
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
            },
            version: version);
}
