using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zaide.Models
{
    public class Workspace
    {
        private readonly Dictionary<string, Document> _documents = new();

        public IReadOnlyCollection<Document> Documents => _documents.Values.ToList().AsReadOnly();

        public Document? ActiveDocument { get; private set; }

        /// <summary>
        /// The project/workspace folder name, or "Zaide" if no folder is open.
        /// </summary>
        public string ProjectName { get; private set; } = "Zaide";

        /// <summary>
        /// Sets the project name from a folder path.
        /// </summary>
        public void SetProjectFromPath(string? folderPath)
        {
            ProjectName = !string.IsNullOrEmpty(folderPath)
                ? Path.GetFileName(folderPath)
                : "Zaide";
        }

        public Document OpenDocument(string path, string content)
        {
            if (_documents.TryGetValue(path, out var existingDocument))
            {
                ActiveDocument = existingDocument;
                return existingDocument;
            }

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

        public void SetActiveDocument(Document? document)
        {
            if (document != null && _documents.ContainsKey(document.FilePath))
            {
                ActiveDocument = document;
            }
            else if (document == null)
            {
                ActiveDocument = null;
            }
        }
    }
}