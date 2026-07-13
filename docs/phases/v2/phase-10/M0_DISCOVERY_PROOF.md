# Phase 10 M0: C# Language Intelligence Discovery Proof

## Status: M0 live-code baseline complete; executable technology proof pending

**Verified against live code at:** `3972bff8947f87feac97ecb74d02810c7655ba6e` (2026-07-13)
**Build/test re-verified at same revision:** 1512 passed / 0 failed (0 build warnings/errors).

This document records the M0 baseline. It does not select an LSP server or
client library, and it introduces no production code.

## 1. Live Ownership and Connection Graph

```text
FileTreeViewModel.RootPath
  -> MainWindowViewModel.Activate()
  -> Workspace.SetProjectFromPath(root)
  -> IProjectContextService.LoadAsync / observable ProjectContext snapshots

FileTreeViewModel.OpenFileRequested
  -> EditorTabViewModel.OpenFileCommand
  -> Workspace.OpenDocument(path, content)
  -> EditorTabViewModel.OpenTabs + ActiveTab
  -> Workspace.ActiveDocument
  -> MainWindow active-tab subscription
  -> shared EditorView.ViewModel
  -> shared AvaloniaEdit TextEditor
```

### `Document`

`Models.Document` owns the file path, current text, dirty state, and last save
error. `Content` changes raise events and mark the document dirty. It has no
language or project semantics. Phase 10 must use the document path and content
as LSP synchronization inputs, but must not make it an LSP client.

### `Workspace`

The singleton `Models.Workspace` owns the open-document dictionary and
`ActiveDocument`; it also retains legacy folder display state (`WorkspacePath`
and `ProjectName`). `EditorTabViewModel` updates `ActiveDocument` on each tab
activation. It is not the authoritative C# project model. Phase 10 must not
discover a project from `WorkspacePath` or add a parallel project state here.

### Project context

The singleton `IProjectContextService` owns immutable `ProjectContext`
snapshots and the load/reload/unload/selection lifecycle. Its states are
`Unloaded`, `Loading`, `NoProject`, `Unsupported`, `SingleProject`,
`Ambiguous`, `Selected`, and `Failed`. `MainWindowViewModel` projects the
snapshots for UI use.

**Eligibility rule (locked M0 contract):** A language session starts only for
`SingleProject` or `Selected` states. `SingleProject` means exactly one
supported candidate (`Solution`, `SolutionX`, or `CSharpProject`) was
discovered and auto-selected. `Selected` means the user explicitly picked a
candidate from the `Ambiguous` list. All other states (`Unloaded`, `Loading`,
`NoProject`, `Unsupported`, `Ambiguous`, `Failed`) prevent a session.

**What is passed to the server:** the winning `ProjectCandidate.FilePath`
(normalised absolute path). The LSP `initialize` request uses the *parent
directory* as the `workspaceFolders[].uri` / `rootUri` — this is the protocol
contract. Any server-specific mechanism (e.g. passing the solution file path
via `initializationOptions`) is deferred to the executable M0 proof and must
not be assumed or hard-coded at planning time. Exactly one workspace folder —
no aggregation or multi-root workspace.

### Editor tabs and active editor

`EditorTabViewModel` owns `OpenTabs` and `ActiveTab`; each tab owns one
`EditorViewModel`, which composes one `Document`. Opening and closing call the
matching `Workspace` APIs. `MainWindow` owns exactly one `EditorView` and swaps
its `ViewModel` on active-tab changes. Therefore a Phase 10 document bridge
must key every asynchronous result by document identity plus version and must
discard results after the tab changes, closes, or receives newer text.

### `EditorView`

`EditorView` owns the shared AvaloniaEdit `TextEditor`. It currently synchronizes
VM text to the editor and editor text/caret/selection back to the active view
model. It already exposes presentation-only editor seams for search and folding.
LSP protocol, lifecycle, and state ownership must remain outside this view.
The view's future role is limited to input routing and applying accepted,
version-matched UI updates (completion presentation, navigation selection, and
atomic formatting edits).

## 2. Locked Phase 10 Responsibility Boundaries

| Concern | Owner | Contract |
|---|---|---|
| Server process, JSON-RPC transport, initialize/shutdown/restart, cancellation, failure | new UI-independent language service | One session per authoritative project context; observable state; deterministic teardown. |
| Project root and chosen solution/project | `IProjectContextService` | Language service consumes snapshots only; it never discovers or selects projects. |
| Open text, active document, dirty state | `Workspace` + tab/editor view models | A document bridge observes open/active/close/content changes and sends ordered didOpen/didChange/didClose notifications. |
| Request versioning and stale-result rejection | language document/session layer | Responses must match current session generation, document URI, and document version before projection. |
| Diagnostics, completion, hover, definition, symbols, formatting results | language service state/results | Structured data, cancellation/unsupported/failure states; no presentation-formatted strings. |
| Problems, completion popup, hover, navigation, symbol UI, text-edit application | Views/ViewModels | Project service state only; never own protocol or process lifecycle. |
| Command discovery and gestures | `ICommandRegistry` and existing materialization | Register Phase 10 commands through the established registry path. |

