# Phase 10 M0: C# Language Intelligence Discovery Proof

## Status: M0 complete (live-code baseline + executable technology proof)

**Verified against live code at:** `1cba3dd4e1aadd9d49a3a259b161e17d63de129f` (2026-07-13)
**Build/test re-verified at same revision baseline:** 1512 passed / 0 failed (0 build warnings/errors).
**Executable technology proof:** complete — see §7–§12.
**Selected stack:** `csharp-ls` 0.25.0 + `StreamJsonRpc` 2.22.23 (protocol library) over stdio Content-Length JSON-RPC.

This document records the M0 baseline **and** the executable technology proof.
It introduces no production Phase 10 application code. The proof lives only in
`tools/Phase10M0LanguageIntelligenceProof/` (outside `Zaide.slnx`).

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
contract. Exactly one workspace folder — no aggregation or multi-root
workspace.

**Server-specific option (proven):** `csharp-ls` discovers `.sln` / `.slnx` /
`.csproj` under the workspace folder automatically. An optional
`initializationOptions.solution` path may be supplied; parent-directory
workspace without that option is sufficient for `.csproj` and for `.slnx`
when the file sits in the workspace folder (see §9).

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
   unless a later milestone proves a server-supported URI and lifecycle. The M0
   proof used on-disk fixture URIs only. This remains a documented limitation,
   not a reason to invent a second document model.
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

  No production schema code is written at M0, but the concrete property path,
  serialisation key, version boundary, migration shape, and UI ownership are
  locked so M6 can implement the feature without an unplanned schema decision.

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

The M0 proof project lives under `tools/Phase10M0LanguageIntelligenceProof/`
and is **not** part of `Zaide.slnx`.

## 6. Baseline Verification

Run sequentially; do not overlap the build and test processes:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

At this M0 closeout, the main product build passed with 0 warnings and 0 errors.
Sequential verification produces **1512 passed / 0 failed** (1512 total).

**Historical note — flaky FD test:** During early M0 planning the
`LinuxTerminalServiceTests.Restart_DoesNotLeakFileDescriptors` test reported FD
growth from 228 to 232 across five restarts (1511/1). This was a pre-existing
intermittent issue outside Phase 10, and the test has since passed consistently
on the same host. No Phase 10 milestone gate depends on resolving it.

---

## 7. Technology Selection (locked)

