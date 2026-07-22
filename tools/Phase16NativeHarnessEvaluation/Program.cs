// Phase 16 M2a: offline fake-candidate evaluation harness.
// No upstream artifacts, network, process launch, or candidate execution.

using Phase16NativeHarnessEvaluation;

if (args.Length == 0)
{
    Console.Error.WriteLine("Phase16NativeHarnessEvaluation — offline fake-candidate harness (M2a).");
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  Phase16NativeHarnessEvaluation validate-manifest <manifest.json>");
    Console.Error.WriteLine("  Phase16NativeHarnessEvaluation fake-run <manifest.json> <ledger.jsonl>");
    return 2;
}

var command = args[0];
try
{
    return command switch
    {
        "validate-manifest" => ValidateManifest(args),
        "fake-run" => FakeRun(args),
        _ => UnknownCommand(command),
    };
}
catch (ManifestValidationException ex)
{
    Console.Error.WriteLine($"MANIFEST_INVALID: {ex.Message}");
    return 3;
}
catch (Phase16RecordValidationException ex)
{
    Console.Error.WriteLine($"RECORD_INVALID: {ex.Message}");
    return 4;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    return 2;
}

static int ValidateManifest(string[] args)
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("Usage: validate-manifest <manifest.json>");
        return 2;
    }

    var manifestJson = File.ReadAllText(args[1]);
    var manifest = Phase16ManifestParser.Parse(manifestJson);
    var runnerConfig = ReadRunnerConfigFromEnvironment();
    Phase16ManifestValidator.ValidateOrThrow(manifest, runnerConfig);
    Console.WriteLine("MANIFEST_OK");
    return 0;
}

static int FakeRun(string[] args)
{
    if (args.Length != 3)
    {
        Console.Error.WriteLine("Usage: fake-run <manifest.json> <ledger.jsonl>");
        return 2;
    }

    var manifestJson = File.ReadAllText(args[1]);
    var manifest = Phase16ManifestParser.Parse(manifestJson);
    var runnerConfig = ReadRunnerConfigFromEnvironment();
    var store = new Phase16RecordStore(Path.GetFullPath(args[2]));
    var runner = new Phase16EvaluationRunner(runnerConfig, store);
    var record = runner.RunFakeTrial(manifest);
    Console.WriteLine(record.RecordId);
    return 0;
}

static Phase16RunnerConfig ReadRunnerConfigFromEnvironment()
{
    var artifactRoot = Environment.GetEnvironmentVariable("PHASE16_ARTIFACT_ROOT");
    if (string.IsNullOrWhiteSpace(artifactRoot))
    {
        artifactRoot = Path.Combine(Path.GetTempPath(), "phase16-artifacts");
    }

    var runnerCommit = Environment.GetEnvironmentVariable("PHASE16_RUNNER_COMMIT");
    if (string.IsNullOrWhiteSpace(runnerCommit))
    {
        runnerCommit = "unknown";
    }

    var campaignLockRevision = Environment.GetEnvironmentVariable("PHASE16_CAMPAIGN_LOCK_REVISION");
    if (string.IsNullOrWhiteSpace(campaignLockRevision))
    {
        campaignLockRevision = "m1-2026-07-23";
    }

    return new Phase16RunnerConfig
    {
        ManifestSchemaVersion = RunnerContractVersion.ManifestSchemaVersion,
        ArtifactRoot = artifactRoot,
        RunnerCommit = runnerCommit,
        CampaignLockRevision = campaignLockRevision,
    };
}
