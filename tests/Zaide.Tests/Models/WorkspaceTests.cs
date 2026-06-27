using System;
using System.IO;
using Moq;
using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Models
{
    public class WorkspaceTests
    {
        [Fact]
        public void OpenDocument_ReturnsExistingDocumentOnRepeatCall()
        {
            var fileServiceMock = new Mock<IFileService>();
            fileServiceMock.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>()))
                .ReturnsAsync("test content");

            var workspace = new Workspace();
            var path = "/path/to/file.txt";

            var firstCall = workspace.OpenDocument(path, fileServiceMock.Object);
            var secondCall = workspace.OpenDocument(path, fileServiceMock.Object);

            Assert.Same(firstCall, secondCall);
        }

        [Fact]
        public void OpenDocument_CreatesNewDocumentOnFirstCall()
        {
            var fileServiceMock = new Mock<IFileService>();
            fileServiceMock.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>()))
                .ReturnsAsync("test content");

            var workspace = new Workspace();
            var path = "/path/to/file.txt";

            var document = workspace.OpenDocument(path, fileServiceMock.Object);

            Assert.NotNull(document);
            Assert.Equal(path, document.FilePath);
            Assert.Equal("test content", document.Content);
        }

        [Fact]
        public void CloseDocument_RemovesFromCache()
        {
            var fileServiceMock = new Mock<IFileService>();
            fileServiceMock.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>()))
                .ReturnsAsync("test content");

            var workspace = new Workspace();
            var path = "/path/to/file.txt";

            workspace.OpenDocument(path, fileServiceMock.Object);
            Assert.Single(workspace.Documents);

            workspace.CloseDocument(path);
            Assert.Empty(workspace.Documents);
        }

        [Fact]
        public void CloseDocument_UpdatesActiveDocument()
        {
            var fileServiceMock = new Mock<IFileService>();
            fileServiceMock.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>()))
                .ReturnsAsync("test content");

            var workspace = new Workspace();
            var path1 = "/path/to/file1.txt";
            var path2 = "/path/to/file2.txt";

            var doc1 = workspace.OpenDocument(path1, fileServiceMock.Object);
            var doc2 = workspace.OpenDocument(path2, fileServiceMock.Object);
            Assert.Equal(doc2, workspace.ActiveDocument);

            workspace.CloseDocument(path2);
            Assert.Equal(doc1, workspace.ActiveDocument);
        }

        [Fact]
        public void SetActiveDocument_UpdatesActiveDocument()
        {
            var fileServiceMock = new Mock<IFileService>();
            fileServiceMock.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>()))
                .ReturnsAsync("test content");

            var workspace = new Workspace();
            var path1 = "/path/to/file1.txt";
            var path2 = "/path/to/file2.txt";

            var doc1 = workspace.OpenDocument(path1, fileServiceMock.Object);
            var doc2 = workspace.OpenDocument(path2, fileServiceMock.Object);

            workspace.SetActiveDocument(doc1);
            Assert.Equal(doc1, workspace.ActiveDocument);
        }
    }
}
