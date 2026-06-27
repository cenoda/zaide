# Refactor 1: Editing Core Reconstruction — Implementation Plan

## Pre-Implementation Verification

- [x] Current open/save flow understood (Tree → Editor → Save)
- [x] EditorViewModel responsibilities identified (state + logic)
- [x] AvaloniaEdit integration confirmed working
- [x] No new NuGet packages needed — pure C# model extraction.
- [x] All existing tests pass: `dotnet test Zaide.slnx`

## Scope

**Goal:**
Rebuild the editor core around a **Document-centered architecture**, introducing:

- `Document` as single source of truth for file state
- `Workspace` as document manager (registry, active document)
- Both introduced in a single pass — no intermediate throwaway architecture

This establishes the foundation for a real IDE editing system.

**Current architecture (reference):**
```
EditorViewModel (ViewModel + state)
  └─ _textContent: string
  └─ _filePath / _fileName: string
  └─ _isDirty: bool
  └─ _lastSaveError: string?
  └─ SaveCommand → IFileService.WriteAllTextAsync
  └─ LoadFileContent() / MarkClean()

EditorTabViewModel (ViewModel + tab management)
  └─ OpenTabs: ObservableCollection<EditorViewModel>
  └─ ActiveTab: EditorViewModel?
  └─ OpenFileAsync() — creates EditorViewModel, sets path/content
  └─ CloseTabAsync() — manages collection, activates neighbor
```

**Target architecture:**
```
Document (Model)
  └─ Content: string
  └─ FilePath: string
  └─ IsDirty: bool
  └─ LastSaveError: string?
  └─ SaveAsync(IFileService)

Workspace (Model — document registry)
  └─ Documents: Dictionary<string, Document>
  └─ ActiveDocument: Document?
  └─ OpenDocument(path, IFileService) → Document
  └─ CloseDocument(path)

EditorViewModel (ViewModel only)
  └─ Document property
  └─ TextContent → Document.Content
  └─ FilePath → Document.FilePath
  └─ IsDirty → Document.IsDirty
  └─ SaveCommand → Document.SaveAsync(IFileService)

EditorTabViewModel (tab UI coordinator)
  └─ References Workspace — delegates document operations
  └─ OpenTabs: ObservableCollection<EditorViewModel>
  └─ ActiveTab: EditorViewModel?
```

**Boundaries (NOT in scope):**
- No UI redesign (tabs, layout remain as-is)
- No LLM integration
- No syntax highlighting changes
- No performance tuning
- No file watcher (external changes ignored)
- No shared/global undo stack
- No split view (single editor per document)
- No persistence of undo history
- ❌ No multi-tab sharing of Document instances (app prevents duplicate tabs)
- ❌ No save-routing layer (WorkspaceService/ISaveService)
- ❌ No Undo/Redo (separate refactor or phase)

## Milestones (Incremental)

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate: current open/edit/save works | `dotnet run` → open → edit → save |
| M1 | Introduce `Document` model (`FilePath`, `Content`, `IsDirty`, `LastSaveError`, `SaveAsync(IFileService)`, `MarkClean()`). Plain model — no `ReactiveObject`. | `DocumentTests` — construct, store/return values, SaveAsync delegates to IFileService, MarkClean resets dirty |
| M2 | Refactor `EditorViewModel` to wrap `Document` only. Replace `_textContent`, `_filePath`, `_fileName`, `_isDirty`, `_lastSaveError`, `_suppressDirty`, `SaveAsync()` with delegation to `Document`. Keep `TextContent`, `FilePath`, `IsDirty`, `DisplayName`, `SaveCommand`, `LoadFileContent()`, `MarkClean()` as public API surface. SaveCommand calls `Document.SaveAsync(IFileService)`. | • `EditorViewModelTests` behavioral contract tests pass unchanged (IsDirty defaults, MarkClean, DisplayName, SaveCommand failure paths, LoadFileContent) — these assert VM API behavior, not internals<br>• `SaveCommand_WritesFile` and `SaveCommand_ClearsDirty` stay as VM integration tests (they exercise the public save pathway)<br>• `FileService`-dependent save tests move to `DocumentTests` (they test Document.SaveAsync directly) |
| M3 | Introduce `Workspace` + refactor `EditorTabViewModel` in **one step**. Workspace is the document registry (`path → Document`). `Workspace.OpenDocument(path, IFileService)` checks its cache: if a Document for `path` already exists, returns it; otherwise creates, caches, and returns a new Document. EditorTabViewModel calls `Workspace.OpenDocument`, then checks if the returned Document is already represented in `OpenTabs`; if yes, activates the existing tab; if no, wraps the Document in a new EditorViewModel and adds it. Close delegates to `Workspace.CloseDocument(path)` then removes the tab. Tab switch updates `Workspace.ActiveDocument`. | • `WorkspaceTests` — OpenDocument returns existing Document on repeat call, creates new Document on first call; CloseDocument removes from cache; ActiveDocument tracks correctly<br>• `EditorTabViewModelTests` — existing open/close/activate tests pass unchanged; open flow now goes through Workspace<br>• Open path: file path → Workspace.OpenDocument (cached or new) → Document → EditorViewModel wrapper<br>• Close path: EditorTabViewModel calls Workspace.CloseDocument then removes tab<br>• Tab switch: EditorTabViewModel sets Workspace.ActiveDocument |
| M4 | Stabilize state + regression sweep. Dirty-flag edge cases, close tab with unsaved changes, tab switch with dirty document, error propagation from Workspace to EditorTabViewModel. | Manual regression: open → edit → save → close → reopen. All existing `dotnet test` pass with zero regressions. |

