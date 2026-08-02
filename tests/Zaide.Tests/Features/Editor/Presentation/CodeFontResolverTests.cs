using System;
using Xunit;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

public class CodeFontResolverTests
{
    [Fact]
    public void EnumerateStack_SplitsCommaSeparatedFamilies()
    {
        var stack = CodeFontResolver.EnumerateStack("Cascadia Code, Consolas, monospace");

        Assert.Equal(new[] { "Cascadia Code", "Consolas", "monospace" }, stack);
    }

    [Fact]
    public void EnumerateStack_TrimsAndSkipsEmptySegments()
    {
        var stack = CodeFontResolver.EnumerateStack("  A , , B  ,");

        Assert.Equal(new[] { "A", "B" }, stack);
    }

    [Fact]
    public void EnumerateStack_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(CodeFontResolver.EnumerateStack(null));
        Assert.Empty(CodeFontResolver.EnumerateStack(""));
        Assert.Empty(CodeFontResolver.EnumerateStack("   "));
    }

    [Fact]
    public void Resolve_PicksFirstFixedPitchFamilyInStack()
    {
        var resolved = CodeFontResolver.Resolve(
            "B&H LucidaBright, Adwaita Mono, monospace",
            family => family is "Adwaita Mono" or "monospace");

        Assert.Equal("Adwaita Mono", resolved.Name);
    }

    [Fact]
    public void Resolve_FallsBackToMonospace_WhenNoFixedPitchFamily()
    {
        var resolved = CodeFontResolver.Resolve(
            "B&H LucidaBright, Noto Sans",
            _ => false);

        Assert.Equal(CodeFontResolver.MonospaceFallback, resolved.Name);
    }

    [Fact]
    public void Resolve_EmptyStack_FallsBackToMonospace()
    {
        var resolved = CodeFontResolver.Resolve("   ", _ => true);

        Assert.Equal(CodeFontResolver.MonospaceFallback, resolved.Name);
    }

    [Fact]
    public void Resolve_AcceptsGenericMonospaceWithoutProbe()
    {
        // Production IsFixedPitchFamily always accepts "monospace"; simulate that.
        var resolved = CodeFontResolver.Resolve(
            "monospace",
            family => family.Equals("monospace", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("monospace", resolved.Name);
    }
}
