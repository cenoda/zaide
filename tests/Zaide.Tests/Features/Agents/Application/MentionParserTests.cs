using System.Linq;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Presentation;

namespace Zaide.Tests.Features.Agents.Application;

public class MentionParserTests
{
    private static AgentPanelHost CreateHostWithPanels()
    {
        var host = new AgentPanelHost();
        host.CreatePanel(); // alpha/Alpha
        host.CreatePanel(); // beta/Beta
        host.CreatePanel(); // gamma/Gamma
        host.CreatePanel(); // delta/Delta
        return host;
    }

    [Fact]
    public void Parse_NoMention_ReturnsDirectSend()
    {
        var host = CreateHostWithPanels();
        var parser = new MentionParser(host);
        var result = parser.Parse("p1", "hello world");
        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.True(result.Request!.IsDirectSend);
        Assert.Null(result.Request.TargetAgentName);
        Assert.Equal("hello world", result.Request.ContentAfterStrip);
    }

    [Fact]
    public void Parse_RecognizedMention_ResolvesTargetCorrectly()
    {
        var host = CreateHostWithPanels();
        var parser = new MentionParser(host);
        var result = parser.Parse("p1", "@Beta please review");
        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.False(result.Request!.IsDirectSend);
        Assert.Equal("Beta", result.Request.TargetAgentName);
        Assert.Equal("please review", result.Request.ContentAfterStrip);
    }

    [Fact]
    public void Parse_MentionMatch_IsCaseInsensitiveExact()
    {
        var host = CreateHostWithPanels();
        var parser = new MentionParser(host);
        var result = parser.Parse("p1", "@gAmMa check this");
        Assert.True(result.Success);
        Assert.Equal("Gamma", result.Request!.TargetAgentName);
        Assert.Equal("check this", result.Request.ContentAfterStrip);
    }

    [Fact]
    public void Parse_UnknownMention_ReturnsExplicitFailure()
    {
        var host = CreateHostWithPanels();
        var parser = new MentionParser(host);
        var result = parser.Parse("p1", "@Epsilon unknown");
        Assert.False(result.Success);
        Assert.Equal("Unknown target", result.FailureReason);
    }

    [Fact]
    public void Parse_DuplicateAmbiguousNames_ReturnsExplicitFailure()
    {
        var host = new AgentPanelHost();
        host.CreatePanel("a1", "Alpha", "Icon.Avatar");
        host.CreatePanel("a2", "Alpha", "Icon.Avatar"); // duplicate name
        var parser = new MentionParser(host);
        var result = parser.Parse("p1", "@Alpha ambiguous");
        Assert.False(result.Success);
        Assert.Equal("Ambiguous target", result.FailureReason);
    }

    [Fact]
    public void Parse_MultipleMentions_ReturnsExplicitFailure()
    {
        var host = CreateHostWithPanels();
        var parser = new MentionParser(host);
        var result = parser.Parse("p1", "@Alpha @Beta both");
        Assert.False(result.Success);
        Assert.Equal("Multiple mentions", result.FailureReason);
    }

    [Fact]
    public void Parse_MentionStripping_LeavesCorrectContent()
    {
        var host = CreateHostWithPanels();
        var parser = new MentionParser(host);
        var result = parser.Parse("p1", "task for @Delta please");
        Assert.True(result.Success);
        Assert.Equal("task for please", result.Request!.ContentAfterStrip);
    }

    [Fact]
    public void Parse_EmptyContentAfterStripping_ReturnsExplicitFailure()
    {
        var host = CreateHostWithPanels();
        var parser = new MentionParser(host);
        var result = parser.Parse("p1", "@Alpha");
        Assert.False(result.Success);
        Assert.Equal("Empty content after stripping", result.FailureReason);
    }
}
