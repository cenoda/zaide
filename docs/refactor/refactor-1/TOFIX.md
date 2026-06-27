# Refactor 1: TOFIX

<!-- Code quality issues found during review. Mark [x] when fixed. -->

- [x] `EditorViewModel.FilePath` can replace `Document`, but the VM only subscribes to document events in the constructor. After a replacement, the new document is no longer wired into the VM’s reactive update path, so UI updates can silently stop.
- [x] `InitializeReactiveProperties()` adds new event handlers every time it runs, but it never unsubscribes the old ones. That means repeated `FilePath` changes can stack duplicate listeners and cause repeated property notifications or leaks.
- [x] The unsubscribe logic still detaches from the wrong document. In `FilePath`, you assign `_document = newDocument` before calling `InitializeReactiveProperties()`, so the unsubscribe step runs against the new document, not the previous one.
- [x] `EditorViewModel.FilePath` still replaces the entire `Document`, which discards model state and weakens the “Document is the source of truth” design. The new document only carries over `Content`, so dirty state and save-error state are lost on path change.
- [x] The `FilePath` setter no longer updates `FileName` when the path is set to the same value on an existing document, which breaks the `Untitled` fallback for documents created with an empty path.
- [x] `Document.Content` changes `IsDirty`, but the model still does not raise `DirtyStateChanged` when that happens. The setter only calls `OnContentChanged()` after setting `IsDirty = true`.
- [x] `EditorTabViewModel` still constructs its own `Workspace` directly instead of receiving it through DI, which weakens the refactor’s architecture and makes the document registry harder to share or test consistently.
- [x] The `Workspace` DI refactor is incomplete: `EditorTabViewModel` now requires a `Workspace`, but the test code and app registration haven’t been updated consistently.
- [x] `EditorTabViewModel.ActiveTab` still only synchronizes `Workspace.ActiveDocument` when the new value is non-null. If any other code sets `ActiveTab = null`, workspace state stays stale unless that caller also remembers to clear `_workspace` manually.
- [x] `EditorViewModel.FilePath` still has a nullability mismatch: it returns `_document?.FilePath`, but the property type is non-nullable `string`, which is why the build keeps warning with `CS8603`.
