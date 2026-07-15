using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Services;

public sealed class SourceControlActionDeriverTests
{
    [Theory]
    [InlineData(1, 0, 3, true, SourceControlPrimaryAction.Commit)]
    [InlineData(0, 1, 3, true, SourceControlPrimaryAction.Commit)]
    [InlineData(2, 1, 5, true, SourceControlPrimaryAction.Commit)]
    [InlineData(0, 0, 2, true, SourceControlPrimaryAction.Push)]
    [InlineData(0, 0, 0, true, SourceControlPrimaryAction.Commit)]
    [InlineData(0, 0, 4, false, SourceControlPrimaryAction.Commit)]
    public void Derive_UsesWorkingTreeCleanlinessAndAheadStatus(
        int unstagedCount,
        int stagedCount,
        int aheadBy,
        bool hasUpstream,
        SourceControlPrimaryAction expected)
    {
        var action = SourceControlActionDeriver.Derive(
            unstagedCount,
            stagedCount,
            aheadBy,
            hasUpstream,
            hasRepository: true);

        Assert.Equal(expected, action);
    }

    [Fact]
    public void Derive_NoRepository_AlwaysCommit()
    {
        var action = SourceControlActionDeriver.Derive(0, 0, 5, true, hasRepository: false);

        Assert.Equal(SourceControlPrimaryAction.Commit, action);
    }
}