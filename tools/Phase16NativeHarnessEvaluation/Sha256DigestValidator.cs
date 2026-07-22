using System.Text.RegularExpressions;

namespace Phase16NativeHarnessEvaluation;

public static partial class Sha256DigestValidator
{
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex LowercaseHexSha256Pattern();

    public static void ValidateOrThrow(string? value, string fieldName)
    {
        if (value is null)
        {
            throw new ManifestValidationException($"Required digest field '{fieldName}' is missing.");
        }

        if (value.Length != 64)
        {
            throw new ManifestValidationException(
                $"Digest field '{fieldName}' must be exactly 64 lowercase hexadecimal characters.");
        }

        if (!LowercaseHexSha256Pattern().IsMatch(value))
        {
            throw new ManifestValidationException(
                $"Digest field '{fieldName}' must be exactly 64 lowercase hexadecimal characters.");
        }
    }
}
