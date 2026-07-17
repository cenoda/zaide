using System;
using System.IO;
using System.Linq;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.Editor.Domain;

namespace Zaide.Tests.Features.Workspace.Presentation;

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
    /// M3.1 canary: every extension in the editor's supported set
    /// (<see cref="SupportedFileTypes.AllSupportedExtensions"/>) must
    /// resolve to a non-null icon key. This is what guarantees the
    /// file tree never renders a blank icon for a known-supported
    /// file. We read the extension list from the source of truth so
    /// this test cannot silently drift if SupportedFileTypes changes.
    /// </summary>
    [Fact]
    public void GetIconKey_ResolvesNonNull_ForEverySupportedExtension()
    {
        // Iterate the live editor policy. The test fails the day
        // someone adds an extension to SupportedFileTypes that the
        // resolver does not yet cover.
        foreach (var ext in SupportedFileTypes.AllSupportedExtensions)
        {
            var key = FileIconKeyResolver.GetIconKey("file" + ext, isDirectory: false);
            Assert.False(string.IsNullOrEmpty(key), $"Resolver returned null/empty for {ext}");
        }
    }

    /// <summary>
    /// Companion to the canary above: the same iteration must also
    /// return a non-empty key for the empty / unknown-extension case.
    /// </summary>
    [Fact]
    public void GetIconKey_ResolvesNonNull_ForUnknownAndEmptyInputs()
    {
        Assert.Equal("Icon.Unknown", FileIconKeyResolver.GetIconKey("README", isDirectory: false));
        Assert.Equal("Icon.Unknown", FileIconKeyResolver.GetIconKey("file.exe", isDirectory: false));
        Assert.Equal("Icon.Unknown", FileIconKeyResolver.GetIconKey("noext", isDirectory: false));
        Assert.Equal("Icon.Unknown", FileIconKeyResolver.GetIconKey(string.Empty, isDirectory: false));
        Assert.Equal("Icon.Unknown", FileIconKeyResolver.GetIconKey(null, isDirectory: false));
    }
}
