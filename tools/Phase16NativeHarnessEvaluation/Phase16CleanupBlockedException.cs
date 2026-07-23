namespace Phase16NativeHarnessEvaluation;

public sealed class Phase16CleanupBlockedException : Exception
{
    public Phase16CleanupBlockedException(string message)
        : base(message)
    {
    }
}
