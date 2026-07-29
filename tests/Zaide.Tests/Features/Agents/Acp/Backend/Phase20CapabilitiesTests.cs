using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Acp.Backend;

public sealed class Phase20CapabilitiesTests
{
    [Fact]
    public void Phase20Capabilities_InitialSnapshot_IsTruthfullyUnavailable()
    {
        var snapshot = AcpCapabilityRows.CreateInitialSnapshot(
            transportReady: false,
            sessionReady: false,
            contextManifestPresent: false,
            usageObserved: false);

        Assert.Equal(AgentBackendIds.Acp, snapshot.BackendId);
        Assert.True(snapshot.TryGetState(AgentCapabilityId.MessageCompletion, out var messageCompletion));
        Assert.Equal(AgentCapabilityFactValue.Unavailable, messageCompletion!.CurrentlyUsable);
        Assert.True(snapshot.TryGetState(AgentCapabilityId.RawTrace, out var rawTrace));
        Assert.Equal(AgentCapabilityFactValue.NotSupported, rawTrace!.Advertised);
        Assert.True(snapshot.TryGetState(AgentCapabilityId.Resume, out var resume));
        Assert.Equal(AgentCapabilityFactValue.NotSupported, resume!.CurrentlyUsable);
    }

    [Fact]
    public void Phase20Capabilities_NegotiatedSnapshot_ExposesRequiredRows()
    {
        var snapshot = AcpCapabilityRows.CreateInitialSnapshot(
            transportReady: true,
            sessionReady: true,
            contextManifestPresent: true,
            usageObserved: false);

        var required = new[]
        {
            AgentCapabilityId.MessageCompletion,
            AgentCapabilityId.Cancellation,
            AgentCapabilityId.Tools,
            AgentCapabilityId.Permissions,
            AgentCapabilityId.IdeContext,
            AgentCapabilityId.Streaming,
            AgentCapabilityId.Resume,
            AgentCapabilityId.UsageReporting,
            AgentCapabilityId.RawTrace,
        };

        foreach (var capabilityId in required)
        {
            Assert.True(snapshot.TryGetState(capabilityId, out _), capabilityId.Value);
        }
    }

    [Fact]
    public async Task Phase20Capabilities_UsageObservation_IncreasesSnapshotVersion()
    {
        var script = new AcpFakeSessionScript
        {
            Updates = new[]
            {
                new AcpSessionUpdate { Kind = AcpSessionUpdateKind.UsageUpdate },
            },
        };

        var backend = new AcpAgentBackend(
            new DelegatingAcpSessionClientFactory(
                _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script))),
            () => "/tmp/zaide-acp");

        var initialVersion = backend.CapabilitySnapshot.Version;
        var context = new AgentBackendExecutionContext(
            new AgentBackendRequest(
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                ActorId.FromValue("actor:user"),
                ActorId.FromValue("actor:agent"),
                ConversationEntryId.New(),
                "observe usage"),
            new UnavailableAgentActionBroker());

        await foreach (var backendEvent in backend.ExecuteAsync(context, CancellationToken.None))
        {
            if (backendEvent.Kind == AgentBackendEventKind.CapabilitySnapshotChanged)
            {
                var payload = Assert.IsType<AgentBackendCapabilityChangedPayload>(backendEvent.Payload);
                Assert.True(payload.Snapshot.Version > initialVersion);
                Assert.True(
                    payload.Snapshot.TryGetState(AgentCapabilityId.UsageReporting, out var usage));
                Assert.Equal(AgentCapabilityFactValue.Supported, usage!.CurrentlyUsable);
            }
        }
    }

    [Fact]
    public void Phase20Capabilities_ToolsRemainBackendReportedNotZaideMediated()
    {
        var snapshot = AcpCapabilityRows.CreateInitialSnapshot(
            transportReady: true,
            sessionReady: true,
            contextManifestPresent: false,
            usageObserved: false);

        Assert.True(snapshot.TryGetState(AgentCapabilityId.Tools, out var tools));
        Assert.Equal(AgentCapabilityFactValue.Unknown, tools!.CurrentlyUsable);
        Assert.Equal(AgentCapabilityFactValue.Unknown, tools.Permitted);
    }
}
