using Xunit;
using Zaide.Views;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Tests for the static grammar scope mapping extracted from EditorView.
/// Verifies that switching from a supported extension to an unsupported
/// one correctly returns null (grammar reset).
/// </summary>
public class EditorViewGrammarTests
{
    [Theory]
    [InlineData("Program.cs", "source.cs")]
    [InlineData("data.json", "source.json")]
    [InlineData("readme.md", "text.html.markdown")]
    [InlineData("README.MD", "text.html.markdown")] // case-insensitive
    [InlineData("notes.txt", null)]
    [InlineData("", null)]
    [InlineData("Makefile", null)]       // no extension
    [InlineData("image.png", null)]      // binary type
    public void GetGrammarScope_ReturnsExpectedScope(string filePath, string? expected)
    {
        var scope = EditorView.GetGrammarScope(filePath);
        Assert.Equal(expected, scope);
    }

    [Fact]
    public void GetGrammarScope_ReturnsNull_ForUnsupportedExtension()
    {
        // .txt has no grammar — must return null, not the previous tab's scope
        Assert.Null(EditorView.GetGrammarScope("notes.txt"));
    }

    [Fact]
    public void GetGrammarScope_Reset_SupportedToUnsupported()
    {
        // Simulate switching from .cs (supported) to .txt (unsupported).
        // The scope must become null — the caller clears the grammar
        // by passing "" to SetGrammar.
        var csScope = EditorView.GetGrammarScope("Program.cs");
        Assert.NotNull(csScope);
        Assert.Equal("source.cs", csScope);

        var txtScope = EditorView.GetGrammarScope("notes.txt");
        Assert.Null(txtScope);
    }

    [Fact]
    public void GetGrammarScope_EmptyFile_ReturnsNull()
    {
        Assert.Null(EditorView.GetGrammarScope(""));
    }
}