using System;
using System.Collections.Generic;
using System.IO;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Locked Phase 17 command executable denylist applied to canonical absolute paths.
/// </summary>
internal static class AgentCommandDenylist
{
    private static readonly HashSet<string> ShellInterpreters = new(StringComparer.OrdinalIgnoreCase)
    {
        "sh",
        "bash",
        "dash",
        "zsh",
        "fish",
        "csh",
        "tcsh",
        "ksh",
    };

    private static readonly HashSet<string> PrivilegeEscalationHelpers = new(StringComparer.OrdinalIgnoreCase)
    {
        "sudo",
        "doas",
        "su",
        "pkexec",
    };

    public static AgentCommandDenylistResult Classify(string canonicalAbsoluteExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(canonicalAbsoluteExecutablePath))
        {
            throw new ArgumentException(
                "Canonical absolute executable path is required.",
                nameof(canonicalAbsoluteExecutablePath));
        }

        var basename = Path.GetFileName(canonicalAbsoluteExecutablePath);
        if (ShellInterpreters.Contains(basename))
        {
            return AgentCommandDenylistResult.Denied(
                AgentCommandDenylistClassification.DeniedShellInterpreter,
                canonicalAbsoluteExecutablePath);
        }

        if (PrivilegeEscalationHelpers.Contains(basename))
        {
            return AgentCommandDenylistResult.Denied(
                AgentCommandDenylistClassification.DeniedPrivilegeEscalation,
                canonicalAbsoluteExecutablePath);
        }

        return AgentCommandDenylistResult.Allowed(canonicalAbsoluteExecutablePath);
    }
}
