using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Zaide.App.Composition;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Settings.Contracts;
using Zaide.Tests.Features.Agents.Transparency.Integration;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Transparency.Trace;

public sealed class Phase22TraceProducerTests : System.IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentTraceBoundedCaptureQueue _queue;
    private readonly AgentTraceBackendEvidenceSourceWriter _writer;

    public Phase22TraceProducerTests()
    {
        (_rootDirectory, _) = Phase21TraceTestSupport.CreateWorkspaceFixture();
        _store = Phase21TraceTestSupport.CreateStore(_rootDirectory);
        var resolver = Phase21TraceTestSupport.CreateKeyResolver();
        _queue = Phase21TraceTestSupport.CreateQueue(_store, resolver);
        var sink = Phase21TraceTestSupport.CreateSink(_queue, resolver);
        _writer = new AgentTraceBackendEvidenceSourceWriter(
            new AgentTraceCoordinator(sink, new AgentTraceInspector(_store), new AgentTraceSourceRegistry(), resolver));
    }

    [Fact]
    public void NativeAndAcpSources_KeepTheirEvidenceKindsIndependent()
    {
        var native = new NativeHarnessAgentTraceSource(_writer);
        var acp = new AcpAgentTraceSource(_writer);

        Assert.Equal(AgentBackendIds.NativeHarnessValue, native.BackendId);
        Assert.Equal(AgentBackendIds.AcpValue, acp.BackendId);
        Assert.True(native.CanExpose(AgentTraceKind.BackendLoopHistory));
        Assert.False(native.CanExpose(AgentTraceKind.ProtocolFrame));
        Assert.True(acp.CanExpose(AgentTraceKind.ProtocolFrame));
        Assert.False(acp.CanExpose(AgentTraceKind.BackendLoopHistory));
    }

    [Fact]
    public async Task NativeHarnessRun_EmitsRedactedTraceThroughTheRegisteredSource()
    {
        var workspace = Path.Combine(_rootDirectory, "run-workspace");
        Directory.CreateDirectory(workspace);
        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Success("trace response"));

        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        Phase23IsolatedSettingsTestSupport.ConfigureIsolatedSettings(services);
        services.RemoveAll<IWorkspaceActionAuthority>();
        services.AddSingleton<IWorkspaceActionAuthority>(new FakeWorkspaceActionAuthority(
            FakeWorkspaceActionAuthority.CreateScopeFromDirectory(workspace)));
        services.RemoveAll<INativeHarnessProviderTransport>();
        services.AddSingleton<INativeHarnessProviderTransport>(transport);
        services.RemoveAll<INativeHarnessProviderOptionsSource>();
        services.AddSingleton<INativeHarnessProviderOptionsSource>(new FixedNativeHarnessProviderOptionsSource());

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<AgentTransparencySettingsSync>();
        var management = provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        await Phase23SettingsTestSupport.EnableTraceCaptureAsync(provider.GetRequiredService<ISettingsService>());
        management.RefreshTracePresentation();
        var session = provider.GetRequiredService<IAgentSessionService>();
        var store = provider.GetRequiredService<IConversationStore>();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var conversation = store.GetOrCreateDirectConversation(
            catalog.CanonicalHuman.Id,
            ActorId.TownhallAgent);

        var result = await session.SendAsync(
            conversation.Id,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            AgentBackendIds.NativeHarness,
            ConversationEntryId.New(),
            "Authorization: Bearer sk-abcdefghijklmnopqrstuvwxyz0123456789",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, result.Status);

        var queue = provider.GetRequiredService<AgentTraceBoundedCaptureQueue>();
        Phase21TraceTestSupport.WaitForQueueDrain(queue, expectedWritten: 2);
        var records = await management.LoadTraceRecordsAsync(0, 64);

        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.Equal(AgentBackendIds.NativeHarnessValue, record.BackendId));
        Assert.DoesNotContain(records, record => record.RedactedPayloadJson.Contains("sk-abcdefghijklmnopqrstuvwxyz", System.StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _queue.Dispose();
        _store.Dispose();
        Phase21TraceTestSupport.DeleteDirectory(_rootDirectory);
    }

    private sealed class FixedNativeHarnessProviderOptionsSource : INativeHarnessProviderOptionsSource
    {
        public AgentExecutionOptions? ResolveOptions() => new()
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "trace-test-key",
            Model = "trace-test-model",
        };
    }
}
