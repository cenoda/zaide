using System;
using System.Threading.Tasks;
using Zaide.Services;

namespace Zaide.Models
{
    public class Document
    {
        private string _content;
        public string Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    IsDirty = true;
                }
            }
        }
        public string FilePath { get; init; }
        public bool IsDirty { get; private set; }
        public string? LastSaveError { get; private set; }

        public Document(string filePath, string content = "")
        {
            FilePath = filePath;
            _content = content;
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
