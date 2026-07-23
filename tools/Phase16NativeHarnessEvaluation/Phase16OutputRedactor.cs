using System.Text.RegularExpressions;

namespace Phase16NativeHarnessEvaluation;

public static class Phase16OutputRedactor
{
    private static readonly Regex CredentialPattern = new(
        @"(?i)(api[_-]?key|secret|token|password|authorization)\s*[:=]\s*\S+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BearerPattern = new(
        @"(?i)bearer\s+[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string RedactOrThrow(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var sensitiveValues = CollectSensitiveValues(text);
        var redacted = BearerPattern.Replace(text, "Bearer [REDACTED]");
        redacted = CredentialPattern.Replace(redacted, match =>
        {
            var separatorIndex = match.Value.IndexOf('=');
            if (separatorIndex < 0)
            {
                separatorIndex = match.Value.IndexOf(':');
            }

            if (separatorIndex < 0)
            {
                return "[REDACTED]";
            }

            return match.Value[..(separatorIndex + 1)] + " [REDACTED]";
        });

        foreach (var sensitiveValue in sensitiveValues)
        {
            if (redacted.Contains(sensitiveValue, StringComparison.Ordinal))
            {
                throw new ManifestValidationException("Redaction did not remove credential-bearing output.");
            }
        }

        return redacted;
    }

    private static List<string> CollectSensitiveValues(string text)
    {
        var values = new List<string>();
        foreach (Match match in BearerPattern.Matches(text))
        {
            var token = match.Value["Bearer ".Length..].Trim();
            if (token.Length > 0)
            {
                values.Add(token);
            }
        }

        foreach (Match match in CredentialPattern.Matches(text))
        {
            var separatorIndex = match.Value.IndexOf('=');
            if (separatorIndex < 0)
            {
                separatorIndex = match.Value.IndexOf(':');
            }

            if (separatorIndex >= 0 && separatorIndex + 1 < match.Value.Length)
            {
                var value = match.Value[(separatorIndex + 1)..].Trim();
                if (value.Length > 0)
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }
}