| Role | Choice | Version | License | Acquisition |
|---|---|---|---|---|
| **C# language server** | [`csharp-ls`](https://github.com/razzmatazz/csharp-language-server) (Roslyn-based) | **0.25.0** `(Punia)+19a9574d7577521555f49bf49e94688a3ba67dd2` | MIT | `dotnet tool install -g csharp-ls` (user/machine tool; **no** repository-wide SDK reorganization) |
| **Client / protocol library** | [`StreamJsonRpc`](https://www.nuget.org/packages/StreamJsonRpc) | **2.22.23** (assembly reports `2.22.0.0`) | MIT | NuGet reference on the standalone proof project only |

**Why this pair**

- **Linux-native:** `csharp-ls` is a self-contained .NET global tool; binary at
  `~/.dotnet/tools/csharp-ls` on the proof host.
- **No product-solution coupling:** acquisition is a global (or future local)
  `dotnet tool`; the main `Zaide.slnx` / `Directory.Packages.props` are unchanged
  at M0.
- **Full Phase 10 capability surface proven:** diagnostics, completion, hover,
  definition, document symbols, workspace symbols, document formatting.
- **StreamJsonRpc** is Microsoft's maintained Content-Length-compatible
  JSON-RPC library and is the planned production framing stack for M1+. The M0
  proof exercises Content-Length framed stdio JSON-RPC and records the
  StreamJsonRpc assembly identity; a thin request/notification loop is used so
  lifecycle, cancellation, and generation semantics stay explicit for the proof.

**Not selected (for the record)**

| Candidate | Reason not chosen for M0 |
|---|---|
| OmniSharp (classic) | Heavier install surface; csharp-ls is lighter and already Roslyn-based. |
| `Microsoft.CodeAnalysis.LanguageServer` (VS Code C# extension host) | Bundled with editor extensions; acquisition path is more complex and was not required once csharp-ls proved the full matrix. |
| OmniSharp.Extensions.LanguageClient | Viable alternative client; StreamJsonRpc is lower-level, better aligned with custom generation/cancellation ownership, and avoids pulling the full OmniSharp.Extensions DI surface into M0. |

## 8. Executable Proof Project

| Item | Value |
|---|---|
| Path | `tools/Phase10M0LanguageIntelligenceProof/` |
| Project | `Phase10M0LanguageIntelligenceProof.csproj` (`net10.0` console) |
| Solution membership | **Outside** `Zaide.slnx` / `Zaide.Tests` |
| Fixture | `fixture/Fixture.csproj`, `fixture/Fixture.slnx`, `fixture/Sample.cs` |
| Packages | `StreamJsonRpc` 2.22.23, `Avalonia.AvaloniaEdit` 12.0.0 (headless `TextDocument` proofs) |

### Commands (sequential)

```bash
export PATH="$PATH:$HOME/.dotnet/tools"   # if csharp-ls is a global tool
dotnet build tools/Phase10M0LanguageIntelligenceProof/Phase10M0LanguageIntelligenceProof.csproj
dotnet run --project tools/Phase10M0LanguageIntelligenceProof/Phase10M0LanguageIntelligenceProof.csproj --no-build
```

Optional: `--server /path/to/csharp-ls` overrides PATH discovery.

### Proof run summary (2026-07-13, Linux)

```text
Client protocol library: StreamJsonRpc 2.22.0.0
Server binary: /home/cenoda/.dotnet/tools/csharp-ls
Server version probe: csharp-ls, 0.25.0 (Punia)+19a9574d7577521555f49bf49e94688a3ba67dd2

PASS  server-acquisition
PASS  fixture
PASS  avaloniaedit-position-encoding
PASS  avaloniaedit-undo-group
PASS  avaloniaedit-caret-mapping
PASS  launch
PASS  initialize
PASS  initialized
PASS  didOpen
PASS  diagnostics          (publishDiagnostics count=3; sample CS1002: ; expected)
PASS  completion           (items≈523)
PASS  hover                (Sample.Greet signature + XML doc)
PASS  definition           (locations=1 → Sample.cs Greet)
PASS  documentSymbol       (symbols≈7)
PASS  workspaceSymbol      (symbols≈1 for query "Greet")
PASS  formatting           (request ok; textEdits≈0 on already-tidy fixture)
PASS  didChange            (full-document change version=2)
PASS  cancellation         (CancellationToken + $/cancelRequest)
PASS  didClose
PASS  graceful-shutdown    (exitCode=0)
PASS  forced-exit          (SIGKILL tree; exitCode=137)
PASS  restart-no-leak      (3 generations; leaked=[]; staleCallbacks=0)
PASS  slnx-workspace       (parent-dir workspace + Fixture.slnx candidate)
PASS  non-bmp-position     (🎉 UTF-16 length=2; LSP character uses UTF-16 units)

Total: 24 passed, 0 failed
```

## 9. Process / Protocol Findings

### Server acquisition and version discovery

- Install: `dotnet tool install -g csharp-ls` → tool command `csharp-ls`.
- Version discovery: `csharp-ls --version` →
  `csharp-ls, 0.25.0 (Punia)+19a9574d7577521555f49bf49e94688a3ba67dd2`.
- Process launch: `ProcessStartInfo` with redirected stdin/stdout/stderr,
  `UseShellExecute=false`, working directory = workspace folder.

### Initialize inputs (actually used)

| Input | Value in proof |
|---|---|
| `processId` | host PID |
| `clientInfo` | `Phase10M0LanguageIntelligenceProof` / `1.0.0` |
| `rootUri` / `rootPath` | parent directory of winning candidate (`fixture/`) |
| `workspaceFolders` | **exactly one** folder = that parent directory |
| `capabilities.general.positionEncodings` | `["utf-16", "utf-8"]` (prefer utf-16 first) |
| `initializationOptions` | optional `{ solution: <absolute .slnx path> }` on the .slnx exercise; omitted for the primary project-path exercise |
| Winning candidate path (contract) | absolute `Fixture.csproj` or `Fixture.slnx` |

### Lifecycle results

| Scenario | Result |
|---|---|
| Launch | Child PID recorded; stdio open |
| `initialize` → `initialized` | Success; capabilities advertised (see matrix) |
| Graceful `shutdown` + `exit` | Process exit code **0** |
| Forced `Kill(entireProcessTree: true)` | Process exit code **137**; client marks exited |
| Restart across generations 10–12 | Prior generation shut down/killed before next launch; **no leaked PIDs**; generation-scoped notification handler rejected foreign generation callbacks (`staleCallbacks=0`) |
| Cancellation | In-flight `workspace/symbol` cancelled via `CancellationToken` and `$/cancelRequest` |

### Capability support matrix (`csharp-ls` 0.25.0)

| Capability | Advertised | Exercised | Notes |
|---|---|---|---|
| `textDocumentSync` | open/close, **change=2** (incremental), save includeText | didOpen, full-document didChange, didClose | Full-document change payload accepted |
| Diagnostics (`publishDiagnostics`) | via sync | yes | CS1002 (`; expected`) and related on fixture |
| Completion | `completionProvider` + trigger `.` / `'` + resolve | yes | ≈523 items at `Greet` call site |
| Hover | `hoverProvider: true` | yes | Markdown/signature + summary |
| Definition | `definitionProvider: true` | yes | Single location to `Greet` method |
| Document symbols | `documentSymbolProvider: true` | yes | ≈7 symbols |
| Workspace symbols | `workspaceSymbolProvider: true` | yes | query `Greet` → ≥1 |
| Document formatting | `documentFormattingProvider: true` | yes | Request succeeded; 0 edits on tidy fixture |
| Position encoding | not returned in `InitializeResult` | client default | **Locked: utf-16** (LSP default when server omits) |

## 10. Position Encoding (locked before M1)

| Item | Finding |
|---|---|
| Client advertised | `positionEncodings: ["utf-16", "utf-8"]` |
| Server selected | **Not reported** in `InitializeResult` → **LSP default `utf-16`** |
| Locked encoding for M1+ | **`utf-16`** |
| Non-BMP (🎉) | 2 UTF-16 code units; fixture offset mapped to LSP `character` in UTF-16 units |
| CJK (中) | 1 UTF-16 code unit |
| AvaloniaEdit storage | .NET `string` / UTF-16 code units; `TextDocument.TextLength == string.Length` |
| `GetLocation(offset)` | 1-based line/column in **UTF-16 code units** |
| `GetOffset(line, column)` | Inverse of `GetLocation`; round-trips for 中 / 🎉 / BMP |

### Conversion function (locked)

When negotiated encoding is `utf-16` (the locked default):

```text
LSP (line0, character0)  ->  AvaloniaEdit offset
  offset = document.GetOffset(line0 + 1, character0 + 1)

AvaloniaEdit offset -> LSP (line0, character0)
  loc = document.GetLocation(offset)
  line0 = loc.Line - 1
  character0 = loc.Column - 1
```

If a future server forces `utf-8` or `utf-32`, M1+ must insert an explicit
transcoding step before `GetOffset` / after `GetLocation`. That path is **not**
needed for `csharp-ls` 0.25.0.

Proof code: `AvaloniaEditProof.ProvePositionEncoding()` and
`LspUtf16PositionToOffset` / `OffsetToLspUtf16` in the proof project.

## 11. `.slnx` Compatibility

| Question | Answer |
|---|---|
| Does `csharp-ls` accept `.slnx`? | **Yes, as a discovery input.** Server logs reference preferred `.sln/.slnx` discovery under the workspace folder. |
| Parent-directory workspace fallback acceptable? | **Yes.** Workspace folder = parent of `Fixture.slnx`; diagnostics and language features still work. This matches the locked Phase 10 initialize contract. |
| Amend Phase 8.3 eligibility (`Solution` / `SolutionX` / `CSharpProject`)? | **No amendment required.** All three `ProjectKind` values remain eligible. The language service always passes the winning candidate path and initializes with its parent directory as the sole workspace folder. |

## 12. M6 Formatting Prerequisites — Proof Results

### Whole-document undo group

Headless `TextDocument` proof:

1. `UndoStack.StartUndoGroup()`
2. `Document.Text = formatted`
3. `UndoStack.EndUndoGroup()`
4. Single `Undo()` restores the pre-format text entirely
5. Multiple assignments inside one group still collapse to **one** undo step

**Locked M6 apply rule:** wrap whole-document (or multi-`TextEdit`) application
in `StartUndoGroup` / `EndUndoGroup` so Format Document is one user-visible undo.

### Caret / selection mapping after full-document replace

`TextDocument` does not own caret; `TextEditor` does. Locked host rule after
`Document.Text = formatted`:

1. Capture pre-replace `TextLocation` via `GetLocation(caretOffset)`.
2. Apply text under an undo group.
3. Map caret: if the old line still exists, place caret at
   `GetOffset(oldLine, clamp(oldColumn, 1, line.Length + 1))`; else clamp to
   `[0, TextLength]`.
4. **Clear selection** (`SelectionLength = 0`; selection start = mapped caret).

Proof code: `AvaloniaEditProof.ProveCaretSelectionAfterFullReplace()` and
`MapCaretAfterFullReplace`.

### Format-on-Save schema / execution

Unchanged from §4 (schema lock only — **no production code at M0**):

- `EditorSettings.FormatOnSave` / `formatOnSave`, default `false`
- `SchemaVersion` 1 → 2 migration via `ISettingsMigration`
- `SettingsSerializer` ceiling 1 → 2 at M6
- Production migration registration at M6
- UI deferred to M6
- Execution: format-before-write; failure still saves; no re-trigger;
  coordination from `EditorViewModel.SaveAsync` via a formatting service

## 13. Contracts Locked for M1 (no implementation)

### URI / document version rules

- Document URI = `file://` absolute URI of the on-disk path (`Uri.AbsoluteUri`).
- Unsaved buffers without a stable file path are **out of scope** for M1.
- `didOpen` version starts at `1`; each `didChange` increments monotonically per URI.
- Full-document `contentChanges: [{ text }]` is acceptable; incremental changes
  may be used later if desired (`textDocumentSync.change = 2`).

### Session generation / stale-result rejection

- Increment session generation on: context replacement, reload, explicit
  restart, process exit, disposal.
- Every notification handler and response consumer captures the generation at
  send/subscribe time and **discards** work when `callbackGeneration != current`.
- Results also require matching document URI + document version before UI
  projection.

### Request cancellation + document-close semantics

- Every language request accepts `CancellationToken`.
- Cancel sends `$/cancelRequest` with the JSON-RPC id (best effort) and
  completes the local task as cancelled.
- `didClose` removes local diagnostics/completion/hover state for that URI and
  cancels in-flight requests for that URI.

### Proposed M1+ service contracts (names are planning guidance)

| Contract | Responsibility |
|---|---|
| `ILanguageSessionService` | Start/stop/restart session from `IProjectContextService` snapshots; generation; process lifecycle; structured ready/loading/unavailable/failed state |
| `ILanguageDocumentBridge` (M2) | didOpen/didChange/didClose from Workspace/tabs |
| `ILanguageDiagnosticsService` (M3) | Per-URI diagnostics snapshots |
| `ILanguageCompletionService` / `ILanguageHoverService` (M4) | Active-document requests |
| `ILanguageNavigationService` / `ILanguageSymbolService` (M5) | Definition + symbols |
| `ILanguageFormattingService` (M6) | `textDocument/formatting` + Format-on-Save coordination API |

Views/ViewModels must **not** own protocol (consistent with §2).

### Command inventory (register via `ICommandRegistry` in later milestones)

| Command id | Milestone | Default gesture |
|---|---|---|
| `editor.showHover` (or projection-only hover) | M4 | hover timer / key as designed in M4 |
| `editor.gotoDefinition` | M5 | existing navigation gesture policy in M5 |
| `editor.documentSymbol` / `workbench.symbol` (exact ids in M5) | M5 | palette + keybinding |
| `editor.formatDocument` | M6 | `Ctrl+Shift+I` |

Exact command id strings are finalized in the milestone that registers them;
M0 only locks that registration goes through `ICommandRegistry`.

### Focused test list for M1+

| Milestone | Focused tests |
|---|---|
| M1 | `LanguageSessionServiceTests` + DI resolve tests |
| M2 | `LanguageDocumentSyncTests` |
| M3 | `LanguageDiagnosticsServiceTests`, `ProblemsViewModelTests` |
| M4 | `LanguageCompletionTests`, `LanguageHoverTests` |
| M5 | `LanguageNavigationTests`, `LanguageSymbolTests` |
| M6 | `LanguageFormattingTests`, `FormatOnSaveTests` |
| M7 | Full Phase 10 suite + sequential green + smoke evidence |

### DI notes for M1

- Register language services in the app host only at M1+.
- M0 adds **no** DI registrations.
- Proof packages are **not** added to `src/Zaide.csproj` at M0; M1 will add
  `StreamJsonRpc` (and only then) via `Directory.Packages.props` when production
  code needs it. Catalog entry is present in `docs/LIBRARIES.md` as planned.

## 14. Limitations Recorded at M0 Close

- Unsaved / untitled C# buffers: not proven; out of scope until a later explicit
  decision.
- Formatting returned zero edits on the tidy fixture; capability is advertised
  and the request succeeds — M6 must still test non-empty `TextEdit` application.
- `csharp-ls` must be available on the PATH (or configured path) on developer
  machines; production acquisition/distribution for end users is an M1+ concern
  (document install or ship strategy then).
- StreamJsonRpc currently pulls MessagePack with known NuGet audit advisories;
  the isolated proof suppresses NU190x. M1 should pin a clean transitive set or
  justify NoWarn narrowly.

## 15. Exact Next Step

**M1 — Language session service (UI-independent)**
Add the session service and DI wiring: consume only `IProjectContextService`,
own `csharp-ls` process + StreamJsonRpc (or equivalent Content-Length) transport,
initialize with parent-directory workspace folder, generation-safe teardown,
cancellation, restart, and structured non-ready states for ineligible project
context. No editor UI, Problems, or completion popup.