## 3. Lifecycle and Synchronization Contract to Prove Before M1

1. A project-context transition starts a new generation only for
   `SingleProject` or `Selected` states (the candidate may be any
   `ProjectKind`: `Solution`, `SolutionX`, or `CSharpProject`). `Unloaded`,
   `Loading`, `NoProject`, `Unsupported`, `Ambiguous`, and `Failed` stop or
   prevent a session and publish a truthful non-ready state.
2. Context replacement, reload, server exit, user restart, and application
   shutdown cancel outstanding requests, close documents when transport permits,
   dispose transport/process resources, and ignore all old-generation events.
3. For each opened supported C# document, send `didOpen` once after session
   initialization. Content changes use monotonically increasing document
   versions and ordered `didChange`; tab activation alone does not reopen or
   resend unchanged content. Closing a tab sends `didClose` and removes local
   diagnostics/completion state for that URI.
4. An unsaved C# tab with no stable file URI is outside the first implementation
   unless the M0 proof demonstrates a server-supported URI and lifecycle. This
   is a documented limitation, not a reason to invent a second document model.
5. Diagnostics and all request results are cleared or replaced only for the
   matching document/version; no result from an inactive, closed, stale, or
   previous-project document may affect the current editor or Problems surface.
6. Formatting is a request over the current full document version. Returned
   edits are validated, applied atomically as one undoable operation, and then
   flow through the normal `Document.Content` dirty-state path. Failure,
   cancellation, timeout, stale version, overlap, or unsupported capability
   leaves text, selection, and dirty state unchanged.
7. **Position encoding** — before any document synchronisation, the client
   must negotiate the LSP position encoding (`UTF-16`, `UTF-32`, or `UTF-8`
   per the `GeneralClientCapabilities.positionEncodings` parameter). The
   negotiated encoding determines how every LSP `Position.line`/`character`
   maps to an AvaloniaEdit document offset. The default is `UTF-16` when the
   server does not advertise a preference. The encoding choice must support
   non-BMP Unicode characters (surrogate-pair handling) and is locked before
   M1; changing it later breaks all existing position/offset conversion
   throughout the language service.

## 4. M0 Required Technology Proof

Before M1, a compile- and process-backed proof must select exactly one Linux
C# language server and one compatible client/protocol library. It must show:

- installed/server acquisition and version discovery without requiring a
  repository-wide SDK reorganization;
- stdio process launch, initialize, initialized, graceful shutdown/exit, and
  forced-process-exit handling;
- workspace-folder and selected project/solution initialization inputs;
- `didOpen`, incremental or full `didChange`, and `didClose` for a C# file;
- published diagnostics plus one request each for completion, hover,
  definition, document/workspace symbols, and `textDocument/formatting`;
- cancellation and a restart without leaked child processes or stale callbacks;
- license, package compatibility, and test-host feasibility recorded in
  `docs/LIBRARIES.md` if a package is adopted.
- **Position encoding:** record the client's `GeneralClientCapabilities.positionEncodings`
  and the server's selected encoding in its `InitializeResult`. Document which
  encoding was requested, which was selected, the actual encoding values for
  non-BMP characters (e.g. 🎉 or multi-byte CJK), and confirm the conversion
  round-trips correctly between LSP positions and AvaloniaEdit document offsets.
- **.slnx compatibility:** verify server initialisation with a `.slnx` solution
  file. If the server does not recognise the `.slnx` format, document whether
  using the parent directory as a plain workspace folder is an acceptable
  fallback, and whether the eligibility rule (all three `ProjectKind` values
  are accepted) needs amending.
- **AvaloniaEdit position encoding:** record the behaviour of
  `AvaloniaEdit.Document.GetLocation(offset)` and `Document.GetOffset(line,
  column)`. Confirm whether they use UTF-16 or byte-offset column semantics
  internally (AvaloniaEdit stores text as UTF-16, so positions are expected
  to be UTF-16-based). Document the conversion function required between the
  negotiated LSP encoding and AvaloniaEdit offsets.

The proof must record actual server/library versions and APIs. No roadmap text
is evidence that a candidate works with this Avalonia/.NET 10 application.

### M6 formatting prerequisites (proven at M0, used at M6)

Because M6 depends on undo grouping, caret/selection mapping, and a settings
schema that must be correct before formatting code is written, the M0 proof
must also establish:

- **Edit grouping:** confirm that `AvaloniaEdit.Document.UndoStack.StartUndoGroup()`
  / `EndUndoGroup()` wraps a whole-document `Document.Text = …` replacement into
  a single undoable action. The existing `IEditorTextOperations.ReplaceAllMatches`
  pattern proves the seam exists; M0 must record the actual behaviour when the
  full document text is replaced (not just individual ranged replacements) and
  confirm that the resulting undo transaction collapses as one `UndoableMergeGroup`
  with a single undo step for the user.

- **Caret/selection mapping:** document the behaviour of `TextEditor.CaretOffset`,
  `SelectionStart`, and `SelectionLength` when the document text is fully replaced
  via `Document.Text = …`. Verify that after a formatting edit the caret remains
  at a deterministic position (end-of-document, zero, or the most equivalent
  line/column) and that the selection is cleared or remapped in a predictable
  way. Record the mapping rule so M6 can implement it.

