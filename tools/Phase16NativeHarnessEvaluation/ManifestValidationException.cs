namespace Phase16NativeHarnessEvaluation;

public sealed class ManifestValidationException : Exception
{
    public ManifestValidationException(string message)
        : base(message)
    {
    }

    public ManifestValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
