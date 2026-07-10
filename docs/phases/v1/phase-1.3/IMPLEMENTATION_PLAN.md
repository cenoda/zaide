# Phase 1.3: File Tree Workflow Polish — Implementation Plan

**Status:** DRAFT — based on Phase 1.2 carry-over findings.

## Pre-Implementation Verification

- [ ] `dotnet build Zaide.slnx` passes with 0 errors
- [ ] `dotnet test Zaide.slnx` passes: 93 tests, 0 failures
- [ ] Phase 1.2 TOFIX.md reviewed (carry-over items F1–F4 confirmed)
- [ ] Live code checked for current create-file, prompt, and watcher behavior

---

## Scope

**Goal:** Finish the next layer of file-tree creation workflow polish: better post-create flow, safer filename defaults, a prompt that matches the app, and drag-and-drop file moving.

**Boundaries (NOT building):**
- No inline rename yet
- No delete/trash flow
- No multi-select drag-and-drop
- No cut/copy/paste file moving
- No external file operations ("Reveal in Finder", "Open With", etc.)
- No status bar UI for `StatusText`

---

## Milestones (Incremental)

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate | `dotnet build Zaide.slnx && dotnet test Zaide.slnx` |
| M1 | Auto-open newly created files | Create file from tree → file appears and opens in editor tab |
| M2 | Default `.txt` extension for extensionless names | Enter `notes` → creates `notes.txt` |
| M3 | Theme the create prompt with app palette | Prompt uses app colors and remains modal/keyboard-safe |
| M4 | Drag-and-drop file moving | Drag file to directory → file moved on disk and tree updates |

---

### M1: Auto-Open Newly Created Files

**Files to modify:**

- `src/ViewModels/FileTreeViewModel.cs`
  - Add a create-result signal for newly created files, separate from the watcher.
  - For file creation only, publish the created file node or full path after successful `CreateNodeCommand`.
  - Do not auto-open newly created folders.

- `src/ViewModels/MainWindowViewModel.cs`
  - Subscribe to the create-result signal.
  - Reuse the existing editor open path so newly created files open exactly like tree-opened files.

**Tests to add:**
- `CreateNodeCommand_File_PublishesOpenRequest`
- `CreateNodeCommand_Folder_DoesNotPublishOpenRequest`

---

### M2: Default `.txt` Extension

**Files to modify:**

- `src/ViewModels/FileTreeViewModel.cs`
  - Normalize new file names before calling the service:
    - If `IsDirectory == false` and the entered name has no extension, append `.txt`.
    - Preserve names that already have an extension.
    - Do not modify directory names.

- `src/Views/FileTreeView.cs`
  - Update prompt hint text if useful to clarify the default behavior.

**Tests to add:**
- `CreateNodeCommand_AppendsTxt_WhenFileNameHasNoExtension`
- `CreateNodeCommand_PreservesExtension_WhenFileNameAlreadyHasOne`
- `CreateNodeCommand_DoesNotAppendTxt_ForDirectory`

---

### M3: Prompt Visual Polish

**Files to modify:**

- `src/Views/FileTreeView.cs`
  - Apply app palette resources to the create prompt window and its controls:
    - background from `DeepBase` or `SurfaceBase`
    - text from `TextActive`
    - accents from `SoftAccent`
  - Keep the existing modal behavior, Enter/Escape shortcuts, and close-button safety.
  - Keep the implementation in C# to match `DESIGN.md` Rule 1.

**No new tests (UI-only milestone).**

---

### M4: Drag-and-Drop File Moving

**Files to modify:**

- `src/Services/FileTreeService.cs`
  - Add a thin `MoveNode(string sourcePath, string destinationPath)` wrapper.
  - Move files with `File.Move(...)` and directories with `Directory.Move(...)`.

- `src/ViewModels/FileTreeViewModel.cs`
  - Add a move command that accepts source node + destination directory.
  - Validate:
    - source exists
    - destination is a directory
    - no-op if source and destination are the same parent
  - Surface failures through `StatusText`.
  - Do not manually rebuild the tree; watcher remains the update source.

- `src/Views/FileTreeView.cs`
  - Add drag source and drop target handling for file tree items.
  - Support moving files into directories only for this phase.
  - Dropping onto a file resolves to its parent directory or is rejected explicitly.

**Tests to add:**
- `MoveNodeCommand_SetsStatusText_WhenDestinationInvalid`
- `MoveNodeCommand_NoOp_WhenDroppingIntoSameParent`

---

## Limitations (by design)

- **Auto-open is file-only** — newly created folders stay selected but do not open anything.
- **Default extension is only `.txt`** — no language-aware template inference yet.
- **Prompt polish is visual only** — not a reusable dialog component in this phase.
- **Drag-and-drop only supports move** — no copy modifier, no multi-select, no cross-root move.
- **Watcher remains source of truth after move** — UI may update after a short debounce rather than instantly.

---

## Exit Conditions

- [ ] `dotnet build Zaide.slnx` passes with 0 errors
- [ ] `dotnet test Zaide.slnx` passes
- [ ] "New File" opens the newly created file in an editor tab
- [ ] Extensionless new file names default to `.txt`
- [ ] Create prompt uses app palette colors and remains modal
- [ ] Dragging a file into a directory moves it successfully
- [ ] Invalid drag/drop move shows `StatusText` error instead of crashing
- [ ] No XAML added beyond `App.axaml` + `MainWindow.axaml` shell

---

## Rollback Plan

- Revert to: current HEAD after Phase 1.2
- Preserve: `docs/`, `App.axaml`, Phase 1.1/1.2/2/2.1 source
- Discard: Phase 1.3 file-tree workflow polish changes in `FileTreeService.cs`, `FileTreeViewModel.cs`, `MainWindowViewModel.cs`, and `FileTreeView.cs`

---

*Draft created: 2026-06-27. Derived from Phase 1.2 carry-over items F1–F4.*
