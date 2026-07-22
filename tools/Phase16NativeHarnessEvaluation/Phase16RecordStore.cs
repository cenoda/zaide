using System.Text.Json;

namespace Phase16NativeHarnessEvaluation;

public sealed class Phase16RecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string _ledgerPath;
    private readonly Dictionary<string, string> _recordIdToLine = new(StringComparer.Ordinal);

    public Phase16RecordStore(string ledgerPath)
    {
        _ledgerPath = ledgerPath;
        LoadExistingRecords();
    }

    public IReadOnlyCollection<string> RecordIds => _recordIdToLine.Keys;

    public int Count => _recordIdToLine.Count;

    public void Append(Phase16Record record, Phase16Manifest manifest)
    {
        Phase16RecordValidator.ValidateStructureOrThrow(record);
        Phase16RecordValidator.ValidateManifestBindingOrThrow(record, manifest);

        if (_recordIdToLine.ContainsKey(record.RecordId))
        {
            throw new Phase16RecordValidationException(
                $"Duplicate recordId '{record.RecordId}' is forbidden in append-only ledger.");
        }

        var expectedContentHash = ComputeRecordContentHash(record);
        if (!string.Equals(record.RecordContentHash, expectedContentHash, StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException("recordContentHash does not match canonical record body.");
        }

        var line = JsonSerializer.Serialize(record, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(_ledgerPath)!);
        File.AppendAllText(_ledgerPath, line + Environment.NewLine);
        _recordIdToLine[record.RecordId] = line;
    }

    public Phase16Record? TryGetRecord(string recordId)
    {
        if (!_recordIdToLine.TryGetValue(recordId, out var line))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Phase16Record>(line, JsonOptions);
    }

    public void RejectOverwrite(string recordId, string newLine)
    {
        if (_recordIdToLine.TryGetValue(recordId, out var existingLine) &&
            !string.Equals(existingLine, newLine, StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException(
                $"Overwrite of recordId '{recordId}' is forbidden.");
        }
    }

    public static string ComputeRecordContentHash(Phase16Record record)
    {
        var canonical = ManifestCanonicalSerializer.SerializeForHash(new
        {
            recordId = record.RecordId,
            manifestSchemaVersion = record.ManifestSchemaVersion,
            runnerConfigHash = record.RunnerConfigHash,
            fixtureHash = record.FixtureHash,
            taskId = record.TaskId,
            executionMode = record.ExecutionMode,
            candidate = record.Candidate,
            fakeCandidate = record.FakeCandidate,
            metrics = record.Metrics,
            stdout = record.Stdout,
            stderr = record.Stderr,
            stdoutTruncated = record.StdoutTruncated,
            stderrTruncated = record.StderrTruncated,
            evidenceClass = record.EvidenceClass,
            invalidationReasons = record.InvalidationReasons,
        });

        return ManifestCanonicalSerializer.ComputeSha256Hex(canonical);
    }

    private void LoadExistingRecords()
    {
        if (!File.Exists(_ledgerPath))
        {
            return;
        }

        foreach (var line in File.ReadAllLines(_ledgerPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<Phase16Record>(line, JsonOptions)
                         ?? throw new Phase16RecordValidationException("Malformed ledger line.");
            ValidateReloadedRecordOrThrow(record);

            if (_recordIdToLine.ContainsKey(record.RecordId))
            {
                throw new Phase16RecordValidationException(
                    $"Ledger contains duplicate recordId '{record.RecordId}'.");
            }

            _recordIdToLine[record.RecordId] = line;
        }
    }

    private static void ValidateReloadedRecordOrThrow(Phase16Record record)
    {
        Phase16RecordValidator.ValidateStructureOrThrow(record);

        var expectedHash = ComputeRecordContentHash(record);
        if (!string.Equals(record.RecordContentHash, expectedHash, StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException(
                $"Ledger record '{record.RecordId}' has invalid recordContentHash.");
        }
    }
}
