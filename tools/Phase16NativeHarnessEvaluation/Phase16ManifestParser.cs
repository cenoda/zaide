using System.Text.Json;

namespace Phase16NativeHarnessEvaluation;

public static class Phase16ManifestParser
{
    public static Phase16Manifest Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var schemaVersion = RequireInt(root, "manifestSchemaVersion");
        var runnerConfigHash = RequireNonEmptyString(root, "runnerConfigHash");
        var fixtureHash = RequireNonEmptyString(root, "fixtureHash");
        var taskId = RequireNonEmptyString(root, "taskId");
        var executionMode = ParseExecutionMode(RequireNonEmptyString(root, "executionMode"));
        var candidate = ParseCandidate(RequireObject(root, "candidate"));
        var fakeCandidate = ParseFakeCandidate(RequireObject(root, "fakeCandidate"));
        var networkEnabled = root.TryGetProperty("networkEnabled", out var networkElement) &&
                             networkElement.ValueKind == JsonValueKind.True;
        var processLaunchEnabled = root.TryGetProperty("processLaunchEnabled", out var processElement) &&
                                   processElement.ValueKind == JsonValueKind.True;
        string? upstreamArtifactPath = null;
        if (root.TryGetProperty("upstreamArtifactPath", out var upstreamElement) &&
            upstreamElement.ValueKind == JsonValueKind.String)
        {
            upstreamArtifactPath = upstreamElement.GetString();
        }

        Sha256DigestValidator.ValidateOrThrow(runnerConfigHash, "runnerConfigHash");
        Sha256DigestValidator.ValidateOrThrow(fixtureHash, "fixtureHash");

        return new Phase16Manifest
        {
            ManifestSchemaVersion = schemaVersion,
            RunnerConfigHash = runnerConfigHash,
            FixtureHash = fixtureHash,
            TaskId = taskId,
            ExecutionMode = executionMode,
            Candidate = candidate,
            FakeCandidate = fakeCandidate,
            NetworkEnabled = networkEnabled,
            ProcessLaunchEnabled = processLaunchEnabled,
            UpstreamArtifactPath = upstreamArtifactPath,
        };
    }

    private static CandidateIdentity ParseCandidate(JsonElement element)
    {
        return new CandidateIdentity
        {
            CandidateSlug = RequireNonEmptyString(element, "candidateSlug"),
            PublicSourceUrl = RequireNonEmptyString(element, "publicSourceUrl"),
            PublicSourceHead = RequireNonEmptyString(element, "publicSourceHead"),
            ReleaseTag = RequireNonEmptyString(element, "releaseTag"),
            TagCommit = RequireNonEmptyString(element, "tagCommit"),
            ReleaseMetadataTarget = RequireNonEmptyString(element, "releaseMetadataTarget"),
            SourceRev = RequireNonEmptyString(element, "sourceRev"),
            DistributedArtifactHash = RequireNonEmptyString(element, "distributedArtifactHash"),
            ChangelogProductIdentity = RequireNonEmptyString(element, "changelogProductIdentity"),
            ProviderIdentity = RequireNonEmptyString(element, "providerIdentity"),
            ServiceIdentity = RequireNonEmptyString(element, "serviceIdentity"),
            ModelIdentity = RequireNonEmptyString(element, "modelIdentity"),
            ProtocolSdkIdentity = RequireNonEmptyString(element, "protocolSdkIdentity"),
        };
    }

    private static FakeCandidateIdentity ParseFakeCandidate(JsonElement element)
    {
        return new FakeCandidateIdentity
        {
            FakeCandidateId = RequireNonEmptyString(element, "fakeCandidateId"),
            FakeCandidateVersion = RequireNonEmptyString(element, "fakeCandidateVersion"),
            FakeCandidateKind = RequireNonEmptyString(element, "fakeCandidateKind"),
        };
    }

    private static CandidateExecutionMode ParseExecutionMode(string value)
    {
        return value switch
        {
            "fake_repository_owned" => CandidateExecutionMode.FakeRepositoryOwned,
            "upstream_candidate" => CandidateExecutionMode.UpstreamCandidate,
            _ => throw new ManifestValidationException($"Unknown executionMode '{value}'."),
        };
    }

    private static JsonElement RequireObject(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.Object)
        {
            throw new ManifestValidationException($"Missing or invalid object field '{propertyName}'.");
        }

        return element;
    }

    private static int RequireInt(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.Number ||
            !element.TryGetInt32(out var value))
        {
            throw new ManifestValidationException($"Missing or invalid integer field '{propertyName}'.");
        }

        return value;
    }

    private static string RequireNonEmptyString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            throw new ManifestValidationException($"Missing or invalid string field '{propertyName}'.");
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ManifestValidationException($"Required field '{propertyName}' is empty.");
        }

        return value;
    }
}
