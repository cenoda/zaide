using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Services;

namespace Zaide.Models
{
    public class Workspace
    {
        private readonly Dictionary<string, Document> _documents = new();

        public IReadOnlyCollection<Document> Documents => _documents.Values.ToList().AsReadOnly();

        public Document? ActiveDocument { get; private set; }

        public Document OpenDocument(string path, IFileService fileService)
        {
            if (_documents.TryGetValue(path, out var existingDocument))
            {
                ActiveDocument = existingDocument;
                return existingDocument;
            }

            var content = fileService.ReadAllTextAsync(path).GetAwaiter().GetResult();
            var document = new Document(path, content);
            _documents[path] = document;
            ActiveDocument = document;
            return document;
        }

        public void CloseDocument(string path)
        {
            if (_documents.Remove(path))
            {
                if (ActiveDocument?.FilePath == path)
                {
                    ActiveDocument = _documents.Values.FirstOrDefault();
                }
            }
        }

        public void SetActiveDocument(Document document)
        {
            if (_documents.ContainsKey(document.FilePath))
            {
                ActiveDocument = document;
            }
        }
    }
}
