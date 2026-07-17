using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zaide.Features.Editor.Domain;

using Zaide.Models;

namespace Zaide.Features.Workspace.Domain
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
        /// The full path of the opened workspace folder, or null when no folder
        /// has been opened. Used by repository discovery to locate the git root.
        /// </summary>
        public string? WorkspacePath { get; private set; }

        /// <summary>
        /// Raised after <see cref="WorkspacePath"/> and <see cref="ProjectName"/>
        /// have been updated by <see cref="SetProjectFromPath"/>. A null
        /// <see cref="WorkspacePath"/> at the time the event fires indicates
        /// the workspace was closed. Document ownership is unchanged.
        /// </summary>
        public event EventHandler? WorkspaceFolderChanged;

        /// <summary>
        /// Raised when a new document is added to <see cref="Documents"/>.
        /// Reactivating an already-open path does not raise this event.
        /// </summary>
        public event EventHandler<Document>? DocumentOpened;

        /// <summary>
        /// Raised after a document path is removed from <see cref="Documents"/>.
        /// </summary>
        public event EventHandler<string>? DocumentClosed;

        /// <summary>
        /// Sets the workspace name and full path from a folder path.
        /// Raises <see cref="WorkspaceFolderChanged"/> after updating properties.
        /// A null or empty <paramref name="folderPath"/> closes the workspace.
        /// </summary>
        public void SetProjectFromPath(string? folderPath)
        {
            WorkspacePath = !string.IsNullOrEmpty(folderPath)
                ? folderPath
                : null;
            ProjectName = !string.IsNullOrEmpty(folderPath)
                ? Path.GetFileName(folderPath)
                : "Zaide";
            WorkspaceFolderChanged?.Invoke(this, EventArgs.Empty);
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
            DocumentOpened?.Invoke(this, document);
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

                DocumentClosed?.Invoke(this, path);
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