- **Format-on-Save setting schema:** lock the typed model shape before M6
  writes production code. The live settings model is a top-level `sealed record
  EditorSettings` in `SettingsModel.cs` with a static `Default` instance,
  `[JsonPropertyName]` serialisation keys, a `SchemaVersion` integer, and
  `ISettingsMigration` entries for version transitions. The M0 proof must
  record:

  - **Property:** `EditorSettings.FormatOnSave` with
    `[property: JsonPropertyName("formatOnSave")] bool FormatOnSave`.
  - **Default:** `false` in `EditorSettings.Default`.
  - **Schema version:** bump from `1` to `2`. The settings validator
    (`SettingsValidator`) currently accepts `SchemaVersion >= 1`; the
    version floor is unchanged, but the presence of a v2-capable model
    is the signal that migrations are complete.
  - **Migration:** an `ISettingsMigration` implementation from `FromVersion: 1`
    to `ToVersion: 2` that adds `FormatOnSave: false` via `with` expression
    (no data loss for existing properties). Registration must be added to
    the production `SettingsService` constructor's migration list.
  - **Serialiser ceiling:** the live `SettingsSerializer` rejects any
    `schemaVersion is < 1 or > 1` (`SettingsSerializer.cs:67`) before
    deserialisation or validation runs. The M6 schema change must update this
    ceiling from `> 1` to `> 2`. The `SettingsValidator` only enforces the
    floor (`SchemaVersion >= 1`) and needs no change.
  - **Compatibility:** a v1 file loaded on v2 code is migrated transparently
    (v1 → v2 migration runs). A v2 file loaded on v1 code is rejected by the
    serializer's ceiling check before deserialisation — this is acceptable
    because v2 ships at M6, by when v1 is the only field-deployed version
    and no v2 file can exist in the wild.
  - **Settings UI ownership:** deferred to M6 — no settings-view toggle or
    keybinding-editor integration is required at M0. The `EditorSettings`
    property and migration are sufficient to prove the schema is viable.

  No code is written at M0, but the concrete property path, serialisation key,
  version boundary, migration shape, and UI ownership are locked so M6 can
  implement the feature without an unplanned schema decision.

- **Format-on-Save execution contract (locked at M0, implemented at M6):**
  The live save path is `EditorViewModel.SaveAsync` → `IFileService.WriteAllTextAsync`
  (`EditorViewModel.cs:177-193`), invoked via `SaveActiveTabCommand` from
  `MainWindowViewModel`. The Format-on-Save contract is:

  1. **Sequence:** formatting runs *before* the file write, on the in-memory
     `Document.Content` of the active tab. The formatted text replaces
     `Document.Content` (flowing through the normal dirty-state path) and
     is then written to disk.
  2. **Failure/cancellation:** if formatting fails, is cancelled, times out,
     or the server does not advertise `textDocument/formatting`, save proceeds
     with the current (unformatted or partially formatted) content. Formatting
     is a presentation enhancement; it is never a data-integrity gate that
     blocks the user's save.
  3. **Coordination layer:** `EditorViewModel.SaveAsync` queries the
     `EditorSettings.FormatOnSave` flag and, when `true`, calls a formatting
     service method (e.g. `ILanguageFormattingService.FormatDocumentAsync`)
     before the file write. This keeps the save path in the ViewModel layer
     without pushing LSP concerns into it — the formatting service is a
     language-service interface whose M6 implementation issues the
     `textDocument/formatting` request and returns formatted text (or null
     on failure/unsupported).
  4. **No double-save or infinite loop:** the formatting edit marks the
     document dirty before the save writes it; the save then clears the dirty
     flag. The formatting service must not re-trigger save.
  5. **Ctrl+S path unchanged:** `SaveActiveTabCommand` continues to delegate
     to the active tab's `SaveCommand`; the Format-on-Save logic is invisible
     to the command and keybinding layers.

## 5. Source Layout Decision

**Decision: do not perform a broad `src/` reorganization.** The live layout
already separates models, services, view models, and views, and the established
project-context seam is in `Services`. Phase 10 may add a focused language
service namespace/folder and narrowly move a file only when a demonstrated
dependency cycle or a confirmed Views-to-Services/ViewModels boundary violation
cannot be resolved in place. Merely grouping files by anticipated LSP concepts
is not sufficient evidence for a move.

## 6. Baseline Verification

Run sequentially; do not overlap the build and test processes:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

At this M0 planning baseline, the build passed with 0 warnings and 0 errors.
The live sequential verification at commit `3972bff` produces **1512 passed /
0 failed** (1512 total).

**Historical note — flaky FD test:** During early M0 planning the
`LinuxTerminalServiceTests.Restart_DoesNotLeakFileDescriptors` test reported FD
growth from 228 to 232 across five restarts (1511/1). This was a pre-existing
intermittent issue outside Phase 10, and the test has since passed consistently
on the same host. No Phase 10 milestone gate depends on resolving it.
