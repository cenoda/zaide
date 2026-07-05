using Xunit;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class LogCategorizerTests
{
    [Fact]
    public void Categorize_ExplicitBuildTag_ReturnsBuild()
    {
        var (cat, warn) = LogCategorizer.Categorize("[BUILD] Build succeeded");
        Assert.Equal(LogCategory.Build, cat);
        Assert.False(warn);
    }

    [Fact]
    public void Categorize_ExplicitAgentTag_ReturnsAgent()
    {
        var (cat, warn) = LogCategorizer.Categorize("[AGENT] Processing request");
        Assert.Equal(LogCategory.Agent, cat);
        Assert.False(warn);
    }

    [Fact]
    public void Categorize_ExplicitLogTag_ReturnsLog()
    {
        var (cat, warn) = LogCategorizer.Categorize("[LOG] Runtime info");
        Assert.Equal(LogCategory.Log, cat);
        Assert.False(warn);
    }

    [Fact]
    public void Categorize_BuildToolOutput_ReturnsBuild()
    {
        var (cat, warn) = LogCategorizer.Categorize("Build started...");
        Assert.Equal(LogCategory.Build, cat);
    }

    [Fact]
    public void Categorize_DotnetCommand_ReturnsBuild()
    {
        var (cat, _) = LogCategorizer.Categorize("  dotnet build Zaide.slnx");
        Assert.Equal(LogCategory.Build, cat);
    }

    [Fact]
    public void Categorize_CsError_ReturnsBuild()
    {
        var (cat, warn) = LogCategorizer.Categorize("error CS1001: Identifier expected");
        Assert.Equal(LogCategory.Build, cat);
        Assert.True(warn);
    }

    [Fact]
    public void Categorize_CsWarning_ReturnsBuildWithWarning()
    {
        var (cat, warn) = LogCategorizer.Categorize("warning CS0219: Unused variable");
        Assert.Equal(LogCategory.Build, cat);
        Assert.True(warn);
    }

    [Fact]
    public void Categorize_AgentKeyword_ReturnsAgent()
    {
        var (cat, _) = LogCategorizer.Categorize("Agent is processing file");
        Assert.Equal(LogCategory.Agent, cat);
    }

    [Fact]
    public void Categorize_TownhallKeyword_ReturnsAgent()
    {
        var (cat, _) = LogCategorizer.Categorize("townhall message received");
        Assert.Equal(LogCategory.Agent, cat);
    }

    [Fact]
    public void Categorize_ModelKeyword_ReturnsAgent()
    {
        var (cat, _) = LogCategorizer.Categorize("model: gpt-4 response");
        Assert.Equal(LogCategory.Agent, cat);
    }

    [Fact]
    public void Categorize_DefaultLine_ReturnsLog()
    {
        var (cat, warn) = LogCategorizer.Categorize("some random output line");
        Assert.Equal(LogCategory.Log, cat);
        Assert.False(warn);
    }

    [Fact]
    public void Categorize_EmptyLine_ReturnsLogNoWarning()
    {
        var (cat, warn) = LogCategorizer.Categorize(string.Empty);
        Assert.Equal(LogCategory.Log, cat);
        Assert.False(warn);
    }

    [Fact]
    public void Categorize_ExceptionLine_ReturnsLogWithWarning()
    {
        var (cat, warn) = LogCategorizer.Categorize("System.Exception: Something broke");
        Assert.Equal(LogCategory.Log, cat);
        Assert.True(warn);
    }

    [Fact]
    public void Categorize_FailedLine_ReturnsLogWithWarning()
    {
        var (cat, warn) = LogCategorizer.Categorize("Build failed with 1 error");
        Assert.Equal(LogCategory.Build, cat);
        Assert.True(warn);
    }

    [Fact]
    public void Categorize_TracebackLine_ReturnsLogWithWarning()
    {
        var (cat, warn) = LogCategorizer.Categorize("Traceback (most recent call last):");
        Assert.Equal(LogCategory.Log, cat);
        Assert.True(warn);
    }

    [Fact]
    public void Categorize_AnsiEscape_StripsBeforeMatching()
    {
        // ANSI escape sequences should be stripped before heuristic matching
        var (cat, warn) = LogCategorizer.Categorize("\x1B[31m[BUILD]\x1B[0m output");
        Assert.Equal(LogCategory.Build, cat);
        Assert.False(warn);
    }

    [Fact]
    public void Categorize_AnsiEscapeWarning_DetectsWarning()
    {
        var (cat, warn) = LogCategorizer.Categorize("\x1B[33mwarning\x1B[0m CS0219");
        Assert.Equal(LogCategory.Build, cat);
        Assert.True(warn);
    }
}