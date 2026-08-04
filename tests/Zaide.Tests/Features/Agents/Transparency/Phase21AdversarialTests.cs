using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using Zaide.Tests.Architecture;

namespace Zaide.Tests.Features.Agents.Transparency;

/// <summary>
/// Phase 21 M7 — adversarial closeout for the integrated Phase 21 surface.
/// Maps every M7-required coverage row to a live regression test already
/// admitted by M1–M6. M7 adds no product behavior; this class only proves
/// the required coverage exists and the forbidden exclusions remain absent.
/// </summary>
public sealed class Phase21AdversarialTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    public static IEnumerable<object[]> M7CoverageCases =>
        new List<object[]>
        {
            // --- M2: redaction, secret variants, sensitive files, malformed/oversized traces, backpressure ---
            Row("M7-01 redaction pass-through", "Phase21RedactionTests", "Apply_PassesThroughSafePayload"),
            Row("M7-02 secret variant OpenAI", "Phase21RedactionTests", "Apply_RedactsOpenAiApiKey"),
            Row("M7-03 secret variant AWS", "Phase21RedactionTests", "Apply_RedactsAwsAccessKey"),
            Row("M7-04 secret variant PEM", "Phase21RedactionTests", "Apply_RedactsPEMPrivateKey"),
            Row("M7-05 secret variant connection string", "Phase21RedactionTests", "Apply_RedactsConnectionStringPassword"),
            Row("M7-06 secret variant hex token", "Phase21RedactionTests", "Apply_RedactsHexSecretLabel"),
            Row("M7-07 redaction fail-closed null", "Phase21RedactionTests", "Apply_IsFailClosedOnNullPayload"),
            Row("M7-08 redaction strips UTF-8 BOM", "Phase21RedactionTests", "Apply_StripsUtf8BomBeforeScanning"),
            Row("M7-09 redacted state when any pattern matches", "Phase21RedactionTests", "Apply_ReachesRedactedStateWhenAnyPatternMatches"),
            Row("M7-10 byte-count preserved for size enforcement", "Phase21RedactionTests", "Apply_PreservesByteCountForSizeEnforcement"),
            Row("M7-11 capture unavailable marker bypasses redaction", "Phase21TraceLifecycleTests", "Capture_UnavailableMarkerBypassesRedaction"),
            Row("M7-12 oversized payload truncated to bounded marker", "Phase21TraceLifecycleTests", "Capture_OversizedPayloadIsTruncatedToBoundedMarker"),
            Row("M7-13 empty payload rejected as invalid request", "Phase21TraceLifecycleTests", "Capture_EmptyPayloadIsRejectedAsInvalidRequest"),
            Row("M7-14 backpressure reported when queue full", "Phase21TraceLifecycleTests", "Capture_BackpressureIsReportedWhenQueueIsFull"),
            Row("M7-15 capture rejects unknown backend", "Phase21TraceLifecycleTests", "Capture_RejectsBackendThatIsNotInSourceRegistry"),
            Row("M7-16 ACP redacts sensitive method name", "Phase21TraceBackendAdapterTests", "AcpSource_RedactsSensitiveMethodName"),
            Row("M7-17 ACP submits frame without leaking secret body", "Phase21TraceBackendAdapterTests", "AcpSource_SubmitsProtocolFrameWithoutLeakingSecretBody"),
            Row("M7-18 native harness submits redacted loop history", "Phase21TraceBackendAdapterTests", "NativeHarnessSource_SubmitsRedactedLoopHistoryTurn"),
            Row("M7-19 capture sink always runs redaction before admit", "Phase21TraceRatchetTests", "CaptureSink_AlwaysRunsRedactionBeforeAdmit"),
            Row("M7-20 capture sink fail-closed on redaction failure", "Phase21TraceRatchetTests", "CaptureSink_FailsClosedOnRedactionFailure"),
            Row("M7-21 bounded queue exposes limits and dropped counter", "Phase21TraceRatchetTests", "BoundedQueue_ExposesLimitsAndDroppedCounter"),
            Row("M7-22 trace pipeline files do not leak unredacted payload names", "Phase21TraceRatchetTests", "TracePipelineFiles_DoNotLeakUnredactedPayloadNames"),
            Row("M7-23 native harness source does not reference backend private types", "Phase21TraceRatchetTests", "NativeHarnessSource_DoesNotReferenceBackendPrivateTypes"),
            Row("M7-24 ACP source does not reference backend private types", "Phase21TraceRatchetTests", "AcpSource_DoesNotReferenceBackendPrivateTypes"),
            Row("M7-25 capture files do not write conversation store", "Phase21TraceRatchetTests", "TraceCapture_FilesDoNotWriteConversationStore"),
            Row("M7-26 capture routes through M1 trace record class", "Phase21TraceRatchetTests", "TraceCapture_RoutesThroughM1TraceRecordClass"),
            Row("M7-27 capture sink rejects unknown backend", "Phase21TraceRatchetTests", "TraceCapture_SinkRejectsUnknownBackend"),
            Row("M7-28 trace files feature-owned, not root infrastructure", "Phase21TraceRatchetTests", "TracePipelineFiles_AreFeatureOwned_NotRootInfrastructure"),
            Row("M7-29 trace files do not reference conversation persistence path", "Phase21TraceRatchetTests", "TracePipelineFiles_DoNotReferenceConversationPersistencePath"),
            Row("M7-30 capture default limits enforce truncation and queue bound", "Phase21TraceRatchetTests", "TraceCapture_DefaultLimitsEnforceTruncationAndQueueBound"),
            // --- M6: export, backup, restore ---
            Row("M7-31 export preserves record-owner semantics and schema markers", "Phase21ExportTests", "Export_PreservesRecordOwnerSemanticsAndSchemaMarkers"),
            Row("M7-32 export all record classes remain independent", "Phase21TransparencyIntegrationTests", "Export_AllRecordClassesRemainIndependent"),
            Row("M7-33 backup/restore round trip preserves partition", "Phase21BackupTests", "Backup_Restore_RoundTripPreservesPartition"),
            // --- M3: usage duplicates, unit/currency, stale/missing pricing, disputed evidence, no-zero fallback ---
            Row("M7-34 usage duplicate idempotency key is ignored", "Phase21UsageLifecycleTests", "Capture_DuplicateIdempotencyKeyIsIgnored"),
            Row("M7-35 usage never defaults missing cost to zero", "Phase21UsageLifecycleTests", "Capture_NeverDefaultsMissingCostToZero"),
            Row("M7-36 usage rejects cost with zero and reported origin", "Phase21UsageLifecycleTests", "Capture_RejectsCostWithZeroAndReportedOrigin"),
            Row("M7-37 usage captured cost preserves currency and pricing source", "Phase21CostEvidenceTests", "CapturedCost_PreservesCurrencyAndPricingSource"),
            Row("M7-38 usage captured cost distinguishes origin", "Phase21CostEvidenceTests", "CapturedCost_DistinguishesOrigin"),
            Row("M7-39 usage cost with disputed origin is preserved", "Phase21CostEvidenceTests", "CapturedCost_WithDisputedOriginIsPreserved"),
            Row("M7-40 usage cost with unavailable origin does not default to zero", "Phase21CostEvidenceTests", "CapturedCost_WithUnavailableOriginDoesNotDefaultToZero"),
            Row("M7-41 usage summary tracks total cost, value, and currency", "Phase21CostEvidenceTests", "Summary_TracksTotalCostValueAndCurrency"),
            Row("M7-42 usage calculated cost preserves formula and source version", "Phase21UsageCalculationTests", "CalculatedCost_PreservesFormulaAndSourceVersion"),
            Row("M7-43 usage measured latency preserves unit and value", "Phase21UsageCalculationTests", "MeasuredLatency_PreservesUnitAndValue"),
            Row("M7-44 usage reported tokens preserves model attribution", "Phase21UsageCalculationTests", "ReportedTokens_PreservesModelAttribution"),
            Row("M7-45 usage value origin includes all required distinctions", "Phase21UsageRatchetTests", "UsageValueOrigin_IncludesAllRequiredDistinctions"),
            Row("M7-46 usage capture limits enforce positive bounds", "Phase21UsageRatchetTests", "UsageCaptureLimits_EnforcePositiveBounds"),
            Row("M7-47 usage pipeline routes through M1 usage record class", "Phase21UsageRatchetTests", "UsagePipeline_RoutesThroughM1UsageRecordClass"),
            Row("M7-48 usage files feature-owned, not root infrastructure", "Phase21UsageRatchetTests", "UsagePipelineFiles_AreFeatureOwned_NotRootInfrastructure"),
            Row("M7-49 usage pipeline files do not write conversation store", "Phase21UsageRatchetTests", "UsagePipelineFiles_DoNotWriteConversationStore"),
            Row("M7-50 native harness usage source does not reference backend private types", "Phase21UsageRatchetTests", "NativeHarnessSource_DoesNotReferenceBackendPrivateTypes"),
            Row("M7-51 ACP usage source does not reference backend private types", "Phase21UsageRatchetTests", "AcpSource_DoesNotReferenceBackendPrivateTypes"),
            Row("M7-52 usage pipeline files do not reference trace namespace", "Phase21UsageRatchetTests", "UsagePipelineFiles_DoNotReferenceTraceNamespace"),
            // --- M1: clean shutdown, crash, partial write, corrupt store, unsupported version, interrupted migration ---
            Row("M7-53 interrupted index write does not replace committed index", "Phase21StorageTests", "InterruptedIndexWrite_DoesNotReplaceCommittedIndex"),
            Row("M7-54 storage append assigns monotonic ordering per record class", "Phase21StorageTests", "Append_AssignsMonotonicOrderingPerRecordClass"),
            Row("M7-55 storage append ignores duplicate idempotency key", "Phase21StorageTests", "Append_IgnoresDuplicateIdempotencyKey"),
            Row("M7-56 storage replay returns ordered records after cursor", "Phase21StorageTests", "Replay_ReturnsOrderedRecordsAfterCursor"),
            Row("M7-57 migration V0 to V1 with pre-migration backup", "Phase21MigrationTests", "Load_MigratesV0IndexWithPreMigrationBackup"),
            Row("M7-58 migration unsupported future version disables writes", "Phase21MigrationTests", "Load_UnsupportedFutureVersion_DisablesWrites"),
            Row("M7-59 migration quarantines unreadable record without deleting committed index", "Phase21MigrationTests", "Load_QuarantinesUnreadableRecordWithoutDeletingCommittedIndex"),
            Row("M7-60 envelope requires schema version ordering and idempotency key", "Phase21RecordContractTests", "Envelope_RequiresSchemaVersionOrderingAndIdempotencyKey"),
            Row("M7-61 retention policy owns distinct defaults per record class", "Phase21RecordContractTests", "RetentionPolicy_OwnsDistinctDefaultsPerRecordClass"),
            Row("M7-62 storage key is stable for same workspace root", "Phase21RecordContractTests", "WorkspaceStorageKey_FromWorkspaceRoot_IsStableForSamePath"),
            Row("M7-63 different workspace roots use distinct partitions", "Phase21WorkspaceIsolationTests", "DifferentWorkspaceRoots_UseDistinctPartitions"),
            Row("M7-64 reloaded store does not leak records across workspaces", "Phase21WorkspaceIsolationTests", "ReloadedStore_DoesNotLeakRecordsAcrossWorkspaces"),
            Row("M7-65 durable record store is agents infrastructure owned", "Phase21StorageOwnershipRatchetTests", "DurableRecordStore_IsAgentsInfrastructureOwned"),
            Row("M7-66 durable record paths isolated from conversation persistence", "Phase21StorageOwnershipRatchetTests", "DurableRecordPaths_AreIsolatedFromConversationPersistence"),
            Row("M7-67 durable record store does not reference conversation store", "Phase21StorageOwnershipRatchetTests", "DurableRecordStore_DoesNotReferenceConversationStore"),
            Row("M7-68 migrate loads workspace without cross-class deletion", "Phase21TransparencyIntegrationTests", "Migrate_LoadsWorkspaceWithoutCrossClassDeletion"),
            // --- M4: clean shutdown, restart, multi-window contention, replay gaps/duplicates, idempotent startup ---
            Row("M7-69 restart reconcile classifies interrupted session without live session", "Phase21RestartTests", "Restart_ReconcileClassifiesInterruptedSessionWithoutLiveSession"),
            Row("M7-70 startup reconciler is idempotent", "Phase21RestartTests", "StartupReconciler_IsIdempotent"),
            Row("M7-71 disconnect and late completion remain representable", "Phase21RestartTests", "DisconnectAndLateCompletion_RemainRepresentableInCheckpoint"),
            Row("M7-72 backend capability matrix reports sibling backends independently", "Phase21RestartTests", "BackendCapabilityMatrix_ReportsSiblingBackendsIndependently"),
            Row("M7-73 recovery classification resume explicit user action", "Phase21RecoveryTests", "Resume_WhenBackendResumeUnusable_ReturnsIndeterminateWithoutLiveSession"),
            Row("M7-74 recovery resume idempotent for same idempotency key", "Phase21RecoveryTests", "Resume_IsIdempotent_ForSameIdempotencyKey"),
            Row("M7-75 recovery rejects identity mismatch", "Phase21RecoveryTests", "Resume_RejectsIdentityMismatch"),
            Row("M7-76 recovery reconcile does not auto-resume side-effecting work", "Phase21RecoveryTests", "Reconcile_DoesNotAutoResumeSideEffectingWork"),
            Row("M7-77 termination records local intent separately from backend ack", "Phase21TerminationTests", "Terminate_RecordsLocalIntentSeparatelyFromBackendAcknowledgement"),
            Row("M7-78 termination abandon is distinct from terminate", "Phase21TerminationTests", "Abandon_IsDistinctFromTerminate"),
            Row("M7-79 termination idempotent for same idempotency key", "Phase21TerminationTests", "Terminate_IsIdempotent_ForSameIdempotencyKey"),
            Row("M7-80 termination does not claim provider deletion without evidence", "Phase21TerminationTests", "Terminate_DoesNotClaimProviderDeletionWithoutEvidence"),
            Row("M7-81 continuity pipeline routes through M1 session recovery record class", "Phase21RecoveryRatchetTests", "ContinuityPipeline_RoutesThroughM1SessionRecoveryRecordClass"),
            Row("M7-82 continuity files feature-owned, not root infrastructure", "Phase21RecoveryRatchetTests", "ContinuityPipelineFiles_AreFeatureOwned_NotRootInfrastructure"),
            Row("M7-83 continuity files do not write conversation store", "Phase21RecoveryRatchetTests", "ContinuityPipelineFiles_DoNotWriteConversationStore"),
            Row("M7-84 native harness adapter does not reference ACP private types", "Phase21RecoveryRatchetTests", "NativeHarnessContinuityAdapter_DoesNotReferenceAcpPrivateTypes"),
            Row("M7-85 ACP adapter does not reference native harness private types", "Phase21RecoveryRatchetTests", "AcpContinuityAdapter_DoesNotReferenceNativeHarnessPrivateTypes"),
            Row("M7-86 backend capability matrix defines both sibling backends", "Phase21RecoveryRatchetTests", "BackendCapabilityMatrix_DefinesBothSiblingBackends"),
            // --- M4 / Phase 17: permission decisions never replayed; TryConsume remains final ---
            Row("M7-87 phase 17 stale base through bridge does not consume published decision", "Phase20PermissionTests", "Phase20Permission_StaleBaseThroughBridge_DoesNotConsumePublishedDecision"),
            Row("M7-88 phase 17 ACP choice does not consume broker decision", "Phase20PermissionTests", "Phase20Permission_AcpChoice_DoesNotConsumeBrokerDecision"),
            Row("M7-89 phase 18 context assembly does not bypass policy boundary", "Phase18ContextBypassRatchetTests", "ContextAssembly_DoesNotBypassPolicyBoundary"),
            Row("M7-90 phase 18 context assembly service requires policy matrix registration", "Phase18ContextBypassRatchetTests", "ContextAssemblyService_RequiresPolicyMatrixRegistration"),
            Row("M7-91 phase 18 context manifest does not leak to legacy backend", "Phase18ContextBypassRatchetTests", "ContextManifest_DoesNotLeakToLegacyBackend"),
            Row("M7-92 phase 18 native harness consumes manifest only through system prompt builder", "Phase18ContextBypassRatchetTests", "NativeHarness_ConsumesContextManifestOnlyThroughSystemPromptBuilder"),
            Row("M7-93 phase 18 ACP consumes manifest only through context manifest encoder", "Phase18ContextBypassRatchetTests", "Acp_ConsumesContextManifestOnlyThroughContextManifestEncoder"),
            Row("M7-94 phase 17 backends do not reference editor file IO or workflow runners", "Phase17BypassRatchetTests", "AgentBackends_DoNotReferenceEditorFileIoOrWorkflowRunners"),
            Row("M7-95 phase 17 control plane does not write conversation store outside projection", "Phase17BypassRatchetTests", "ControlPlane_DoesNotWriteConversationStoreOutsideProjection"),
            // --- M5/M6: memory poisoning, stale/conflicting/superseded/deleted/disabled, cross-workspace isolation, budget enforcement, influence attribution ---
            Row("M7-96 memory create flags poisoning suspect patterns", "Phase21MemoryPolicyTests", "Create_FlagsPoisoningSuspectPatterns"),
            Row("M7-97 memory create flags import source as poisoning suspect", "Phase21MemoryPolicyTests", "Create_FlagsImportSourceAsPoisoningSuspect"),
            Row("M7-98 memory create detects content conflict for same scope", "Phase21MemoryPolicyTests", "Create_DetectsContentConflictForSameScope"),
            Row("M7-99 memory create flags stale validation timestamp", "Phase21MemoryPolicyTests", "Create_FlagsStaleValidationTimestamp"),
            Row("M7-100 memory correct rejects deleted memory", "Phase21MemoryPolicyTests", "Correct_RejectsDeletedMemory"),
            Row("M7-101 memory supersede rejects scope mismatch", "Phase21MemoryPolicyTests", "Supersede_RejectsScopeMismatch"),
            Row("M7-102 memory create rejects oversized content", "Phase21MemoryPolicyTests", "Create_RejectsOversizedContent"),
            Row("M7-103 memory retrieve eligible active ranked deterministically", "Phase21MemoryRetrievalTests", "Retrieve_EligibleActiveMemory_IsRankedDeterministically"),
            Row("M7-104 memory retrieve disabled/deleted/superseded/poisoning not retrieved", "Phase21MemoryRetrievalTests", "Retrieve_DisabledDeletedSupersededPoisoning_AreNotRetrieved"),
            Row("M7-105 memory retrieve out-of-scope conversation memory excluded", "Phase21MemoryRetrievalTests", "Retrieve_OutOfScopeConversationMemory_IsExcluded"),
            Row("M7-106 memory retrieve stale fact remains eligible with marker", "Phase21MemoryRetrievalTests", "Retrieve_StaleFact_RemainsEligibleWithMarker"),
            Row("M7-107 memory influence records revisions included in manifest", "Phase21MemoryInfluenceTests", "Influence_RecordsMemoryRevisionsIncludedInManifest"),
            Row("M7-108 memory influence unavailable marker recorded truthfully", "Phase21MemoryInfluenceTests", "Influence_UnavailableMarker_IsRecordedTruthfully"),
            Row("M7-109 memory manifest never inserted wholesale", "Phase21MemoryInfluenceTests", "Manifest_MemoryNeverInsertedWholeSale_UsesPolicyExclusionAndRedaction"),
            Row("M7-110 memory create persists scoped memory with provenance", "Phase21MemoryStoreTests", "Create_PersistsScopedMemoryWithProvenance"),
            Row("M7-111 memory correct updates content without rewriting history", "Phase21MemoryStoreTests", "Correct_UpdatesContentWithoutRewritingHistory"),
            Row("M7-112 memory disable marks memory non-retrievable", "Phase21MemoryStoreTests", "Disable_MarksMemoryNonRetrievable"),
            Row("M7-113 memory supersede links replacement and marks old superseded", "Phase21MemoryStoreTests", "Supersede_LinksReplacementAndMarksOldRecordSuperseded"),
            Row("M7-114 memory delete tombstones without removing audit trail", "Phase21MemoryStoreTests", "Delete_TombstonesMemoryWithoutRemovingAuditTrail"),
            Row("M7-115 memory cross-workspace access denied by default", "Phase21MemoryStoreTests", "CrossWorkspace_AccessIsDeniedByDefault"),
            Row("M7-116 memory export includes schema version and provenance", "Phase21MemoryLifecycleTests", "Export_IncludesSchemaVersionAndProvenance"),
            Row("M7-117 memory backup preserves deleted tombstones for audit", "Phase21MemoryLifecycleTests", "Backup_PreservesDeletedTombstonesForAudit"),
            Row("M7-118 memory replay idempotent operations preserve single logical outcome", "Phase21MemoryLifecycleTests", "Replay_IdempotentOperationsPreserveSingleLogicalOutcome"),
            Row("M7-119 memory inspector summary reflects lifecycle counts", "Phase21MemoryLifecycleTests", "Inspector_SummaryReflectsLifecycleCounts"),
            Row("M7-120 memory migration uses M1 memory record class partition", "Phase21MemoryLifecycleTests", "Migration_UsesM1MemoryRecordClassPartition"),
            Row("M7-121 memory retention default is user-controlled, no automatic expiry", "Phase21MemoryLifecycleTests", "Retention_DefaultIsUserControlled_NoAutomaticExpiry"),
            Row("M7-122 memory pipeline routes through M1 memory record class", "Phase21MemoryRatchetTests", "MemoryPipeline_RoutesThroughM1MemoryRecordClass"),
            Row("M7-123 memory context source policy matrix defines durable memory source", "Phase21MemoryRatchetTests", "ContextSourcePolicyMatrix_DefinesDurableMemorySource"),
            Row("M7-124 memory retrieval integrates only through context manifest builder", "Phase21MemoryRatchetTests", "MemoryRetrieval_IntegratesOnlyThroughContextManifestBuilder"),
            Row("M7-125 memory scopes include all required admitted scopes", "Phase21MemoryRatchetTests", "MemoryScopes_IncludeAllRequiredAdmittedScopes"),
            Row("M7-126 memory operations include inspect/correct/disable/supersede/delete", "Phase21MemoryRatchetTests", "MemoryOperations_IncludeInspectCorrectDisableSupersedeDelete"),
            Row("M7-127 memory coordinator enforces cross-workspace denial", "Phase21MemoryRatchetTests", "MemoryCoordinator_EnforcesCrossWorkspaceDenial"),
            // --- Deletion independence across record classes ---
            Row("M7-128 export all record classes remain independent", "Phase21TransparencyIntegrationTests", "Export_AllRecordClassesRemainIndependent"),
            Row("M7-129 migrate loads workspace without cross-class deletion", "Phase21TransparencyIntegrationTests", "Migrate_LoadsWorkspaceWithoutCrossClassDeletion"),
            Row("M7-130 memory backup preserves deleted tombstones for audit", "Phase21MemoryLifecycleTests", "Backup_PreservesDeletedTombstonesForAudit"),
            // --- Equal Native Harness/ACP placement with truthful unavailable states ---
            Row("M7-131 native harness trace source exposes expected backend id", "Phase21TraceBackendAdapterTests", "NativeHarnessSource_ExposesExpectedBackendId"),
            Row("M7-132 ACP trace source exposes expected backend id", "Phase21TraceBackendAdapterTests", "AcpSource_ExposesExpectedBackendId"),
            Row("M7-133 native harness usage source exposes expected backend id", "Phase21UsageBackendAdapterTests", "NativeHarnessSource_ExposesExpectedBackendId"),
            Row("M7-134 ACP usage source exposes expected backend id", "Phase21UsageBackendAdapterTests", "AcpSource_ExposesExpectedBackendId"),
            Row("M7-135 native harness trace source can report unavailable", "Phase21TraceBackendAdapterTests", "NativeHarnessSource_CanReportUnavailable"),
            Row("M7-136 backend evidence sources are independent siblings", "Phase21UsageBackendAdapterTests", "BackendSources_AreIndependentSiblings"),
            Row("M7-137 townhall screen-reader and keyboard metadata", "Phase21TownhallAccessibilityTests", "TransparencyManagement_ExposesScreenReaderAndKeyboardMetadata"),
            Row("M7-138 townhall bounded paging defaults are stable", "Phase21TownhallAccessibilityTests", "TransparencyManagement_BoundedPagingDefaultsAreStable"),
            // --- Continued absence of Phase 21 exclusions ---
            Row("M7-139 capture sink always runs redaction (exclusion: no unredacted retain)", "Phase21TraceRatchetTests", "CaptureSink_AlwaysRunsRedactionBeforeAdmit"),
            Row("M7-140 capture files do not write conversation store (exclusion: no store bypass)", "Phase21TraceRatchetTests", "TraceCapture_FilesDoNotWriteConversationStore"),
            Row("M7-141 trace files do not reference conversation persistence path (exclusion: no store bypass)", "Phase21TraceRatchetTests", "TracePipelineFiles_DoNotReferenceConversationPersistencePath"),
            Row("M7-142 usage files do not write conversation store (exclusion: no store bypass)", "Phase21UsageRatchetTests", "UsagePipelineFiles_DoNotWriteConversationStore"),
            Row("M7-143 memory files do not write conversation store (exclusion: no store bypass)", "Phase21MemoryRatchetTests", "MemoryPipelineFiles_DoNotWriteConversationStore"),
            Row("M7-144 continuity files do not write conversation store (exclusion: no store bypass)", "Phase21RecoveryRatchetTests", "ContinuityPipelineFiles_DoNotWriteConversationStore"),
            Row("M7-145 trace files feature-owned, not root infrastructure (exclusion: no root infrastructure)", "Phase21TraceRatchetTests", "TracePipelineFiles_AreFeatureOwned_NotRootInfrastructure"),
            Row("M7-146 usage files feature-owned, not root infrastructure (exclusion: no root infrastructure)", "Phase21UsageRatchetTests", "UsagePipelineFiles_AreFeatureOwned_NotRootInfrastructure"),
            Row("M7-147 memory files feature-owned, not root infrastructure (exclusion: no root infrastructure)", "Phase21MemoryRatchetTests", "MemoryPipelineFiles_AreFeatureOwned_NotRootInfrastructure"),
            Row("M7-148 continuity files feature-owned, not root infrastructure (exclusion: no root infrastructure)", "Phase21RecoveryRatchetTests", "ContinuityPipelineFiles_AreFeatureOwned_NotRootInfrastructure"),
        };

    [Theory]
    [MemberData(nameof(M7CoverageCases))]
    public void Phase21Adversarial_M7RequiredCoverage_RegressionTestExists(
        string threatId,
        string typeName,
        string methodName)
    {
        var assembly = typeof(Phase21AdversarialTests).Assembly;
        var type = assembly.GetTypes().SingleOrDefault(candidate =>
            candidate.Name == typeName && candidate.Namespace!.StartsWith("Zaide.Tests", StringComparison.Ordinal));

        Assert.NotNull(type);
        var method = type!.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            ?? type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        Assert.False(string.IsNullOrWhiteSpace(threatId));
    }

    [Fact]
    public void Phase21Adversarial_M7DoesNotAddNewProductionTypes()
    {
        // M7 is closeout-only; the M0 architecture inventory baseline must
        // remain unchanged. If M7 added new production types this test
        // would fail with a count mismatch.
        var inventory = new ArchitectureInventoryReader().Read();

        Assert.Equal(ArchitectureInventoryReader.M0TotalTopLevelTypes, inventory.TotalTopLevelTypeCount);
        Assert.Equal(ArchitectureInventoryReader.M0PublicTopLevelTypes, inventory.PublicTopLevelTypeCount);
        Assert.Equal(ArchitectureInventoryReader.M0InternalTopLevelTypes, inventory.InternalTopLevelTypeCount);
    }

    [Fact]
    public void Phase21Adversarial_Phase21Sources_ContinueToExcludeConversationStoreWrites()
    {
        // Phase 21 must not write directly to the conversation persistence
        // path; transparency, continuity, and memory flow through M1 record
        // owners only. Continued absence is an explicit M7 closeout check.
        var roots = new[]
        {
            Path.Combine(RepositoryRoot, "src/Features/Agents/Application/Transparency"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Application/Memory"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Application/Continuity"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Infrastructure/Transparency"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Domain/Transparency"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Domain/Continuity"),
        };

        var forbidden = new Regex(
            @"\bIConversationStore\b|\bConversationPersistenceService\b|\bAppendEntry\b",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        var violations = roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(entry => forbidden.IsMatch(entry.text))
            .Select(entry => Path.GetRelativePath(RepositoryRoot, entry.path).Replace('\\', '/'))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Phase21Adversarial_Phase21Sources_ContinueToExcludeRootInfrastructure()
    {
        // Phase 21 production code must live under Feature/Agents, not in a
        // root Infrastructure, UI/Shared, plugin, or public API assembly.
        var forbidden = new[]
        {
            Path.Combine(RepositoryRoot, "src/Infrastructure"),
            Path.Combine(RepositoryRoot, "src/UI/Shared"),
            Path.Combine(RepositoryRoot, "src/Plugins"),
        };

        var paths = forbidden
            .Where(Directory.Exists)
            .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Empty(paths);
    }

    [Fact]
    public void Phase21Adversarial_Phase21Sources_ContinueToExcludeEmbeddingsAndNetwork()
    {
        // M7 must not introduce embeddings, vector search, or new network
        // surfaces into Phase 21-specific feature areas. The check is
        // scoped to Transparency/Continuity/Memory surfaces so legitimate
        // backends (NativeHarnessProviderClient, Acp) keep their transport.
        var roots = new[]
        {
            Path.Combine(RepositoryRoot, "src/Features/Agents/Application/Transparency"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Application/Memory"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Application/Continuity"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Infrastructure/Transparency"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Domain/Transparency"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Domain/Continuity"),
        };

        var forbidden = new Regex(
            @"\bEmbeddingsClient\b|\bIVectorStore\b|\bOpenAIClient\b",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        var violations = roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(entry => forbidden.IsMatch(entry.text))
            .Select(entry => Path.GetRelativePath(RepositoryRoot, entry.path).Replace('\\', '/'))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Phase21Adversarial_Phase21TestFiles_PreserveExistingCoverage()
    {
        // All M1–M6 regression test files must remain present and
        // non-empty at M7 publish. If a test file is removed, weakened,
        // or had its cases stripped, the existence or count check fails.
        var requiredFiles = new[]
        {
            "tests/Zaide.Tests/Features/Agents/Transparency/Trace/Phase21RedactionTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Trace/Phase21TraceLifecycleTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Trace/Phase21TraceBackendAdapterTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Usage/Phase21UsageLifecycleTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Usage/Phase21CostEvidenceTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Usage/Phase21UsageCalculationTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Usage/Phase21UsageBackendAdapterTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Storage/Phase21StorageTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Storage/Phase21MigrationTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Storage/Phase21RecordContractTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Storage/Phase21WorkspaceIsolationTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Integration/Phase21ExportTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Integration/Phase21BackupTests.cs",
            "tests/Zaide.Tests/Features/Agents/Transparency/Integration/Phase21TransparencyIntegrationTests.cs",
            "tests/Zaide.Tests/Features/Agents/Continuity/Phase21RecoveryTests.cs",
            "tests/Zaide.Tests/Features/Agents/Continuity/Phase21RestartTests.cs",
            "tests/Zaide.Tests/Features/Agents/Continuity/Phase21TerminationTests.cs",
            "tests/Zaide.Tests/Features/Agents/Memory/Store/Phase21MemoryStoreTests.cs",
            "tests/Zaide.Tests/Features/Agents/Memory/Store/Phase21MemoryPolicyTests.cs",
            "tests/Zaide.Tests/Features/Agents/Memory/Store/Phase21MemoryLifecycleTests.cs",
            "tests/Zaide.Tests/Features/Agents/Memory/Retrieval/Phase21MemoryRetrievalTests.cs",
            "tests/Zaide.Tests/Features/Agents/Memory/Retrieval/Phase21MemoryInfluenceTests.cs",
            "tests/Zaide.Tests/Features/Townhall/Presentation/Phase21TownhallAccessibilityTests.cs",
            "tests/Zaide.Tests/Architecture/Phase21TraceRatchetTests.cs",
            "tests/Zaide.Tests/Architecture/Phase21UsageRatchetTests.cs",
            "tests/Zaide.Tests/Architecture/Phase21MemoryRatchetTests.cs",
            "tests/Zaide.Tests/Architecture/Phase21RecoveryRatchetTests.cs",
            "tests/Zaide.Tests/Architecture/Phase21StorageOwnershipRatchetTests.cs",
        };

        foreach (var relativePath in requiredFiles)
        {
            var fullPath = Path.Combine(RepositoryRoot, relativePath);
            Assert.True(File.Exists(fullPath), $"Missing required test file: {relativePath}");
            var info = new FileInfo(fullPath);
            Assert.True(info.Length > 0, $"Empty required test file: {relativePath}");
        }
    }

    [Fact]
    public void Phase21Adversarial_RequiredM7EvidenceArtifact_IsPublished()
    {
        var evidencePath = Path.Combine(
            RepositoryRoot,
            "docs/phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md");

        Assert.True(File.Exists(evidencePath), "M7 closeout evidence artifact must be published alongside M7 commit");
    }

    private static object[] Row(string threatId, string typeName, string methodName) =>
        new object[] { threatId, typeName, methodName };
}
