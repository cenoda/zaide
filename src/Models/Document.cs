using System;
using System.Threading.Tasks;
using Zaide.Services;

namespace Zaide.Models
{
    public class Document
    {
        public string Content { get; set; }
        public string FilePath { get; init; }
        public bool IsDirty { get; private set; }
        public string? LastSaveError { get; private set; }

        public Document(string filePath, string content = "")
        {
            FilePath = filePath;
            Content = content;
            IsDirty = false;
            LastSaveError = null;
        }

        public async Task SaveAsync(IFileService fileService)
        {
            try
            {
                await fileService.WriteAllTextAsync(FilePath, Content);
                IsDirty = false;
                LastSaveError = null;
            }
            catch (Exception ex)
            {
                LastSaveError = ex.Message;
                IsDirty = true;
                throw;
            }
        }

        public void MarkClean()
        {
            IsDirty = false;
            LastSaveError = null;
        }
    }
}
