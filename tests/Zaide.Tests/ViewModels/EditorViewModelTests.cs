using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class EditorViewModelTests
{
    [Fact]
    public void FileName_DerivedFromPath()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());

        vm.FilePath = "/home/user/project/Program.cs";

        Assert.Equal("Program.cs", vm.FileName);
    }

    [Fact]
    public void FileName_Untitled_WhenPathEmpty()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());

        vm.FilePath = "";

        Assert.Equal("Untitled", vm.FileName);
    }

    [Fact]
    public void FileName_InitializedFromDocumentPath()
    {
        var vm = new EditorViewModel(
            new Document("/home/user/project/Program.cs"),
            new FileService());

        Assert.Equal("Program.cs", vm.FileName);
    }

    [Fact]
    public void IsDirty_DefaultsToFalse()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());

        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void MarkDirty_WhenTextChanges()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());

        vm.TextContent = "hello world";

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void MarkClean_AfterSave()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());
        vm.TextContent = "hello";
        Assert.True(vm.IsDirty);

        vm.MarkClean();

        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void SaveCommand_Fails_WhenPathIsDirectory()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());
        var dir = Path.Combine(Path.GetTempPath(), "zaide-dir-" + Guid.NewGuid());

        try
        {
            Directory.CreateDirectory(dir);
            vm.FilePath = dir; // directory, not a file → WriteAllText will fail
            vm.TextContent = "should not save";
            Assert.True(vm.IsDirty);

            var result = true;
            vm.SaveCommand.Execute().Subscribe(r => result = r);

            Assert.False(result, "Save should return false on I/O failure");
            Assert.True(vm.IsDirty, "Dirty flag should remain true after failed save");
            Assert.NotNull(vm.LastSaveError);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir);
        }
    }

    [Fact]
    public async Task SaveCommand_WritesFile()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());
        var filePath = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(filePath, "original");
            vm.FilePath = filePath;
            vm.TextContent = "hello world";

            var result = false;
            vm.SaveCommand.Execute().Subscribe(r => result = r);
            await Task.Delay(50);

            Assert.True(result);
            var saved = File.ReadAllText(filePath);
            Assert.Equal("hello world", saved);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SaveCommand_ClearsDirty()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());
        var filePath = Path.Combine(Path.GetTempPath(), "zaide-test-" + Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(filePath, "original");
            vm.FilePath = filePath;
            vm.TextContent = "modified";
            Assert.True(vm.IsDirty);

            vm.SaveCommand.Execute().Subscribe();
            await Task.Delay(50);

            Assert.False(vm.IsDirty);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    // ---------------------------------------------------------------
    // Risky-path tests
    // ---------------------------------------------------------------

    [Fact]
    public void LoadFileContent_DoesNotMarkDirty()
    {
        // LoadFileContent suppresses the dirty-tracking subscription.
        // Even with a large payload, the tab must remain clean.
        var vm = new EditorViewModel(new Document(""), new FileService());
        var large = new string('x', 500_000);

        vm.LoadFileContent(large);

        Assert.Equal(large, vm.TextContent);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void TextContent_AfterLoadFileContent_MarksDirty()
    {
        // After a file load, any subsequent TextContent change should
        // re-engage the dirty subscription.
        var vm = new EditorViewModel(new Document(""), new FileService());
        vm.LoadFileContent("original content");
        Assert.False(vm.IsDirty);

        vm.TextContent = "tweaked";

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void LoadFileContent_LargePayload_SetsTextContent()
    {
        // Simulates loading a very large file (1 MB of text).
        // The content must be stored as-is, not truncated.
        var vm = new EditorViewModel(new Document(""), new FileService());
        var big = new string('A', 1_000_000);

        vm.LoadFileContent(big);

        Assert.Equal(1_000_000, vm.TextContent.Length);
        Assert.Equal(big, vm.TextContent);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void LoadFileContent_EmptyString_ClearsContent()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());
        vm.TextContent = "was here";
        vm.MarkClean();

        vm.LoadFileContent("");

        Assert.Equal("", vm.TextContent);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void DisplayName_ShowsDot_WhenDirty()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());
        vm.FilePath = "/tmp/test.cs";

        Assert.Equal("test.cs", vm.DisplayName);

        vm.TextContent = "changed";
        Assert.Equal("● test.cs", vm.DisplayName);
    }

    [Fact]
    public void DisplayName_ReturnsFileName_WhenClean()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());
        vm.FilePath = "/tmp/report.md";

        Assert.Equal("report.md", vm.DisplayName);

        vm.MarkClean();
        Assert.Equal("report.md", vm.DisplayName);
    }

    [Fact]
    public void SaveCommand_Fails_WhenPathIsEmpty()
    {
        var vm = new EditorViewModel(new Document(""), new FileService());
        vm.TextContent = "content";

        var result = true;
        vm.SaveCommand.Execute().Subscribe(r => result = r);

        Assert.False(result);
        Assert.True(vm.IsDirty);
    }
}
