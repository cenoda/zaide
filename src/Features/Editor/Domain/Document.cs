using System;

namespace Zaide.Features.Editor.Domain
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
                    OnContentChanged();
                    OnDirtyStateChanged();
                }
            }
        }
        public string FilePath { get; set; }
        public bool IsDirty { get; private set; }
        public string? LastSaveError { get; private set; }

        public event EventHandler? ContentChanged;
        public event EventHandler? DirtyStateChanged;
        public event EventHandler? SaveErrorChanged;

        public Document(string filePath, string content = "")
        {
            FilePath = filePath;
            _content = content;
            IsDirty = false;
            LastSaveError = null;
        }

        public void MarkClean()
        {
            IsDirty = false;
            LastSaveError = null;
            OnDirtyStateChanged();
            OnSaveErrorChanged();
        }

        public void RecordSaveError(string? error)
        {
            LastSaveError = error;
            IsDirty = true;
            OnSaveErrorChanged();
            OnDirtyStateChanged();
        }

        protected virtual void OnContentChanged()
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnDirtyStateChanged()
        {
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSaveErrorChanged()
        {
            SaveErrorChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