### Removed from original plan

| Old Milestone | Reason for Removal |
|---------------|--------------------|
| Old M4: Document cache in EditorTabViewModel | Scope creep — app prevents duplicate tabs; multi-tab Document sharing is a future split-view concern. Cache lives in Workspace (M3) but is a 1:1 registry, not a sharing mechanism. |
| Old M5: Move tab management into Workspace | Merged into M3 — Workspace is introduced *with* the open-flow change, so there is no intermediate to migrate from. |
| Old M6: Move Save logic → WorkspaceService/ISaveService | Feature work, not refactor — changes command-routing contract. Belongs in its own refactor doc. |
| Old M7: Integrate Undo/Redo | Feature work — entirely new behavior with new keyboard semantics and editor-state integration. Belongs in its own phase or refactor doc. |

## Test migration guide (M2)

When EditorViewModel stops owning file state directly, the following test assertions move from VM tests to Document tests:

| Test | New home | Why |
|------|----------|-----|
| `SaveCommand_WritesFile` | Stays in `EditorViewModelTests` | Exercises public VM save pathway end-to-end |
| `SaveCommand_ClearsDirty` | Stays in `EditorViewModelTests` | Same — behavioral contract of SaveCommand |
| `SaveCommand_Fails_WhenPathIsDirectory` | Stays in `EditorViewModelTests` | VM-level error handling pathway |
| `Document.SaveAsync delegates to IFileService` | New `DocumentTests` | Pure Document model behavior |
| `Document.MarkClean resets IsDirty` | New `DocumentTests` | Pure Document model behavior |
| `Document.Content round-trips` | New `DocumentTests` | Pure Document model behavior |
| All `LoadFileContent` + dirty-suppression tests | Stays in `EditorViewModelTests` | VM-level concern (suppress-while-loading) |

## Limitations (by design)
- `Document` is NOT a `ReactiveObject` — it's a plain model. `EditorViewModel` wraps it with reactive properties.
- `Document` does NOT own `SaveCommand` (ReactiveCommand) — the command stays in the ViewModel layer.
- `Document` takes `IFileService` as method parameter, not constructor injection — keeps it simple and avoids DI lifecycle issues.
- `Workspace` is a plain model class, not a ReactiveObject — EditorTabViewModel bridges it to the reactive UI layer.
- `Workspace` owns **document identity** — `OpenDocument` returns the same Document instance for the same path. `EditorTabViewModel` owns **tab-level uniqueness** — it checks `OpenTabs` before creating a new tab wrapper, preserving the existing no-duplicate-tab behavior. These are complementary, not contradictory.
- No file watcher, no external-change detection, no auto-reload.
- No Undo/Redo in this refactor — saved for a separate change.
- No multi-tab Document sharing — not needed until split-view is implemented.

## Exit Conditions
- [ ] Build succeeds: `dotnet build`
- [ ] Open → edit → save works: open goes `path → Workspace.OpenDocument → Document → EditorViewModel`; save goes `EditorViewModel.SaveCommand → Document.SaveAsync(IFileService)` directly (Workspace is not in the save path)
- [ ] EditorViewModel delegates all file state to Document (no `_textContent`, `_filePath`, `_isDirty` in EditorViewModel)
- [ ] EditorTabViewModel delegates document operations to Workspace (no Document creation in EditorTabViewModel)
- [ ] No regressions in tab open/close/switch behavior
- [ ] All existing tests pass: `dotnet test` — zero regressions
- [ ] New `DocumentTests` exist and pass
- [ ] New `WorkspaceTests` exist and pass

## Rollback Plan
- Commit hash to revert to: (fill before starting M1)
- Fallback strategy:
  - Restore string-based TextContent in EditorViewModel
  - Remove Document and Workspace classes
  - Revert EditorTabViewModel to its current DI-based transient-VM pattern
