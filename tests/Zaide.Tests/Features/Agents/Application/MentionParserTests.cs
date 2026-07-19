using System.Collections.Generic;
using Xunit;
using Zaide.Features.Agents.Application;

namespace Zaide.Tests.Features.Agents.Application;

public class MentionParserTests
{
    private static readonly IReadOnlyList<string> DefaultVisibleNames =
        new[] { "Alpha", "Beta", "Gamma", "Delta" };

    [Fact]
    public void Parse_NoMention_ReturnsDirectSend()
    {
        var parser = new MentionParser();
        var result = parser.Parse("p1", "hello world", DefaultVisibleNames);
        Assert.True(result.Success);
        Assert.NotNull(result.Intent);
        Assert.True(result.Intent!.IsDirectSend);
        Assert.Null(result.Intent.MatchedAgentName);
        Assert.Equal("hello world", result.Intent.ContentAfterStrip);
    }

    [Fact]
    public void Parse_RecognizedMention_ResolvesTargetCorrectly()
    {
        var parser = new MentionParser();
        var result = parser.Parse("p1", "@Beta please review", DefaultVisibleNames);
        Assert.True(result.Success);
        Assert.NotNull(result.Intent);
        Assert.False(result.Intent!.IsDirectSend);
        Assert.Equal("Beta", result.Intent.MatchedAgentName);
        Assert.Equal("please review", result.Intent.ContentAfterStrip);
    }

    [Fact]
    public void Parse_MentionMatch_IsCaseInsensitiveExact()
    {
        var parser = new MentionParser();
        var result = parser.Parse("p1", "@gAmMa check this", DefaultVisibleNames);
        Assert.True(result.Success);
        Assert.Equal("Gamma", result.Intent!.MatchedAgentName);
        Assert.Equal("check this", result.Intent.ContentAfterStrip);
    }

    [Fact]
    public void Parse_UnknownMention_ReturnsExplicitFailure()
    {
        var parser = new MentionParser();
        var result = parser.Parse("p1", "@Epsilon unknown", DefaultVisibleNames);
        Assert.False(result.Success);
        Assert.Equal("Unknown target", result.FailureReason);
    }

    [Fact]
    public void Parse_DuplicateAmbiguousNames_ReturnsExplicitFailure()
    {
        var parser = new MentionParser();
        IReadOnlyList<string> ambiguousNames = new[] { "Alpha", "Alpha" };
        var result = parser.Parse("p1", "@Alpha ambiguous", ambiguousNames);
        Assert.False(result.Success);
        Assert.Equal("Ambiguous target", result.FailureReason);
    }

    [Fact]
    public void Parse_MultipleMentions_ReturnsExplicitFailure()
    {
        var parser = new MentionParser();
        var result = parser.Parse("p1", "@Alpha @Beta both", DefaultVisibleNames);
        Assert.False(result.Success);
        Assert.Equal("Multiple mentions", result.FailureReason);
    }

    [Fact]
    public void Parse_MentionStripping_LeavesCorrectContent()
    {
        var parser = new MentionParser();
        var result = parser.Parse("p1", "task for @Delta please", DefaultVisibleNames);
        Assert.True(result.Success);
        Assert.Equal("task for please", result.Intent!.ContentAfterStrip);
    }

    [Fact]
    public void Parse_EmptyContentAfterStripping_ReturnsExplicitFailure()
    {
        var parser = new MentionParser();
        var result = parser.Parse("p1", "@Alpha", DefaultVisibleNames);
        Assert.False(result.Success);
        Assert.Equal("Empty content after stripping", result.FailureReason);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsExplicitFailure()
    {
        var parser = new MentionParser();
        var result = parser.Parse("p1", "   ", DefaultVisibleNames);
        Assert.False(result.Success);
        Assert.Equal("Empty input", result.FailureReason);
    }

    [Fact]
    public void Parse_UsesCallerSuppliedVisibleNamesOnly()
    {
        var parser = new MentionParser();
        IReadOnlyList<string> names = new[] { "OnlyOne" };
        var known = parser.Parse("p1", "@OnlyOne go", names);
        Assert.True(known.Success);
        Assert.Equal("OnlyOne", known.Intent!.MatchedAgentName);

        var unknown = parser.Parse("p1", "@Beta go", names);
        Assert.False(unknown.Success);
        Assert.Equal("Unknown target", unknown.FailureReason);
    }
}
