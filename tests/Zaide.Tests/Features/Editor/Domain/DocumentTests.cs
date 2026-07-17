using System;
using Zaide.Models;
using Xunit;
using Zaide.Features.Editor.Domain;

namespace Zaide.Tests.Features.Editor.Domain
{
    public class DocumentTests
    {
        [Fact]
        public void Constructor_InitializesProperties()
        {
            var filePath = "/path/to/file.txt";
            var content = "Hello, World!";
            var document = new Document(filePath, content);

            Assert.Equal(filePath, document.FilePath);
            Assert.Equal(content, document.Content);
            Assert.False(document.IsDirty);
            Assert.Null(document.LastSaveError);
        }

        [Fact]
        public void Constructor_DefaultContent_EmptyString()
        {
            var filePath = "/path/to/file.txt";
            var document = new Document(filePath);

            Assert.Equal(filePath, document.FilePath);
            Assert.Equal(string.Empty, document.Content);
            Assert.False(document.IsDirty);
            Assert.Null(document.LastSaveError);
        }

        [Fact]
        public void MarkClean_ResetsDirtyAndError()
        {
            var filePath = "/path/to/file.txt";
            var document = new Document(filePath);
            document.Content = "New content";

            document.MarkClean();

            Assert.False(document.IsDirty);
            Assert.Null(document.LastSaveError);
        }

        [Fact]
        public void RecordSaveError_SetsErrorAndMarksDirty()
        {
            var filePath = "/path/to/file.txt";
            var document = new Document(filePath);
            document.MarkClean();
            Assert.False(document.IsDirty);

            document.RecordSaveError("Failed to save");

            Assert.Equal("Failed to save", document.LastSaveError);
            Assert.True(document.IsDirty);
        }

        [Fact]
        public void RecordSaveError_NullError_ClearsErrorButKeepsDirty()
        {
            var filePath = "/path/to/file.txt";
            var document = new Document(filePath);
            document.RecordSaveError("Previous error");

            document.RecordSaveError(null);

            Assert.Null(document.LastSaveError);
            Assert.True(document.IsDirty);
        }
    }
}
