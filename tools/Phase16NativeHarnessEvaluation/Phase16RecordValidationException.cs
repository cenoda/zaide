namespace Phase16NativeHarnessEvaluation;

public sealed class Phase16RecordValidationException : Exception
{
    public Phase16RecordValidationException(string message)
        : base(message)
    {
    }
}
