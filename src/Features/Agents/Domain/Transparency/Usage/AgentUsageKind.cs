namespace Zaide.Features.Agents.Domain.Transparency.Usage;

internal enum AgentUsageKind
{
    TokensInput = 0,
    TokensOutput = 1,
    TotalTokens = 2,
    EstimatedCost = 3,
    InvoicedCost = 4,
    TotalCost = 5,
    RequestCount = 6,
    LatencyMs = 7,
    Other = 8,
}
