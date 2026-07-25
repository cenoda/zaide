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
        public bool IsDiskAbsent { get; private set; }
        public string? LastSaveError { get; private set; }

        public event EventHandler? ContentChanged;
        public event EventHandler? DirtyStateChanged;
        public event EventHandler? SaveErrorChanged;
        public event EventHandler? DiskAbsentStateChanged;

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

        /// <summary>
        /// Replaces buffer content without marking the document dirty. Used when
        /// reconciling a clean open document with confirmed disk content.
        /// </summary>
        public void ReloadCleanContent(string content)
        {
            ArgumentNullException.ThrowIfNull(content);

            if (IsDiskAbsent)
            {
                IsDiskAbsent = false;
                OnDiskAbsentStateChanged();
            }

            if (_content == content && !IsDirty)
            {
                return;
            }

            _content = content;
            IsDirty = false;
            RaiseContentChangedSafely();
            RaiseDirtyStateChangedSafely();
        }

        /// <summary>
        /// Flags that the backing file is absent on disk while preserving the
        /// current buffer content.
        /// </summary>
        public void FlagDiskAbsent()
        {
            if (IsDiskAbsent)
            {
                return;
            }

            IsDiskAbsent = true;
            RaiseDiskAbsentStateChangedSafely();
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

        protected virtual void OnDiskAbsentStateChanged()
        {
            DiskAbsentStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseContentChangedSafely()
        {
            try
            {
                OnContentChanged();
            }
            catch
            {
                // Observer failures must not break reconciliation.
            }
        }

        private void RaiseDirtyStateChangedSafely()
        {
            try
            {
                OnDirtyStateChanged();
            }
            catch
            {
                // Observer failures must not break reconciliation.
            }
        }

        private void RaiseDiskAbsentStateChangedSafely()
        {
            try
            {
                OnDiskAbsentStateChanged();
            }
            catch
            {
                // Observer failures must not break reconciliation.
            }
        }
    }
}

