using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Editor.Application;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Language.Application;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Domain;
using Zaide.Tests.Features.Agents.Memory.Store;

namespace Zaide.Tests.Features.Agents.Memory.Retrieval;

public sealed class Phase21MemoryInfluenceTests : IDisposable
{
    private static readonly JsonSerializerOptions InfluenceSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentMemoryCoordinator _coordinator;
    private readonly AgentMemoryRetriever _retriever;
    private readonly AgentMemoryInfluenceRecorder _influenceRecorder;
    private readonly AgentContextManifestBuilder _manifestBuilder;

    public Phase21MemoryInfluenceTests()
    {
        (_rootDirectory, _workspaceKey, _) = Phase21MemoryTestSupport.CreateWorkspaceFixture();
        _store = Phase21MemoryTestSupport.CreateStore(_rootDirectory);
        _coordinator = Phase21MemoryTestSupport.CreateCoordinator(_store);
        _retriever = new AgentMemoryRetriever(_coordinator.Inspector, _store);
        _influenceRecorder = new AgentMemoryInfluenceRecorder(_store);
        _manifestBuilder = new AgentContextManifestBuilder();
    }

    public void Dispose()
    {
        _store.Dispose();
        Phase21MemoryTestSupport.DeleteDirectory(_rootDirectory);
    }

