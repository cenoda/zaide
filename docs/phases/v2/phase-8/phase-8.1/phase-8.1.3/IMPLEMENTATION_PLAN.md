# Phase 8.1.3: Workspace Close Lifecycle — Implementation Plan

## Scope

**Goal:** Implement M3 only: a reachable, cleanup-safe close-folder operation
and the workspace notification seam needed by Phase 8.3.

**Dependencies:** Phase 8.1.1 is complete and green. Phase 8.1.2 is not a
runtime dependency and need not be expanded by this slice.

**Out of scope:** Project discovery/context service, command-registry
registration, editor/terminal settings, Settings UI, M4–M6, and Phase 8.2/8.3
production work.

## Implementation Contract

- Add parameterless `Workspace.WorkspaceFolderChanged`. `SetProjectFromPath()`
  updates `WorkspacePath` and `ProjectName` first, then raises it; null is the
  supported close transition. Keep document ownership unchanged.
- Make `FileTreeViewModel.RootPath` privately settable and add public
  `SetRootPath(string? path)` as the sole writer.
- `SetRootPath(null)` disposes `_watcherSubscription`, calls
  `_fileTreeService.StopWatching()`, clears `RootNodes`, clears `SelectedFile`,
  resets status state, then exposes the null path.
- For opens, validate/enumerate before tearing down the current watcher. A
  failed open leaves existing tree and watcher state intact.
- Add local `MainWindowViewModel.CloseFolderCommand`, enabled only when a folder
  is open; do not register it in a command registry. Add
  `FileTreeViewModel.CloseFolderRequested` and bridge it in `Activate()` so the
  interaction always completes. Remove the RootPath null filter so close flows
  through `Workspace.SetProjectFromPath(null)` and Source Control refresh.
- Add only the required file-tree header close affordance. No menu bar and no
  keybinding work belongs here.

## Required Tests

- Command → interaction → `SetRootPath(null)` flow; watcher disposal,
  `StopWatching`, tree/selection cleanup, and interaction completion.
- `WorkspaceFolderChanged` observes already-null `WorkspacePath`; Source
  Control returns to its uninitialized state while open documents remain.
- A failed open preserves the prior watcher and root/tree state.

## Exit Conditions

- [x] M3 tests, build, and full test suite are green.
- [x] No project-context, command-registry, UI settings, or M4+ behavior was
      added.

## Rollback Plan

- Revert the isolated M3 commit if a close/open transition can lose a valid
  watcher or document state.
