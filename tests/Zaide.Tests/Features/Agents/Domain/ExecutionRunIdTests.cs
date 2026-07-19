using System;
using System.Collections.Generic;
using Xunit;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class ExecutionRunIdTests
{
    [Fact]
    public void New_CreatesNonDefaultValue()
    {
        var id = ExecutionRunId.New();

        Assert.NotEqual(default(ExecutionRunId), id);
        Assert.False(string.IsNullOrWhiteSpace(id.Value));
        Assert.StartsWith("run:", id.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void New_GeneratesUniqueValues()
    {
        var ids = new HashSet<string>();
        for (var i = 0; i < 32; i++)
        {
            Assert.True(ids.Add(ExecutionRunId.New().Value));
        }
    }

    [Fact]
    public void Default_IsSafeAndNotEqualToCreatedIds()
    {
        var defaultId = default(ExecutionRunId);
        var created = ExecutionRunId.New();

        Assert.Equal(string.Empty, defaultId.Value);
        Assert.NotEqual(defaultId, created);
        Assert.False(defaultId.Equals(created));
    }

    [Fact]
    public void Equality_DistinguishesDifferentCreatedIds()
    {
        var left = ExecutionRunId.New();
        var other = ExecutionRunId.New();

        Assert.NotEqual(left, other);
        Assert.NotEqual(left.GetHashCode(), other.GetHashCode());
    }
}
