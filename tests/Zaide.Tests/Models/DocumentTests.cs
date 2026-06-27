using System;
using System.Threading.Tasks;
using Moq;
using Zaide.Models;
using Zaide.Services;
using Xunit;

namespace Zaide.Tests.Models
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
        public async Task SaveAsync_DelegatesToFileService()
        {
            var filePath = "/path/to/file.txt";
            var content = "Hello, World!";
            var document = new Document(filePath, content);
            document.MarkClean();
            var fileServiceMock = new Mock<IFileService>();

            await document.SaveAsync(fileServiceMock.Object);

            fileServiceMock.Verify(fs => fs.WriteAllTextAsync(filePath, content), Times.Once);
            Assert.False(document.IsDirty);
            Assert.Null(document.LastSaveError);
        }

        [Fact]
        public async Task SaveAsync_FileServiceThrows_UpdatesLastSaveError()
        {
            var filePath = "/path/to/file.txt";
            var content = "Hello, World!";
            var document = new Document(filePath, content);
            document.MarkClean();
            var fileServiceMock = new Mock<IFileService>();
            var exceptionMessage = "Failed to save file";

            fileServiceMock
                .Setup(fs => fs.WriteAllTextAsync(filePath, content))
                .ThrowsAsync(new Exception(exceptionMessage));

            await Assert.ThrowsAsync<Exception>(() => document.SaveAsync(fileServiceMock.Object));

            Assert.Equal(exceptionMessage, document.LastSaveError);
            Assert.True(document.IsDirty);
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
    }
}
