namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// M1 baseline retention metadata per record class. Enforcement belongs to later
/// milestones; M1 records the distinct policy owner per class.
/// </summary>
internal static class AgentDurableRecordRetentionPolicy
{
    public const int DefaultRetentionDaysTrace = 30;
    public const int DefaultRetentionDaysUsage = 365;
    public const int DefaultRetentionDaysSessionRecovery = 90;
    public const int DefaultRetentionDaysAudit = 365;
    public const int DefaultRetentionDaysMemory = 0;

    public static int GetDefaultRetentionDays(AgentDurableRecordClass recordClass) =>
        recordClass switch
        {
            AgentDurableRecordClass.Trace => DefaultRetentionDaysTrace,
            AgentDurableRecordClass.Usage => DefaultRetentionDaysUsage,
            AgentDurableRecordClass.SessionRecovery => DefaultRetentionDaysSessionRecovery,
            AgentDurableRecordClass.Audit => DefaultRetentionDaysAudit,
            AgentDurableRecordClass.Memory => DefaultRetentionDaysMemory,
            _ => throw new System.ArgumentOutOfRangeException(nameof(recordClass)),
        };
}
