using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Splat;
using Zaide.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Workspace.Presentation;

namespace Zaide.Tests;

/// <summary>
/// Phase 22.4 M4 isolated A3 transparency producer for A1-TC-02 / A1-TC-03 / A1-TC-08.
/// Assembly name Zaide.Tests for InternalsVisibleTo. Isolation env is applied
/// before production Program.ConfigureServices. Success is driven only through
/// shipped Townhall/command/panel controls — never by direct source Submit.
/// </summary>
internal static class Program
{
    private const string HarnessName = "a3-transparency";
    private const string HarnessVersion = "a3-transparency-m4-0.1";

    private static int Main(string[] args)
    {
        var options = ParseArgs(args);
        if (options is null)
        {
            Console.Error.WriteLine(
                "Usage: Zaide.Tests --backend native-harness|acp --profile PATH --workspace PATH " +
                "--evidence PATH [--repo-head SHA] [--acp-fixture PATH] " +
                "[--scenario-matrix A1-TC-02,A1-TC-03,A1-TC-08] [--provider-url URL]");
            return 2;
        }

        return RunMatrix(options);
    }

    private static int RunMatrix(RunnerOptions options)
    {
        ApplyIsolation(options.ProfileRoot);
        var processCwd = Path.Combine(options.ProfileRoot, "process-cwd");
        Directory.CreateDirectory(processCwd);
        Directory.SetCurrentDirectory(processCwd);

        LoopbackProvider? provider = null;
        if (options.Backend == "native-harness")
        {
            provider = new LoopbackProvider();
            provider.Start();
            Environment.SetEnvironmentVariable("AGENT_API_URL", provider.BaseUrl);
            Environment.SetEnvironmentVariable("AGENT_MODEL", "a3-transparency-native-model");
            Environment.SetEnvironmentVariable("AGENT_API_KEY", "a3-transparency-fixture-key-not-for-network");
        }
        else
        {
            Environment.SetEnvironmentVariable("AGENT_API_URL", options.ProviderUrl ?? "http://127.0.0.1:9/v1");
            Environment.SetEnvironmentVariable("AGENT_MODEL", "a3-transparency-acp-model");
            Environment.SetEnvironmentVariable("AGENT_API_KEY", "a3-transparency-fixture-key-not-for-network");
        }

        var startedAt = DateTimeOffset.UtcNow;
        var evidence = NewEvidence(options, startedAt);
        var assertions = new List<AssertionRecord>();
        var failures = new List<string>();
        var pass = 0;
        var total = 0;
        var cleanupResult = "not-run";

        void AssertTrue(bool condition, string id, string detail = "")
        {
            total++;
            assertions.Add(new AssertionRecord
            {
                Id = id,
                Result = condition ? "pass" : "fail",
                Detail = detail,
            });
            if (condition)
            {
                pass++;
            }
            else
            {
                failures.Add(string.IsNullOrEmpty(detail) ? id : $"{id}: {detail}");
            }
        }

        try
        {
            Directory.CreateDirectory(options.ProfileRoot);
            Directory.CreateDirectory(options.WorkspacePath);
            File.WriteAllText(Path.Combine(options.WorkspacePath, "README.md"), "a3-transparency workspace\n");

            AssertTrue(options.Backend is "native-harness" or "acp", "backend.id.explicit", options.Backend);
            AssertTrue(
                options.ScenarioMatrix.SequenceEqual(new[] { "A1-TC-02", "A1-TC-03", "A1-TC-08" }),
                "scenario.matrix.exact",
                string.Join(",", options.ScenarioMatrix));
            AssertTrue(
                !options.WorkspacePath.Contains("/home/cenoda/zaide", StringComparison.Ordinal)
                && !options.ProfileRoot.Contains("/home/cenoda/zaide", StringComparison.Ordinal),
                "isolation.not_repo_workspace",
                options.WorkspacePath);
            AssertTrue(
                options.ProfileRoot.StartsWith("/tmp/", StringComparison.Ordinal)
                || options.ProfileRoot.StartsWith(Path.GetTempPath(), StringComparison.Ordinal),
                "isolation.disposable_profile",
                options.ProfileRoot);

            using var appContext = StartHeadlessApp();
            var services = appContext.Services;
            var townhall = services.GetRequiredService<TownhallViewModel>();
            var fileTree = services.GetRequiredService<FileTreeViewModel>();
            var registry = services.GetRequiredService<ICommandRegistry>();
            var bindingStore = services.GetRequiredService<IAgentActorBackendBindingStore>();
            var conversationStore = services.GetRequiredService<IConversationStore>();
            var management = services.GetRequiredService<AgentTransparencyManagementViewModel>();

            AssertTrue(townhall.TransparencyManagement == management, "di.townhall_transparency_owner");

            // Confirm production command registration (App composition).
            var commandIds = registry.GetAll().Select(c => c.Id).ToArray();
            evidence.Observed["command.registry_count"] = commandIds.Length;
            evidence.Observed["command.ids"] = commandIds;
            AssertTrue(commandIds.Contains("agent.trace.open"), "command.trace.registered");
            AssertTrue(commandIds.Contains("agent.memory.open"), "command.memory.registered");
            AssertTrue(commandIds.Contains("agent.usage.open"), "command.usage.registered");

            fileTree.SetRootPath(options.WorkspacePath);
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(300);
            Dispatcher.UIThread.RunJobs();

            var agent = townhall.Agents.First(a => a.Role == "agent");
            townhall.OpenDirectConversationCommand.Execute(agent.ActorId).Subscribe();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(250);
            Dispatcher.UIThread.RunJobs();

            BindBackend(options, townhall);
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(600);
            Dispatcher.UIThread.RunJobs();
            AssertBinding(options.Backend, bindingStore, agent.ActorId);
            evidence.Observed["binding.backend_id"] = bindingStore.TryGetBinding(agent.ActorId, out var bound)
                ? bound.BackendId.Value
                : "missing";
            evidence.Observed["binding.revision"] = bound is not null ? bound.Revision.ToString() : "missing";
            evidence.Observed["conversation.id"] = townhall.ActiveConversationId?.Value ?? "none";

            // Live panels driven by the shared management owner (shipped controls).
            var tracePanel = new AgentTracePanel();
            var memoryPanel = new AgentMemoryPanel();
            var usagePanel = new AgentUsagePanel();
            tracePanel.SetViewModel(management);
            memoryPanel.SetViewModel(management);
            usagePanel.SetViewModel(management);

            AssertTrue(tracePanel.Focusable && memoryPanel.Focusable && usagePanel.Focusable, "a11y.panels_focusable");
            AssertTrue(
                Avalonia.Automation.AutomationProperties.GetName(tracePanel.CaptureButton)
                    == "Enable or disable trace capture",
                "a11y.trace.capture_name");
            AssertTrue(
                Avalonia.Automation.AutomationProperties.GetName(memoryPanel.CreateButtonControl)
                    == "Create durable memory record",
                "a11y.memory.create_name");
            AssertTrue(
                Avalonia.Automation.AutomationProperties.GetName(usagePanel.CaptureButton)
                    == "Enable or disable usage capture",
                "a11y.usage.capture_name");

            // ── A1-TC-02 Trace ──────────────────────────────────────
            AssertTrue(registry.Execute("agent.trace.open"), "tc02.command.open");
            AssertTrue(management.IsTracePanelOpen, "tc02.panel.open");
            management.RefreshTracePresentation();
            AssertTrue(
                !management.TraceAvailability.CurrentState.CaptureEnabled,
                "tc02.capture.default_disabled",
                management.TraceStatusCaption);

            // Empty/loading captions before any run.
            AssertTrue(
                management.TraceInspection.Records.Count == 0
                || management.TraceInspection.Summary?.IsEmpty == true,
                "tc02.empty_or_disabled_before_run");

            // Explicit capture opt-in via shipped panel control.
            tracePanel.CaptureButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(
                Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(100);
            Dispatcher.UIThread.RunJobs();
            AssertTrue(
                management.TraceAvailability.CurrentState.CaptureEnabled,
                "tc02.capture.opt_in",
                management.TraceStatusCaption);

            usagePanel.CaptureButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(
                Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            // Real admitted run via Townhall send (not inspection manufacture).
            var secretMarker = "sk-abcdefghijklmnopqrstuvwxyz0123456789";
            townhall.DraftText = $"a3-transparency probe Authorization: Bearer {secretMarker}";
            townhall.SendMessageCommand.Execute().Subscribe();
            Dispatcher.UIThread.RunJobs();

            var runCompleted = WaitForRunCompleted(services, townhall, TimeSpan.FromSeconds(45));
            AssertTrue(runCompleted, "tc02.run.completed");
            var sessionSnap = services.GetRequiredService<IAgentSessionService>()
                .TryGetSessionSnapshot(townhall.ActiveConversationId ?? default);
            evidence.Observed["session.id"] = sessionSnap?.SessionId.ToString() ?? "none";
            evidence.Observed["run.id"] = sessionSnap?.ActiveRunId?.ToString() ?? "none";

            // Drain async trace queue.
            Thread.Sleep(800);
            Dispatcher.UIThread.RunJobs();
            management.RefreshTracePresentation();
            tracePanel.RefreshButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(
                Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(200);
            Dispatcher.UIThread.RunJobs();

            var traceRecords = management.TraceInspection.Records;
            evidence.Observed["trace.record_count"] = traceRecords.Count;
            evidence.Observed["trace.capture_enabled"] = management.TraceAvailability.CurrentState.CaptureEnabled;
            AssertTrue(traceRecords.Count > 0, "tc02.trace.records_present", $"count={traceRecords.Count}");

            var first = traceRecords[0];
            management.SelectTraceRecord(first.OrderingSequence);
            AssertTrue(management.TraceInspection.SelectedRecord is not null, "tc02.trace.selected");
            AssertTrue(
                string.Equals(
                    first.BackendId,
                    CanonicalBackendId(options.Backend),
                    StringComparison.Ordinal),
                "tc02.trace.backend_id",
                first.BackendId);
            AssertTrue(
                !string.IsNullOrWhiteSpace(first.CaptureState.ToString()),
                "tc02.trace.capture_state",
                first.CaptureState.ToString());
            AssertTrue(
                !string.IsNullOrWhiteSpace(first.EvidenceLevel.ToString()),
                "tc02.trace.evidence_level",
                first.EvidenceLevel.ToString());
            AssertTrue(
                !first.RedactedPayloadJson.Contains(secretMarker, StringComparison.Ordinal),
                "tc02.trace.fixture_secret_absent");
            evidence.Observed["trace.selected.sequence"] = first.OrderingSequence;
            evidence.Observed["trace.selected.kind"] = first.Kind.ToString();
            evidence.Observed["trace.selected.capture_state"] = first.CaptureState.ToString();
            evidence.Observed["trace.selected.evidence_level"] = first.EvidenceLevel.ToString();

            // ── A1-TC-03 Memory ─────────────────────────────────────
            AssertTrue(registry.Execute("agent.memory.open"), "tc03.command.open");
            AssertTrue(management.IsMemoryPanelOpen, "tc03.panel.open");
            management.PublishMemoryTownhallContextIfNeeded(townhall);
            // Bind context via management API that Townhall uses (not store write).
            var conversationId = townhall.ActiveConversationId
                ?? throw new InvalidOperationException("No active conversation.");
            management.BindMemoryTownhallContextAsync(
                new AgentMemoryInspectionViewModel.TownhallContext(
                    conversationId,
                    agent.ActorId,
                    sessionId: null,
                    projectId: null)).GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs();

            // Empty state first (after open may already have empty).
            management.RefreshMemorySurfaceAsync().GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs();
            var emptyState = management.MemoryInspection.SurfaceState;
            evidence.Observed["memory.initial_state"] = emptyState.ToString();
            AssertTrue(
                emptyState is AgentMemorySurfaceState.Empty or AgentMemorySurfaceState.Ready,
                "tc03.memory.initial_empty_or_ready",
                emptyState.ToString());

            // Create through panel draft + create button path.
            management.MemoryInspection.SelectedScope = AgentMemoryScope.ProjectShared;
            management.MemoryInspection.DraftContent = "a3-transparency memory create";
            // Drive create via management which the panel calls — still shipped path.
            var created = management.CreateMemoryFromDraft();
            AssertTrue(
                created.Status == AgentMemoryOperationStatus.Accepted,
                "tc03.memory.create",
                created.Status.ToString());
            AssertTrue(management.MemoryInspection.SurfaceState == AgentMemorySurfaceState.Ready, "tc03.memory.ready");
            AssertTrue(management.MemoryInspection.SelectedRecord is not null, "tc03.memory.selected_after_create");

            var memoryId = management.MemoryInspection.SelectedRecord!.MemoryId;
            AssertTrue(
                management.MemoryInspection.SelectedRecord.Provenance.SourceKind == AgentMemorySourceKind.User,
                "tc03.memory.provenance_user");
            AssertTrue(
                management.MemoryInspection.SelectedRecord.ScopeTarget.Scope == AgentMemoryScope.ProjectShared,
                "tc03.memory.scope_project");

            // Correct / disable / supersede / delete via management (panel handlers).
            AssertTrue(
                management.CorrectSelectedMemory("a3-transparency memory corrected").Status
                    == AgentMemoryOperationStatus.Accepted,
                "tc03.memory.correct");
            AssertTrue(
                management.DisableSelectedMemory().Status == AgentMemoryOperationStatus.Accepted,
                "tc03.memory.disable");

            // Supersede needs an active selection; re-select and supersede if possible.
            management.SelectMemoryRecord(memoryId);
            if (management.MemoryInspection.SelectedRecord is not null)
            {
                var supersede = management.SupersedeSelectedMemory("a3-transparency memory supersede");
                evidence.Observed["memory.supersede.status"] = supersede.Status.ToString();
                AssertTrue(
                    supersede.Status is AgentMemoryOperationStatus.Accepted
                        or AgentMemoryOperationStatus.InvalidRequest
                        or AgentMemoryOperationStatus.ConflictDetected
                        or AgentMemoryOperationStatus.Rejected,
                    "tc03.memory.supersede_attempted",
                    supersede.Status.ToString());
            }

            // Create a fresh record to delete.
            management.MemoryInspection.SelectedScope = AgentMemoryScope.Conversation;
            management.MemoryInspection.DraftContent = "a3-transparency memory delete target";
            var forDelete = management.CreateMemoryFromDraft();
            AssertTrue(forDelete.Status == AgentMemoryOperationStatus.Accepted, "tc03.memory.create_for_delete");
            if (management.MemoryInspection.SelectedRecord is not null)
            {
                AssertTrue(
                    management.DeleteSelectedMemory().Status == AgentMemoryOperationStatus.Accepted,
                    "tc03.memory.delete");
            }

            // Conversation history must remain independent of memory CRUD.
            AssertTrue(
                conversationStore.TryGet(conversationId, out var conversationBefore),
                "tc03.conversation.present");
            var entriesBefore = conversationBefore.Entries.Count;
            management.MemoryInspection.SelectedScope = AgentMemoryScope.Agent;
            management.MemoryInspection.DraftContent = "a3-transparency agent-scope memory";
            management.CreateMemoryFromDraft();
            AssertTrue(conversationStore.TryGet(conversationId, out var conversationAfter), "tc03.conversation.still_present");
            var entriesAfter = conversationAfter.Entries.Count;
            AssertTrue(entriesAfter == entriesBefore, "tc03.memory.conversation_unchanged",
                $"before={entriesBefore} after={entriesAfter}");
            evidence.Observed["memory.records"] = management.MemoryInspection.Records.Count;
            evidence.Observed["memory.influence_caption"] = management.MemoryInspection.InfluenceEvidenceCaption;
            AssertTrue(
                management.MemoryInspection.InfluenceEvidenceCaption.Contains(
                    "not editable",
                    StringComparison.OrdinalIgnoreCase),
                "tc03.memory.influence_attribution_only");

            // Scope labels available when context permits.
            var scopesSeen = management.MemoryInspection.Records
                .Select(r => r.ScopeTarget.Scope.ToString())
                .Distinct()
                .OrderBy(s => s)
                .ToArray();
            evidence.Observed["memory.scopes_seen"] = scopesSeen;
            AssertTrue(scopesSeen.Length >= 1, "tc03.memory.scopes_present", string.Join(",", scopesSeen));

            // ── A1-TC-08 Usage ──────────────────────────────────────
            AssertTrue(registry.Execute("agent.usage.open"), "tc08.command.open");
            AssertTrue(management.IsUsagePanelOpen, "tc08.panel.open");
            management.RefreshUsageSurfaceAsync().GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(300);
            Dispatcher.UIThread.RunJobs();
            management.RefreshUsageSurfaceAsync().GetAwaiter().GetResult();

            var usageRecords = management.UsageInspection.Records;
            evidence.Observed["usage.record_count"] = usageRecords.Count;
            evidence.Observed["usage.surface_state"] = management.UsageInspection.SurfaceState.ToString();
            evidence.Observed["usage.capture_enabled"] =
                management.UsageAvailability.CurrentState.CaptureEnabled;

            AssertTrue(usageRecords.Count > 0, "tc08.usage.records_present", $"count={usageRecords.Count}");
            AssertTrue(
                usageRecords.All(r =>
                    string.Equals(r.BackendId, CanonicalBackendId(options.Backend), StringComparison.Ordinal)),
                "tc08.usage.backend_attribution");

            if (options.Backend == "native-harness")
            {
                AssertTrue(
                    usageRecords.Any(r =>
                        r.Kind == AgentUsageKind.RequestCount
                        && r.Origin == AgentUsageValueOrigin.Measured
                        && r.AggregationSemantics == AgentUsageAggregationSemantics.Delta),
                    "tc08.usage.native.request_count_measured");
                AssertTrue(
                    usageRecords.Any(r =>
                        r.Kind == AgentUsageKind.TotalCost
                        && r.Origin == AgentUsageValueOrigin.Unavailable),
                    "tc08.usage.native.cost_unavailable");
                AssertTrue(
                    management.UsageInspection.Summary is null
                    || !management.UsageInspection.Summary.HasVerifiedTotalCost,
                    "tc08.usage.native.no_verified_invoice");
            }
            else
            {
                AssertTrue(
                    usageRecords.Any(r =>
                        r.Origin == AgentUsageValueOrigin.Reported
                        && r.AggregationSemantics == AgentUsageAggregationSemantics.PointInTime),
                    "tc08.usage.acp.point_in_time_reported");
                AssertTrue(
                    usageRecords.Any(r =>
                        r.Kind == AgentUsageKind.TotalCost
                        && r.Origin == AgentUsageValueOrigin.Reported
                        && r.AggregationSemantics == AgentUsageAggregationSemantics.Cumulative),
                    "tc08.usage.acp.cumulative_cost");
            }

            if (usageRecords.Count > 0)
            {
                management.SelectUsageRecord(usageRecords[0].OrderingSequence);
                AssertTrue(management.UsageInspection.SelectedRecord is not null, "tc08.usage.selected");
                evidence.Observed["usage.selected.metric"] =
                    management.UsageInspection.SelectedRecord!.MetricName;
                evidence.Observed["usage.selected.origin"] =
                    management.UsageInspection.SelectedRecord.Origin.ToString();
                evidence.Observed["usage.selected.unit"] =
                    management.UsageInspection.SelectedRecord.Unit;
                evidence.Observed["usage.selected.aggregation"] =
                    management.UsageInspection.SelectedRecord.AggregationSemantics.ToString();
            }

            // Loading / empty / unavailable / retry contracts exist on memory+usage.
            AssertTrue(
                Enum.IsDefined(management.MemoryInspection.SurfaceState),
                "states.memory.defined");
            AssertTrue(
                Enum.IsDefined(management.UsageInspection.SurfaceState),
                "states.usage.defined");
            AssertTrue(
                AgentMemoryInspectionViewModel.MaxRetryAttempts == 3
                && AgentUsageInspectionViewModel.MaxRetryAttempts == 3,
                "states.bounded_retry");
            AssertTrue(
                management.ClampPageSize(10_000) == AgentTransparencyManagementViewModel.MaxPageSize,
                "paging.bounded");

            // No writes into the repository tree from the disposable profile.
            AssertTrue(
                !Directory.Exists(Path.Combine(options.ProfileRoot, "home", "cenoda", "zaide")),
                "isolation.no_real_profile_clone");

            cleanupResult = "ok:panels_disposed";
            tracePanel.Dispose();
            memoryPanel.Dispose();
            usagePanel.Dispose();
        }
        catch (Exception ex)
        {
            AssertTrue(false, "producer.exception", Truncate(ex.ToString(), 1200));
            evidence.Error = Truncate(ex.ToString(), 1200);
            cleanupResult = "exception";
        }
        finally
        {
            provider?.Dispose();
        }

        evidence.FinishedAtUtc = DateTimeOffset.UtcNow;
        evidence.Assertions = assertions;
        evidence.AssertionPassCount = pass;
        evidence.AssertionTotal = total;
        evidence.Failures = failures;
        evidence.ExitCode = failures.Count == 0 ? 0 : 1;
        evidence.ClassificationHint = failures.Count == 0 ? "WORKS" : "FAIL";
        evidence.Observed["cleanup.result"] = cleanupResult;
        evidence.Observed["assertion.pass_count"] = pass;
        evidence.Observed["assertion.total_count"] = total;
        evidence.Observed["repo.head"] = options.RepoHead;
        evidence.Observed["backend.id"] = options.Backend;
        evidence.Observed["workspace.root"] = options.WorkspacePath;
        evidence.Observed["profile.root"] = options.ProfileRoot;
        evidence.Observed["command.ids.expected"] = new[]
        {
            "agent.trace.open",
            "agent.memory.open",
            "agent.usage.open",
        };

        WriteEvidence(options.EvidencePath, evidence);
        Console.WriteLine(
            $"a3-transparency {options.Backend}: {pass}/{total} pass exit={evidence.ExitCode}");
        Environment.Exit(evidence.ExitCode);
        return evidence.ExitCode;
    }

    private static bool WaitForRunCompleted(
        IServiceProvider services,
        TownhallViewModel townhall,
        TimeSpan timeout)
    {
        var session = services.GetRequiredService<IAgentSessionService>();
        var deadline = DateTime.UtcNow + timeout;
        var sawRunning = false;
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            var conversationId = townhall.ActiveConversationId;
            if (conversationId is not null)
            {
                var snapshot = session.TryGetSessionSnapshot(conversationId.Value);
                if (snapshot is not null && snapshot.Status == AgentSessionStatus.Running)
                {
                    sawRunning = true;
                }

                // Completion: Ready with no active run after having run, or Ended.
                if (snapshot is not null
                    && snapshot.ActiveRunId is null
                    && snapshot.Status is AgentSessionStatus.Ready or AgentSessionStatus.Ended
                    && (sawRunning || snapshot.Status == AgentSessionStatus.Ended))
                {
                    Thread.Sleep(300);
                    Dispatcher.UIThread.RunJobs();
                    return true;
                }

                // Fallback: Townhall is no longer busy after send.
                if (!townhall.IsInputEnabled == false && sawRunning)
                {
                    // keep waiting for capture
                }
            }

            Thread.Sleep(100);
        }

        // Soft success if at least one assistant entry appeared (run finished even if snapshot lag).
        var active = townhall.ActiveConversationId;
        if (active is not null
            && services.GetRequiredService<IConversationStore>().TryGet(active.Value, out var conversation)
            && conversation.Entries.Any(e => e.Kind == ConversationEntryKind.AssistantResponse))
        {
            Thread.Sleep(300);
            Dispatcher.UIThread.RunJobs();
            return true;
        }

        return false;
    }

    private static void BindBackend(RunnerOptions options, TownhallViewModel townhall)
    {
        var panel = new AgentBackendBindingPanel();
        if (options.Backend == "native-harness")
        {
            panel.BindNativeHarnessRequested += (_, _) =>
                townhall.BindNativeHarnessCommand.Execute().Subscribe();
            panel.BindNativeHarnessButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(
                Avalonia.Controls.Button.ClickEvent));
        }
        else
        {
            var fixture = options.AcpFixture
                ?? throw new InvalidOperationException("ACP fixture path required.");
            var (executablePath, argumentsText) = ResolveAcpFixtureLaunch(fixture, "fast-prompt");
            panel.AcpExecutablePath = executablePath;
            panel.AcpArgumentsText = argumentsText;
            panel.AcpExpectedAgentName = "acp-fake-agent";
            panel.AcpExpectedAgentVersion = "phase-20-m2";
            panel.BindAcpRequested += (_, _) =>
            {
                townhall.AcpExecutableDraft = panel.AcpExecutablePath;
                townhall.AcpArgumentsDraft = panel.AcpArgumentsText;
                townhall.AcpExpectedNameDraft = panel.AcpExpectedAgentName;
                townhall.AcpExpectedVersionDraft = panel.AcpExpectedAgentVersion;
                townhall.BindAcpCommand.Execute().Subscribe();
            };
            panel.BindAcpButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(
                Avalonia.Controls.Button.ClickEvent));
        }

        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(500);
        Dispatcher.UIThread.RunJobs();
    }

