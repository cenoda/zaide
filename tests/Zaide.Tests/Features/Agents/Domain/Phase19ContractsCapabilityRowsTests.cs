using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class Phase19ContractsCapabilityRowsTests
{
    [Fact]
    public void Phase19Contracts_CreateInitialSnapshot_IncludesAllSixRequiredRows()
    {
        var snapshot = NativeHarnessCapabilityRows.CreateInitialSnapshot(
            providerConfigured: true,
            workspaceCaptured: true,
            contextManifestPresent: true,
            streamingSupportedByProvider: true);

        Assert.Equal(AgentBackendIds.NativeHarness, snapshot.BackendId);
        Assert.Equal(6, snapshot.Rows.Count);

        var required = new[]
        {
            AgentCapabilityId.Tools,
            AgentCapabilityId.Permissions,
            AgentCapabilityId.IdeContext,
            AgentCapabilityId.Streaming,
            AgentCapabilityId.Cancellation,
            AgentCapabilityId.MessageCompletion,
        };

        foreach (var capabilityId in required)
        {
            Assert.True(snapshot.TryGetState(capabilityId, out var state), capabilityId.Value);
            AssertAllSixFactsPresent(state!);
        }
    }

    [Fact]
    public void Phase19Contracts_CapabilitySnapshot_WithRow_RequiresMonotonicVersionIncrease()
    {
        var snapshot = NativeHarnessCapabilityRows.CreateInitialSnapshot(
            providerConfigured: true,
            workspaceCaptured: true,
            contextManifestPresent: false,
            streamingSupportedByProvider: false);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            snapshot.WithRow(
                NativeHarnessCapabilityRows.CreateIdeContextRow(
                    providerConfigured: true,
                    contextManifestPresent: true),
                version: snapshot.Version));

        Assert.Equal("version", exception.ParamName);
    }

    [Fact]
    public void Phase19Contracts_ToolsRow_DoesNotClaimCurrentlyUsableWithoutWorkspace()
    {
        var row = NativeHarnessCapabilityRows.CreateToolsRow(
            providerConfigured: true,
            workspaceCaptured: false);

        Assert.Equal(AgentCapabilityFactValue.Supported, row.State.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Unavailable, row.State.Available);
        Assert.NotEqual(AgentCapabilityFactValue.Supported, row.State.CurrentlyUsable);
    }

    [Fact]
    public void Phase19Contracts_StreamingRow_ReportsNotSupportedWhenProviderLacksStreaming()
    {
        var row = NativeHarnessCapabilityRows.CreateStreamingRow(
            providerConfigured: true,
            streamingSupportedByProvider: false);

        Assert.Equal(AgentCapabilityFactValue.NotSupported, row.State.Available);
        Assert.Equal(AgentCapabilityFactValue.NotSupported, row.State.CurrentlyUsable);
    }

    [Fact]
    public void Phase19Contracts_IdeContextRow_RequiresManifestForCurrentUsability()
    {
        var withoutManifest = NativeHarnessCapabilityRows.CreateIdeContextRow(
            providerConfigured: true,
            contextManifestPresent: false);
        var withManifest = NativeHarnessCapabilityRows.CreateIdeContextRow(
            providerConfigured: true,
            contextManifestPresent: true);

        Assert.Equal(AgentCapabilityFactValue.NotSupported, withoutManifest.State.CurrentlyUsable);
        Assert.Equal(AgentCapabilityFactValue.Supported, withManifest.State.CurrentlyUsable);
    }

    private static void AssertAllSixFactsPresent(AgentCapabilityState state)
    {
        Assert.True(Enum.IsDefined(state.Advertised));
        Assert.True(Enum.IsDefined(state.Available));
        Assert.True(Enum.IsDefined(state.Configured));
        Assert.True(Enum.IsDefined(state.Permitted));
        Assert.True(Enum.IsDefined(state.Degraded));
        Assert.True(Enum.IsDefined(state.CurrentlyUsable));
    }
}
