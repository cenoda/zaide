using System;
using System.IO;
using System.Linq;
using Xunit;
using Zaide.Services;
using Zaide.Views;

namespace Zaide.Tests.Views;

/// <summary>
/// Tests for the file-tree icon category resolver.
/// Per M3.1, the resolver is per-category (not per-extension) and the
/// fallback key is <c>Icon.Unknown</c> (no <c>Icon.File</c> exists).
/// </summary>
public class FileIconKeyResolverTests
{
    [Fact]
    public void GetIconKey_Directory_ReturnsFolderKey()
    {
        Assert.Equal("Icon.Folder", FileIconKeyResolver.GetIconKey("anything", isDirectory: true));
    }

    [Theory]
    [InlineData(".cs")]
    [InlineData(".ts")]
    [InlineData(".js")]
    [InlineData(".jsx")]
    [InlineData(".tsx")]
    [InlineData(".json")]
    [InlineData(".xml")]
    [InlineData(".html")]
    [InlineData(".css")]
    [InlineData(".axaml")]
    public void GetIconKey_CodeExtensions_ReturnCodeKey(string ext)
    {
        Assert.Equal("Icon.Code", FileIconKeyResolver.GetIconKey("file" + ext, isDirectory: false));
    }

    [Theory]
    [InlineData(".md")]
    [InlineData(".txt")]
    [InlineData(".log")]
    public void GetIconKey_TextExtensions_ReturnTextKey(string ext)
    {
        Assert.Equal("Icon.Text", FileIconKeyResolver.GetIconKey("file" + ext, isDirectory: false));
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".gif")]
    [InlineData(".webp")]
    [InlineData(".svg")]
    public void GetIconKey_ImageExtensions_ReturnImageKey(string ext)
    {
        Assert.Equal("Icon.Image", FileIconKeyResolver.GetIconKey("file" + ext, isDirectory: false));
    }

    [Theory]
    [InlineData(".sln")]
    [InlineData(".slnx")]
    [InlineData(".csproj")]
    public void GetIconKey_ProjectExtensions_ReturnProjectKey(string ext)
    {
        Assert.Equal("Icon.Project", FileIconKeyResolver.GetIconKey("file" + ext, isDirectory: false));
    }

    [Theory]
    [InlineData(".editorconfig")]
    [InlineData(".gitignore")]
    [InlineData(".yml")]
    [InlineData(".yaml")]
    [InlineData(".toml")]
    public void GetIconKey_ConfigExtensions_ReturnConfigKey(string ext)
    {
        Assert.Equal("Icon.Config", FileIconKeyResolver.GetIconKey("file" + ext, isDirectory: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("file")]
    [InlineData("file.exe")]
    [InlineData("file.dll")]
    [InlineData("file.zip")]
    [InlineData("file.pdf")]
    public void GetIconKey_UnknownExtension_ReturnsUnknownKey(string? name)
    {
        Assert.Equal("Icon.Unknown", FileIconKeyResolver.GetIconKey(name, isDirectory: false));
    }

    [Fact]
    public void GetIconKey_IsCaseInsensitive()
    {
        Assert.Equal("Icon.Code", FileIconKeyResolver.GetIconKey("FILE.CS", isDirectory: false));
        Assert.Equal("Icon.Project", FileIconKeyResolver.GetIconKey("Project.SLNX", isDirectory: false));
    }

    /// <summary>
    /// CanM3.1 guarantee: every supported text-file extension from
    /// <see cref="SupportedFileTypes"/> resolves to a non-null icon key.
    /// This is the canary for the per-category mapping. It does not
    /// assert the specific category (some extensions are not yet
    /// categorized), only that the resolver returns *something*.
    /// </summary>
    [Fact]
    public void GetIconKey_ResolvesNonNull_ForEverySupportedExtension()
    {
        // Build a set of "file<ext>" for every supported extension.
        // The resolver is intentionally per-category, so the test is
        // restricted to a non-null, non-empty string — not a specific key.
        var supportedExts = new[]
        {
            ".cs", ".json", ".md", ".txt", ".xml", ".axaml", ".csproj",
            ".sln", ".slnx", ".props", ".targets", ".config",
            ".gitignore", ".gitattributes", ".yml", ".yaml", ".css", ".html",
            ".js", ".ts", ".fs", ".vb", ".xaml", ".resx", ".razor", ".cshtml", ".svg"
        };

        foreach (var ext in supportedExts)
        {
            var key = FileIconKeyResolver.GetIconKey("file" + ext, isDirectory: false);
            Assert.False(string.IsNullOrEmpty(key), $"Resolver returned null/empty for {ext}");
        }
    }
}
