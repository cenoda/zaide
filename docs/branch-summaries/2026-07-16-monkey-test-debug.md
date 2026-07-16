# `monkey-test-debug` Branch Closeout

**Merged into:** `master` on 2026-07-16  
**Integrated commit range:** `d79900d..20a331f` (20 commits)

## Delivered Changes

### Explorer and file synchronization

- Added live file-tree synchronization with a refresh fallback.
- Added file and directory deletion from the explorer.
- Hardened file watching for path normalization and buffer-overflow/error cases.

### Source control

- Added a two-step Commit/Push primary action, including push through the system Git CLI.
- Added success feedback, stage-all support, branch-selection preservation after refresh,
  and source-control refresh when the bottom-panel mode changes.
- Added editor tabs for file diffs and refined source-control layout and status wording.

### Settings and UI lifecycle

- Made the settings panel lifecycle reactive and added vertical scrolling.
- Added an installed-font catalog and an interactive settings font picker.
- Centered status-bar text elements and made agent-panel hosts closable.

### Coverage

- Added or expanded unit and integration coverage for file-tree synchronization and
  deletion, Git mutation and repository flows, source-control action derivation and
  diff tabs, agent-panel lifetime, settings/font-picker behavior, and command wiring.

## Validation Record

- Manual monkey testing was completed before this merge, as confirmed by the user.
- This closeout performed no additional automated build or test run; the merge was a
  clean fast-forward from `monkey-test-debug` into `master`.

## Scope Note

This was an out-of-band UI, explorer, and source-control integration branch. It does
not change the planned scope or milestone status of Phase 13 Release Hardening.
