using System;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class ExecutionRunCorrelationTests
{
    [Fact]
    public void ToEntryCorrelation_MapsExecutionRunIdValue()
    {
        var runId = ExecutionRunId.New();

        var correlation = ExecutionRunCorrelation.ToEntryCorrelation(runId);

        Assert.Equal(runId.Value, correlation.Value);
    }

    [Fact]
    public void ToEntryCorrelation_RejectsDefaultRunId()
    {
        Assert.Throws<ArgumentException>(() =>
            ExecutionRunCorrelation.ToEntryCorrelation(default));
    }
}
