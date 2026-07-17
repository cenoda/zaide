using Zaide.App.Composition;
using Xunit;
using Zaide.Features.Editor.Domain;

namespace Zaide.Tests.Features.Editor.Domain;

public class SupportedFileTypesTests
{
    [Theory]
    [InlineData(".cs", true)]
    [InlineData(".json", true)]
    [InlineData(".md", true)]
    [InlineData(".txt", true)]
    [InlineData(".xml", true)]
    [InlineData(".axaml", true)]
    [InlineData(".csproj", true)]
    [InlineData(".sln", true)]
    [InlineData(".slnx", true)]
    [InlineData(".props", true)]
    [InlineData(".targets", true)]
    [InlineData(".config", true)]
    [InlineData(".gitignore", true)]
    [InlineData(".gitattributes", true)]
    [InlineData(".yml", true)]
    [InlineData(".yaml", true)]
    [InlineData(".css", true)]
    [InlineData(".html", true)]
    [InlineData(".js", true)]
    [InlineData(".ts", true)]
    [InlineData(".fs", true)]
    [InlineData(".vb", true)]
    [InlineData(".xaml", true)]
    [InlineData(".resx", true)]
    [InlineData(".razor", true)]
    [InlineData(".cshtml", true)]
    [InlineData(".svg", true)]
    [InlineData(".exe", false)]
    [InlineData(".dll", false)]
    [InlineData(".png", false)]
    [InlineData(".jpg", false)]
    [InlineData(".pdf", false)]
    [InlineData(".zip", false)]
    public void IsTextFile_ReturnsExpected(string extension, bool expected)
    {
        var path = $"/path/to/file{extension}";
        Assert.Equal(expected, SupportedFileTypes.IsTextFile(path));
    }

    [Fact]
    public void IsTextFile_NoExtension_ReturnsFalse()
    {
        Assert.False(SupportedFileTypes.IsTextFile("/path/to/file"));
    }

    [Fact]
    public void IsTextFile_CaseInsensitive()
    {
        Assert.True(SupportedFileTypes.IsTextFile("/path/to/file.CS"));
        Assert.True(SupportedFileTypes.IsTextFile("/path/to/file.JSON"));
    }

    [Theory]
    [InlineData(".exe", "Unsupported file type: .exe")]
    [InlineData(".dll", "Unsupported file type: .dll")]
    [InlineData(".png", "Unsupported file type: .png")]
    [InlineData("", "Unsupported file type: (no extension)")]
    public void GetUnsupportedMessage_ReturnsMessage(string? extension, string expected)
    {
        var path = extension is null ? "/path/to/file" : $"/path/to/file{extension}";
        Assert.Equal(expected, SupportedFileTypes.GetUnsupportedMessage(path));
    }

    [Fact]
    public void GetUnsupportedMessage_SupportedFile_ReturnsNull()
    {
        Assert.Null(SupportedFileTypes.GetUnsupportedMessage("/path/to/file.cs"));
    }
}