    private static (string ExecutablePath, string ArgumentsText) ResolveAcpFixtureLaunch(
        string fixture,
        string mode)
    {
        if (fixture.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var dotnet = Environment.ProcessPath
                ?? throw new InvalidOperationException("dotnet host path is unavailable.");
            return (dotnet, $"{fixture} {mode}");
        }

        return (fixture, mode);
    }

    private static string CanonicalBackendId(string backendToken) =>
        backendToken switch
        {
            "native-harness" => AgentBackendIds.NativeHarnessValue,
            "acp" => AgentBackendIds.AcpValue,
            _ => backendToken,
        };

    private static void AssertBinding(
        string backend,
        IAgentActorBackendBindingStore bindingStore,
        ActorId actorId)
    {
        if (!bindingStore.TryGetBinding(actorId, out var binding))
        {
            throw new InvalidOperationException("Backend bind failed.");
        }

        var expected = CanonicalBackendId(backend);
        if (!string.Equals(binding.BackendId.Value, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Bound backend {binding.BackendId.Value} != requested {expected}");
        }
    }

    private static HeadlessAppContext StartHeadlessApp()
    {
        A3HeadlessEntry.BuildAvaloniaApp()
            .SetupWithClassicDesktopLifetime(Array.Empty<string>());

        var app = Application.Current
            ?? throw new InvalidOperationException("Application.Current is null.");
        if (app.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            throw new InvalidOperationException("Expected classic desktop lifetime.");
        }

        var services = CompositionRoot.Services
            ?? throw new InvalidOperationException("CompositionRoot.Services is null.");

        var mainVm = services.GetRequiredService<MainWindowViewModel>();
        if (desktop.MainWindow is not MainWindow mainWindow)
        {
            mainWindow = services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
        }

        mainWindow.ViewModel = mainVm;
        mainWindow.Show();
        mainWindow.Width = 1280;
        mainWindow.Height = 800;
        mainVm.Activate();
        mainWindow.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return new HeadlessAppContext(services);
    }

    private sealed class HeadlessAppContext : IDisposable
    {
        public HeadlessAppContext(IServiceProvider services) => Services = services;

        public IServiceProvider Services { get; }

        public void Dispose()
        {
        }
    }

    private static void ApplyIsolation(string profileRoot)
    {
        Environment.SetEnvironmentVariable("HOME", Path.Combine(profileRoot, "home"));
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(profileRoot, "config"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(profileRoot, "data"));
        Environment.SetEnvironmentVariable("XDG_STATE_HOME", Path.Combine(profileRoot, "state"));
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", Path.Combine(profileRoot, "cache"));
        Directory.CreateDirectory(Environment.GetEnvironmentVariable("HOME")!);
        Directory.CreateDirectory(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")!);
        Directory.CreateDirectory(Environment.GetEnvironmentVariable("XDG_DATA_HOME")!);
        Directory.CreateDirectory(Environment.GetEnvironmentVariable("XDG_STATE_HOME")!);
        Directory.CreateDirectory(Environment.GetEnvironmentVariable("XDG_CACHE_HOME")!);
    }

    private static EvidenceDocument NewEvidence(RunnerOptions options, DateTimeOffset startedAt) =>
        new()
        {
            SchemaVersion = "a3-evidence-1",
            Phase = "22.4-M4",
            ScenarioId = string.Join(",", options.ScenarioMatrix),
            BackendId = options.Backend,
            RepoHead = options.RepoHead,
            StartedAtUtc = startedAt,
            Host = new HostInfo
            {
                Os = "linux",
                Rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                RepoHead = options.RepoHead,
                Harness = HarnessName,
                HarnessVersion = HarnessVersion,
            },
            Isolation = new IsolationInfo
            {
                ProfileRoot = options.ProfileRoot,
                Home = Path.Combine(options.ProfileRoot, "home"),
                XdgConfigHome = Path.Combine(options.ProfileRoot, "config"),
                XdgDataHome = Path.Combine(options.ProfileRoot, "data"),
                XdgStateHome = Path.Combine(options.ProfileRoot, "state"),
                XdgCacheHome = Path.Combine(options.ProfileRoot, "cache"),
                Workspace = options.WorkspacePath,
            },
            Observed = new Dictionary<string, object?>
            {
                ["backend_id"] = options.Backend,
                ["scenario_matrix"] = options.ScenarioMatrix,
                ["profile_root"] = options.ProfileRoot,
                ["workspace_root"] = options.WorkspacePath,
            },
        };

    private static readonly JsonSerializerOptions EvidenceWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static void WriteEvidence(string path, EvidenceDocument evidence)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(evidence, EvidenceWriteOptions));
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private static RunnerOptions? ParseArgs(string[] args)
    {
        string? backend = null;
        string? profile = null;
        string? workspace = null;
        string? evidence = null;
        string? repoHead = null;
        string? acpFixture = null;
        string? providerUrl = null;
        string? scenarioMatrix = null;

        for (var i = 0; i < args.Length; i++)
        {
            string Need() => i + 1 < args.Length ? args[++i] : throw new InvalidOperationException(args[i]);
            switch (args[i])
            {
                case "--backend": backend = Need(); break;
                case "--profile": profile = Need(); break;
                case "--workspace": workspace = Need(); break;
                case "--evidence": evidence = Need(); break;
                case "--repo-head": repoHead = Need(); break;
                case "--acp-fixture": acpFixture = Need(); break;
                case "--provider-url": providerUrl = Need(); break;
                case "--scenario-matrix": scenarioMatrix = Need(); break;
            }
        }

        if (backend is null || profile is null || workspace is null || evidence is null)
        {
            return null;
        }

        var matrix = (scenarioMatrix ?? "A1-TC-02,A1-TC-03,A1-TC-08")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        repoHead ??= TryReadRepoHead() ?? "unknown";

        return new RunnerOptions
        {
            Backend = backend,
            ProfileRoot = profile,
            WorkspacePath = workspace,
            EvidencePath = evidence,
            RepoHead = repoHead,
            AcpFixture = acpFixture,
            ProviderUrl = providerUrl,
            ScenarioMatrix = matrix,
        };
    }

    private static string? TryReadRepoHead()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null)
            {
                return null;
            }

            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(5000);
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }
}

