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
            var workspace = new Workspace();
            var path = "/path/to/file.txt";
            var content = "test content";

            var firstCall = workspace.OpenDocument(path, content);
            var secondCall = workspace.OpenDocument(path, content);

            Assert.Same(firstCall, secondCall);
        }

        [Fact]
        public void OpenDocument_CreatesNewDocumentOnFirstCall()
        {
            var workspace = new Workspace();
            var path = "/path/to/file.txt";
            var content = "test content";

            var document = workspace.OpenDocument(path, content);

            Assert.NotNull(document);
            Assert.Equal(path, document.FilePath);
            Assert.Equal(content, document.Content);
        }

        [Fact]
        public void CloseDocument_RemovesFromCache()
        {
            var workspace = new Workspace();
            var path = "/path/to/file.txt";
            var content = "test content";

            workspace.OpenDocument(path, content);
            Assert.Single(workspace.Documents);

            workspace.CloseDocument(path);
            Assert.Empty(workspace.Documents);
        }

        [Fact]
        public void CloseDocument_UpdatesActiveDocument()
        {
            var workspace = new Workspace();
            var path1 = "/path/to/file1.txt";
            var path2 = "/path/to/file2.txt";
            var content = "test content";

            var doc1 = workspace.OpenDocument(path1, content);
            var doc2 = workspace.OpenDocument(path2, content);
            Assert.Equal(doc2, workspace.ActiveDocument);

            workspace.CloseDocument(path2);
            Assert.Equal(doc1, workspace.ActiveDocument);
        }

        [Fact]
        public void SetActiveDocument_UpdatesActiveDocument()
        {
            var workspace = new Workspace();
            var path1 = "/path/to/file1.txt";
            var path2 = "/path/to/file2.txt";
            var content = "test content";

            var doc1 = workspace.OpenDocument(path1, content);
            var doc2 = workspace.OpenDocument(path2, content);

            workspace.SetActiveDocument(doc1);
            Assert.Equal(doc1, workspace.ActiveDocument);
        }
    }
}
