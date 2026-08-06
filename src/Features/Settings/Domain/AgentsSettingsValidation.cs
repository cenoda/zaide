using System;
using System.Collections.Generic;

namespace Zaide.Features.Settings.Domain;

/// <summary>
/// Validates agent-related settings fields and rejects secret-shaped ACP arguments.
/// </summary>
internal static class AgentsSettingsValidation
{
    private static readonly string[] SecretPatterns =
    {
        "api-key",
        "apikey",
        "api_key",
        "bearer",
        "token=",
        "secret",
        "password",
        "authorization:",
    };

    public static void Validate(AgentsSettings agents, ICollection<SettingsValidationError> errors)
    {
        if (agents.TracePageSize <= 0)
        {
            errors.Add(new("Agents.TracePageSize", "Trace page size must be positive."));
        }

        if (agents.TraceMaxPageSize <= 0)
        {
            errors.Add(new("Agents.TraceMaxPageSize", "Trace max page size must be positive."));
        }

        if (agents.TracePageSize > agents.TraceMaxPageSize)
        {
            errors.Add(new("Agents.TracePageSize",
                "Trace page size must not exceed trace max page size."));
        }

        if (!TryParseContextPolicyLevel(agents.DefaultContextPolicyLevel, out _))
        {
            errors.Add(new("Agents.DefaultContextPolicyLevel",
                "Default context policy must be Off, Minimal, Standard, or Detailed."));
        }

        if (ContainsSecretPattern(agents.AcpArguments))
        {
            errors.Add(new("Agents.AcpArguments",
                "ACP arguments must not contain secret-shaped values; use ISecretStore for credentials."));
        }
    }

    public static bool TryParseContextPolicyLevel(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        normalized = value.Trim();
        return normalized.Equals("Off", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Minimal", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Standard", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Detailed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSecretPattern(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var pattern in SecretPatterns)
        {
            if (value.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
