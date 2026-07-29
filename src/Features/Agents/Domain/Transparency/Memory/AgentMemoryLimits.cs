namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal static class AgentMemoryLimits
{
    public const int PayloadSchemaVersion = 1;

    public const int MaxContentLength = 16 * 1024;

    public const int DefaultStaleValidationDays = 90;

    public const int MaxRecordsPerPage = 256;
}
