using System;
using System.Text.RegularExpressions;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Redacts credential-like substrings from bounded stderr evidence.
/// </summary>
internal static class AcpStderrRedactor
{
    private static readonly Regex BearerTokenPattern = new(
        @"Bearer\s+\S+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex KeyValueSecretPattern = new(
        @"(?i)(api[_-]?key|token|secret|password)\s*[:=]\s*\S+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Redact(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        var redacted = BearerTokenPattern.Replace(line, "Bearer [redacted]");
        redacted = KeyValueSecretPattern.Replace(redacted, "$1=[redacted]");
        return redacted;
    }
}
