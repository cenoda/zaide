using AvaloniaEdit.Document;
using Xunit;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

public class IndentGuideLevelCacheTests
{
    [Fact]
    public void GetGuideLevelCount_ReturnsLevelsForIndentedLines()
    {
        var doc = new TextDocument(
            "class C\n" +
            "{\n" +
            "    void M()\n" +
            "    {\n" +
            "        var x = 1;\n" +
            "    }\n" +
            "}\n");
        var cache = new IndentGuideLevelCache();

        Assert.Equal(0, cache.GetGuideLevelCount(doc, lineNumber: 1, indentationSize: 4));
        Assert.Equal(0, cache.GetGuideLevelCount(doc, lineNumber: 2, indentationSize: 4));
        Assert.Equal(1, cache.GetGuideLevelCount(doc, lineNumber: 3, indentationSize: 4));
        Assert.Equal(1, cache.GetGuideLevelCount(doc, lineNumber: 4, indentationSize: 4));
        Assert.Equal(2, cache.GetGuideLevelCount(doc, lineNumber: 5, indentationSize: 4));
        Assert.Equal(1, cache.GetGuideLevelCount(doc, lineNumber: 6, indentationSize: 4));
    }

    [Fact]
    public void GetGuideLevelCount_WhitespaceOnlyLines_ReturnZero()
    {
        var doc = new TextDocument("    \n\t\ncode\n");
        var cache = new IndentGuideLevelCache();

        Assert.Equal(0, cache.GetGuideLevelCount(doc, 1, 4));
        Assert.Equal(0, cache.GetGuideLevelCount(doc, 2, 4));
        Assert.Equal(0, cache.GetGuideLevelCount(doc, 3, 4));
    }

    [Fact]
    public void GetGuideLevelCount_WarmsCache_AndReusesAcrossLookups()
    {
        // No trailing newline: exactly two document lines.
        var doc = new TextDocument("    a\n        b");
        var cache = new IndentGuideLevelCache();

        Assert.False(cache.IsWarmFor(doc, 4));
        _ = cache.GetGuideLevelCount(doc, 1, 4);
        Assert.True(cache.IsWarmFor(doc, 4));
        Assert.Equal(2, cache.CachedLineCount);

        // Same version: still warm; second line hits array without rebuild.
        Assert.Equal(2, cache.GetGuideLevelCount(doc, 2, 4));
        Assert.True(cache.IsWarmFor(doc, 4));
    }

    [Fact]
    public void GetGuideLevelCount_Invalidates_WhenDocumentTextChanges()
    {
        var doc = new TextDocument("    a\n");
        var cache = new IndentGuideLevelCache();

        Assert.Equal(1, cache.GetGuideLevelCount(doc, 1, 4));
        Assert.True(cache.IsWarmFor(doc, 4));

        doc.Insert(0, "    "); // now 8 spaces before 'a' → 2 levels
        Assert.False(cache.IsWarmFor(doc, 4));

        Assert.Equal(2, cache.GetGuideLevelCount(doc, 1, 4));
        Assert.True(cache.IsWarmFor(doc, 4));
    }

    [Fact]
    public void GetGuideLevelCount_Invalidates_WhenIndentationSizeChanges()
    {
        var doc = new TextDocument("    a\n");
        var cache = new IndentGuideLevelCache();

        Assert.Equal(1, cache.GetGuideLevelCount(doc, 1, indentationSize: 4));
        Assert.True(cache.IsWarmFor(doc, 4));
        Assert.False(cache.IsWarmFor(doc, 2));

        // Same four spaces are two levels when indentation size is 2.
        Assert.Equal(2, cache.GetGuideLevelCount(doc, 1, indentationSize: 2));
        Assert.True(cache.IsWarmFor(doc, 2));
        Assert.False(cache.IsWarmFor(doc, 4));
    }

    [Fact]
    public void GetGuideLevelCount_Tabs_CountAsFullIndentLevels()
    {
        var doc = new TextDocument("\t\tx\n");
        var cache = new IndentGuideLevelCache();

        Assert.Equal(2, cache.GetGuideLevelCount(doc, 1, indentationSize: 4));
    }

    [Fact]
    public void GetGuideLevelCount_OutOfRangeLine_ReturnsZero()
    {
        var doc = new TextDocument("    a\n");
        var cache = new IndentGuideLevelCache();

        Assert.Equal(0, cache.GetGuideLevelCount(doc, 0, 4));
        Assert.Equal(0, cache.GetGuideLevelCount(doc, 99, 4));
    }
}
