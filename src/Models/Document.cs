using System;
using System.Threading.Tasks;
using Zaide.Services;

namespace Zaide.Models
{
    public class Document
    {
        public string Content { get; set; }
        public string FilePath { get; set; }
        public bool IsDirty { get; set; }
        public string? LastSaveError { get; set; }

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
