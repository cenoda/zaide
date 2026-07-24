using System;
using System.Text;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Locked Phase 17 operational budgets from P17-D10.
/// </summary>
internal static class AgentActionBudgets
{
    public const int RegularFileReadMaxBytes = 1 * 1024 * 1024;

    public const int ProposedFileTextMaxBytes = 1 * 1024 * 1024;

    public const int PermissionPreviewSummaryMaxBytes = 64 * 1024;

    public const int PermissionPreviewSummaryMaxLines = 2_000;

    public const int CommandStdoutMaxBytes = 1 * 1024 * 1024;

    public const int CommandStdoutMaxLines = 10_000;

    public const int CommandStderrMaxBytes = 1 * 1024 * 1024;

    public const int CommandStderrMaxLines = 10_000;

    public static readonly TimeSpan CommandExecutionTimeout = TimeSpan.FromSeconds(120);

    public static readonly TimeSpan ProcessTreeCleanupTimeout = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan PermissionDecisionLifetime = TimeSpan.FromMinutes(5);

    public const int NonTerminalActionsPerRun = 1;

    public const int StoredAuditSummaryMaxBytes = 8 * 1024;

    public const int BackendCorrelationKeyMaxBytes = 128;

    public static void ValidatePositiveFinite(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero || value == TimeSpan.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Budget duration must be positive and finite.");
        }
    }

    public static int GetUtf8ByteCount(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Encoding.UTF8.GetByteCount(text);
    }
}