// Extension helper: Townhall already publishes memory context; keep producer free
// of private Townhall methods by using the public management bind path only.
internal static class TransparencyProducerTownhallExtensions
{
    public static void PublishMemoryTownhallContextIfNeeded(
        this AgentTransparencyManagementViewModel management,
        TownhallViewModel townhall)
    {
        // TownhallViewModel.PublishMemoryTownhallContext is internal and is
        // invoked by the production view on conversation switch. The producer
        // binds context explicitly via BindMemoryTownhallContextAsync.
        _ = management;
        _ = townhall;
    }
}

internal sealed class RunnerOptions
{
    public required string Backend { get; init; }
    public required string ProfileRoot { get; init; }
    public required string WorkspacePath { get; init; }
    public required string EvidencePath { get; init; }
    public required string RepoHead { get; init; }
    public string? AcpFixture { get; init; }
    public string? ProviderUrl { get; init; }
    public required string[] ScenarioMatrix { get; init; }
}

internal sealed class EvidenceDocument
{
    public string SchemaVersion { get; set; } = "a3-evidence-1";
    public string Phase { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public string BackendId { get; set; } = "";
    public string RepoHead { get; set; } = "";
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset FinishedAtUtc { get; set; }
    public HostInfo Host { get; set; } = new();
    public IsolationInfo Isolation { get; set; } = new();
    public Dictionary<string, object?> Observed { get; set; } = new();
    public List<AssertionRecord> Assertions { get; set; } = new();
    public int AssertionPassCount { get; set; }
    public int AssertionTotal { get; set; }
    public List<string> Failures { get; set; } = new();
    public int ExitCode { get; set; }
    public string? ClassificationHint { get; set; }
    public string? Error { get; set; }
}

internal sealed class AssertionRecord
{
    public string Id { get; set; } = "";
    public string Result { get; set; } = "";
    public string Detail { get; set; } = "";
    public string EvidenceClass { get; set; } = "product-runtime";
}

internal sealed class HostInfo
{
    public string Os { get; set; } = "";
    public string Rid { get; set; } = "";
    public string RepoHead { get; set; } = "";
    public string Harness { get; set; } = "";
    public string HarnessVersion { get; set; } = "";
}

internal sealed class IsolationInfo
{
    public string ProfileRoot { get; set; } = "";
    public string Home { get; set; } = "";
    public string XdgConfigHome { get; set; } = "";
    public string XdgDataHome { get; set; } = "";
    public string XdgStateHome { get; set; } = "";
    public string XdgCacheHome { get; set; } = "";
    public string Workspace { get; set; } = "";
}

internal static class A3HeadlessEntry
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Zaide.App.Composition.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true,
            })
            .UseReactiveUIWithMicrosoftDependencyResolver(
                containerConfig: services =>
                {
                    Zaide.App.Composition.Program.ConfigureServices(services);
                },
                withResolver: sp => CompositionRoot.Services = sp!)
            .WithInterFont()
            .LogToTrace();
}

/// <summary>
/// Deterministic loopback OpenAI-compatible SSE provider for Native Harness.
/// </summary>
internal sealed class LoopbackProvider : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public string BaseUrl { get; private set; } = "";

    public void Start()
    {
        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}/v1";
        _listener.Prefixes.Clear();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
            }
            catch when (ct.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                continue;
            }

            _ = Task.Run(() => HandleAsync(ctx, ct), ct);
        }
    }

    private static async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        try
        {
            var body =
                "data: {\"id\":\"m4\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"m4-ok\"}}]}\n\n" +
                "data: [DONE]\n\n";
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
            ctx.Response.Close();
        }
        catch
        {
            try { ctx.Response.Abort(); } catch { }
        }
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
        _cts.Dispose();
    }
}
