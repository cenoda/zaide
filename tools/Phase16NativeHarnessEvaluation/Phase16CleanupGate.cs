namespace Phase16NativeHarnessEvaluation;

public static class Phase16CleanupGate
{
    private static int _blocked;
    private static string? _blockReason;

    public static bool IsBlocked => Volatile.Read(ref _blocked) != 0;

    public static string? BlockReason => _blockReason;

    public static void RecordCleanupFailure(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Cleanup failure reason is required.", nameof(reason));
        }

        _blockReason = reason;
        Volatile.Write(ref _blocked, 1);
    }

    public static void EnsureNotBlockedOrThrow()
    {
        if (IsBlocked)
        {
            throw new Phase16CleanupBlockedException(
                $"Trial blocked because a prior cleanup failure was recorded: {_blockReason}");
        }
    }

    public static void ResetForTesting()
    {
        _blockReason = null;
        Volatile.Write(ref _blocked, 0);
    }
}
