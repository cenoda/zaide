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
- `Workspace` as document manager (cache, active document)
- Foundation for Undo/Redo as per-document editing capability

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
```

**Target architecture:**
```
Document (Model)
  └─ Content: string
  └─ FilePath: string
  └─ IsDirty: bool
  └─ LastSaveError: string?
  └─ SaveAsync(IFileService)

Workspace (Model manager)
  └─ Documents: Dictionary<string, Document>
  └─ ActiveDocument: Document?
  └─ OpenDocument(path) → Document
  └─ CloseDocument(path)

EditorViewModel (ViewModel only)
  └─ Document property
  └─ TextContent → Document.Content
  └─ FilePath → Document.FilePath
  └─ IsDirty → Document.IsDirty
  └─ SaveCommand → Workspace or Document.SaveAsync()

EditorTabViewModel (simplified)
  └─ References Workspace instead of owning EditorViewModels directly
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

## Milestones (Incremental)

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate: current open/edit/save works | `dotnet run` → open → edit → save |
| M1 | Introduce `Document` model (`FilePath`, `Content`, `IsDirty`, `LastSaveError`, `SaveAsync(IFileService)`, `MarkClean()`). Plain model — no `ReactiveObject`. | Unit test: Document constructs, stores/returns values, SaveAsync delegates to IFileService |
| M2 | Refactor `EditorViewModel` to reference `Document` only. Replace `_textContent`, `_filePath`, `_fileName`, `_isDirty`, `_lastSaveError`, `_suppressDirty`, `SaveAsync()` with delegation to `Document`. Keep `TextContent`, `FilePath`, `IsDirty`, `DisplayName`, `SaveCommand` as public API surface. | Existing `EditorViewModelTests` pass unchanged |
| M3 | Change open flow: path → Document → EditorViewModel. `EditorTabViewModel.OpenFileAsync` creates Document first, then wraps it in EditorViewModel. | Existing `EditorTabViewModelTests` pass unchanged |
| M4 | Introduce Document cache (`path → Document`) in `EditorTabViewModel`. Same file → same Document instance across tabs. | Open same file twice → assert same Document identity |
| M5 | Introduce `Workspace` (Documents collection + ActiveDocument). Move tab management (open/close/switch) into Workspace. | Tab switching works via Workspace |
| M6 | Move Save logic → `WorkspaceService` (or `ISaveService`). Ctrl+S routes through service layer. | Ctrl+S works via service |
| M7 | Integrate Undo/Redo (per Document). Ctrl+Z / Ctrl+Y works reliably. | Undo/Redo test: edit → undo → redo → content matches |
| M8 | Stabilize state: dirty flag edge cases, close tab with unsaved changes, tab switch with dirty document, regression sweep. | Manual regression: open → edit → save → close → reopen |

## Limitations (by design)
- `Document` is NOT a `ReactiveObject` — it's a plain model. `EditorViewModel` wraps it with reactive properties.
- `Document` does NOT own `SaveCommand` (ReactiveCommand) — the command stays in the ViewModel layer.
- `Document` takes `IFileService` as method parameter, not constructor injection — keeps it simple and avoids DI lifecycle issues.
- Undo/Redo is per-document only.
- No persistence of undo history.

## Exit Conditions
- [ ] Build succeeds: `dotnet build`
- [ ] Open → edit → save works via Document
- [ ] Same file always maps to same Document instance (cache works)
- [ ] EditorViewModel contains no file state (only binding)
- [ ] Undo/Redo works reliably per document
- [ ] No regressions in tab behavior
- [ ] All existing tests pass: `dotnet test` — zero regressions

## Rollback Plan
- Commit hash to revert to: (fill before starting M1)
- Fallback strategy:
  - Restore string-based TextContent in EditorViewModel
  - Disable Document cache and Workspace