    [Fact]
    public void Influence_RecordsMemoryRevisionsIncludedInManifest()
    {
        var actor = Phase21MemoryTestSupport.TestAuthor;
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var conversationId = ConversationId.ForChannel("general");

        _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(actor),
            "Influence fact",
            Phase21MemoryTestSupport.CreateProvenance(sourceRevision: "influence-rev"),
            idempotencyKey: "influence-create"));

        var context = new AgentMemoryRetrievalContext(
            sessionId,
            runId,
            conversationId,
            actor,
            projectId: _workspaceKey.Value);
        var retrieval = _retriever.Retrieve(new AgentMemoryRetrievalRequest(_workspaceKey, context));

        var manifest = _manifestBuilder.Build(
            sessionId,
            runId,
            conversationId,
            AgentContextPolicy.CreateApplicationDefault(),
            CreateEmptySnapshots(),
            DateTimeOffset.UtcNow,
            retrieval);

        var memoryItem = Assert.Single(manifest.Items, item => item.SourceId == AgentContextSourceId.DurableMemory);
        Assert.Contains("Influence fact", memoryItem.Content, StringComparison.Ordinal);

        var revision = ParseRevision(memoryItem);
        _influenceRecorder.RecordInfluence(
            _workspaceKey,
            runId,
            sessionId,
            AgentMemoryInfluenceState.Recorded,
            new[] { revision });

        var influencePayload = ReplayInfluencePayload();
        Assert.Equal(AgentMemoryInfluenceState.Recorded, influencePayload.State);
        Assert.Equal(runId.Value, influencePayload.RunId);
        Assert.Equal(sessionId.Value, influencePayload.SessionId);
        Assert.Equal(retrieval.EligibleRecords[0].MemoryId.Value, influencePayload.Revisions[0].MemoryId);
    }

    [Fact]
    public void Influence_UnavailableMarker_IsRecordedTruthfully()
    {
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();

        _influenceRecorder.RecordInfluence(
            _workspaceKey,
            runId,
            sessionId,
            AgentMemoryInfluenceState.Unavailable,
            Array.Empty<AgentMemoryInfluenceRevision>(),
            "Memory partition unavailable.");

        var payload = ReplayInfluencePayload();
        Assert.Equal(AgentMemoryInfluenceState.Unavailable, payload.State);
        Assert.Equal("Memory partition unavailable.", payload.UnavailableReason);
        Assert.Empty(payload.Revisions);
    }

    [Fact]
    public void Manifest_MemoryNeverInsertedWholeSale_UsesPolicyExclusionAndRedaction()
    {
        var actor = Phase21MemoryTestSupport.TestAuthor;
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var conversationId = ConversationId.ForChannel("general");

        var secretContent = "User prefers Bearer sk-abcdefghijklmnopqrstuvwxyz0123456789 tokens";
        _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(actor),
            secretContent,
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "secret-memory"));

        var retrieval = _retriever.Retrieve(
            new AgentMemoryRetrievalRequest(
                _workspaceKey,
                new AgentMemoryRetrievalContext(
                    sessionId,
                    runId,
                    conversationId,
                    actor,
                    projectId: _workspaceKey.Value)));

        var manifest = _manifestBuilder.Build(
            sessionId,
            runId,
            conversationId,
            new AgentContextPolicy(
                AgentContextPolicyLevel.Standard,
                new AgentContextSessionOverride(AgentContextPolicyLevel.Minimal)),
            CreateEmptySnapshots(),
            DateTimeOffset.UtcNow,
            retrieval);

        Assert.DoesNotContain(manifest.Items, item => item.SourceId == AgentContextSourceId.DurableMemory);
        Assert.Contains(
            manifest.ExclusionDecisions,
            decision => decision.SourceId == AgentContextSourceId.DurableMemory);
    }

    private static AgentMemoryInfluenceRevision ParseRevision(AgentContextItem item)
    {
        const string revisionMarker = ":rev:";
        var fingerprint = item.Fingerprint;
        var revisionIndex = fingerprint.IndexOf(revisionMarker, StringComparison.Ordinal);
        Assert.True(revisionIndex >= 0);
        var memoryIdValue = fingerprint.Substring(4, revisionIndex - 4);
        var tail = fingerprint.Substring(revisionIndex + revisionMarker.Length);
        var tailParts = tail.Split(':');
        return new AgentMemoryInfluenceRevision(
            AgentMemoryId.FromValue(memoryIdValue),
            long.Parse(tailParts[0]),
            AgentMemoryLimits.PayloadSchemaVersion,
            tailParts.Length > 1 && tailParts[1] == "stale");
    }

    private InfluencePayload ReplayInfluencePayload()
    {
        var replay = _store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey,
            AgentDurableRecordClass.Memory,
            afterOrderingSequence: 0,
            maxRecords: 64));

        var influence = replay.Records
            .Select(record => JsonSerializer.Deserialize<InfluencePayload>(record.PayloadJson, InfluenceSerializerOptions))
            .Last(payload => payload?.PayloadKind == "memory-influence");

        return influence!;
    }

    private static TestAgentContextSnapshotSources CreateEmptySnapshots() =>
        new()
        {
            Editor = new EditorStateSnapshot(
                generation: 0,
                activeFilePath: null,
                activeFileContent: null,
                openFilePaths: Array.Empty<string>()),
            SourceControl = new SourceControlStatusSnapshot(
                generation: 0,
                availability: SourceControlSnapshotAvailability.NoWorkspace),
            LanguageDiagnostics = LanguageDiagnosticsSnapshot.Empty,
            BuildDiagnostics = BuildDiagnosticsSnapshot.Empty,
            Workflow = new ProjectWorkflowSnapshot(
                ProjectWorkflowOperationState.Idle,
                Generation: 0,
                ActiveOperation: null,
                LastOutcome: null,
                TargetFilePath: null,
                ProcessId: null,
                OutputLines: Array.Empty<ManagedProcessOutputLine>(),
                LastOperation: null),
            TestResults = TestResultsSnapshot.Empty,
            DebugSession = new DebugSessionSnapshot(
                DebugSessionState.Idle,
                Generation: 0,
                ProgramPath: null,
                WorkingDirectory: null,
                AdapterProcessId: null,
                StopInfo: null,
                Failure: null,
                LastOutcome: null,
                DiagnosticOutput: Array.Empty<string>(),
                BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications),
            ProjectContext = new ProjectContext(
                ProjectContextState.Unloaded,
                WorkspaceRoot: "/workspace",
                Candidates: Array.Empty<ProjectCandidate>(),
                SelectedProject: null,
                UnsupportedFiles: Array.Empty<string>(),
                ErrorMessage: null),
        };

    private sealed class InfluencePayload
    {
        public string PayloadKind { get; set; } = string.Empty;

        public string RunId { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        public AgentMemoryInfluenceState State { get; set; }

        public string? UnavailableReason { get; set; }

        public InfluenceRevisionPayload[] Revisions { get; set; } = Array.Empty<InfluenceRevisionPayload>();
    }

    private sealed class InfluenceRevisionPayload
    {
        public string MemoryId { get; set; } = string.Empty;
    }

    private sealed record TestAgentContextSnapshotSources : IAgentContextSnapshotSources
    {
        public required EditorStateSnapshot Editor { get; init; }

        public required SourceControlStatusSnapshot SourceControl { get; init; }

        public required LanguageDiagnosticsSnapshot LanguageDiagnostics { get; init; }

        public required BuildDiagnosticsSnapshot BuildDiagnostics { get; init; }

        public required ProjectWorkflowSnapshot Workflow { get; init; }

        public required TestResultsSnapshot TestResults { get; init; }

        public required DebugSessionSnapshot DebugSession { get; init; }

        public required ProjectContext ProjectContext { get; init; }
    }
}
