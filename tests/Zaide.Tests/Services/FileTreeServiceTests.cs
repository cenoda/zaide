using System;
using System.IO;
using System.Linq;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

public class FileTreeServiceTests
{
    private readonly FileTreeService _service = new();

    [Theory]
    [InlineData("node_modules")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData(".git")]
    [InlineData(".vs")]
    [InlineData(".idea")]
    [InlineData("__pycache__")]
    [InlineData(".DS_Store")]
    [InlineData("Thumbs.db")]
    [InlineData(".hidden")]
    public void IsIgnored_ReturnsTrue_ForCommonPatterns(string name)
    {
        Assert.True(_service.IsIgnored(name));
    }

    [Theory]
    [InlineData("src")]
    [InlineData("MyProject")]
    [InlineData("README.md")]
    [InlineData("Program.cs")]
    [InlineData("folder")]
    public void IsIgnored_ReturnsFalse_ForNormalFolders(string name)
    {
        Assert.False(_service.IsIgnored(name));
    }

    [Fact]
    public void EnumerateDirectory_ReturnsNestedTree_ForTempFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "subdir"));
            File.WriteAllText(Path.Combine(root, "readme.md"), "hello");
            File.WriteAllText(Path.Combine(root, "subdir", "nested.txt"), "world");

            var nodes = _service.EnumerateDirectory(root);

            Assert.Equal(2, nodes.Count);
            Assert.Equal("subdir", nodes[0].Name);
            Assert.True(nodes[0].IsDirectory);
            Assert.Single(nodes[0].Children);
            Assert.Equal("nested.txt", nodes[0].Children[0].Name);

            Assert.Equal("readme.md", nodes[1].Name);
            Assert.False(nodes[1].IsDirectory);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnumerateDirectory_SkipsIgnoredDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "node_modules"));
            Directory.CreateDirectory(Path.Combine(root, "src"));
            File.WriteAllText(Path.Combine(root, "node_modules", "package.json"), "{}");
            File.WriteAllText(Path.Combine(root, "src", "Program.cs"), "code");

            var nodes = _service.EnumerateDirectory(root);

            Assert.Single(nodes);
            Assert.Equal("src", nodes[0].Name);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
