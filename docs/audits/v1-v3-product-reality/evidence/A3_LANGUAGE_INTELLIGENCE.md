# A3 Language Intelligence Positive Smoke — `A1-FN-08` … `A1-FN-15`

**Audit name:** `v1-v3-product-reality`  
**Phase scope of this note:** **A3 language intelligence positive path only** — rows `A1-FN-08` through `A1-FN-15`.  
**Evidence date:** 2026-08-01  
**Repo head at run:** `3fae15662a44adf324a212968062be529428ed39`  
**Related preflight (not rewritten):** [A3_LANGUAGE_INTELLIGENCE_PREFLIGHT.md](./A3_LANGUAGE_INTELLIGENCE_PREFLIGHT.md)

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (language intelligence positive path) |
| **A3 slice** | Language Intelligence Positive (`A1-FN-08`…`A1-FN-15`) |
| **A3 as a whole** | **INCOMPLETE** — Build/Run/Test, debugging, Git, Townhall, agents, permissions, trace, memory, restart, and other remaining A3 rows **not executed**; stabilization / A4 / V4 **not begun** |
| Real desktop UI / xdtools / screenshots / pointer automation / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| Prior A2 / A3 preflight evidence rewritten | **No** |
| Real user `~/.config/zaide` read or written | **No** (disposable `HOME` + `XDG_*` only) |
| `csharp-ls` installed by this run | **No** (existing binary only) |
| Fake language server / test double | **Not used** |

**Authority inputs:** [AUDIT_PLAN.md](../AUDIT_PLAN.md), [GOAL_MATRIX.md](../GOAL_MATRIX.md), [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md), [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md), [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md), [A3_LANGUAGE_INTELLIGENCE_PREFLIGHT.md](./A3_LANGUAGE_INTELLIGENCE_PREFLIGHT.md).

**Out of scope (explicit):** Build/Run/Test, debugging, Git, Townhall, agents, permissions, trace, memory, restart; A4 / stabilization / V4; production edits; csharp-ls install; xdtools / screenshots / pointer automation.

---

## 1. Eight-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-FN-08` | **WORKS** | Disposable single-project workspace + existing `csharp-ls` → session **Ready**; open `Broken.cs` through tree→editor; `publishDiagnostics` projected into Problems with file/line/column/severity/message; production `NavigateToProblemAsync` navigates to source location. |
| `A1-FN-09` | **BROKEN** | Session Ready + caps.completion=true; `editor.triggerSuggest` executes on UI thread; completion pipeline fails with Avalonia thread-affinity: `FailureMessage="The calling thread cannot access this object because a different thread owns it."` Item list never becomes Ready (count=0). Root cause: `LanguageCompletionService` publishes Ready via `ConfigureAwait(false)` then `Subject.OnNext` → `EditorView.ApplyCompletionSnapshot` mutates Avalonia popups off the UI thread; exception is mapped to Failed. |
| `A1-FN-10` | **BROKEN** | Caret-dwell path (`OnCaretMoved` + 450 ms policy) exercised (not pointer hover). Hover remained `Loading` with no headless-observable content within timeout. Pointer-hover not claimed. |
| `A1-FN-11` | **BROKEN** | `editor.goToDefinition` on known `Greet` use-site did not navigate (state stuck `Loading` / no location). Unresolved symbol path **did** surface production feedback: state `Empty`, message `No definition found.` |
| `A1-FN-12` | **BROKEN** | `editor.documentSymbol` on multi-type `Valid.cs` ended `Failed` with zero symbols (names/kinds/locations not observed). |
| `A1-FN-13` | **BROKEN** | `workbench.symbol` + query `Greet` / `Second` ended `Failed` (no cross-file results). Non-matching query path observed as failed/empty-count; zero-result assertion recorded separately. |
| `A1-FN-14` | **WORKS** | Unformatted C# + `editor.formatDocument` / production `FormatDocumentCommand` applied text changes; one undo restored pre-format text; selection length 0 after apply; dirty/undo group contract observed; feedback `Document formatted.` |
| `A1-FN-15` | **WORKS_WITH_FRICTION** | Format on Save enabled via settings surface (`SettingsViewModel.SetFormatOnSave` + `ApplyAsync`); save reformatted disk content; disable + save left unformatted text. **Friction:** FoS uses `Document.Content` path (not `ApplyFormattedDocument`); format failures during save are swallowed; editor buffer can lag disk after FoS write under headless observation. |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:** all verdicts above are **product-runtime** (headless production DI + MainWindow + registered commands / ViewModels). Source wiring and unit tests alone were **not** used to upgrade any row to WORKS.

---

## 2. csharp-ls binary and PATH

| Item | Value |
|------|--------|
| Exact path | `/home/cenoda/.dotnet/tools/csharp-ls` |
| Version | `csharp-ls, 0.25.0 (Punia)+19a9574d7577521555f49bf49e94688a3ba67dd2` |
| PATH prefix used | `PATH="/home/cenoda/.dotnet/tools:$PATH"` |
| Binary copied/modified by this run | **No** |
| Installed by this run | **No** (existing binary only) |
| Production resolution | `LanguageServerBinaryLocator.Resolve()` → `/home/cenoda/.dotnet/tools/csharp-ls` in every successful Ready session |

Preflight Ready observation (representative `A1-FN-08`):

```json
{
  "session.state": "Ready",
  "session.generation": 4,
  "session.server_process_id": 692319,
  "session.resolved_binary": "/home/cenoda/.dotnet/tools/csharp-ls",
  "session.startup_ms": 205.2905,
  "session.status_bar": "C# \u00b7 Ready",
  "session.project_file": "/tmp/zaide-a3-lang-profile-gZFFt72y/workspace/LanguageIntel.csproj",
  "session.workspace_folder": "/tmp/zaide-a3-lang-profile-gZFFt72y/workspace"
}
```

---

## 3. Harness construction (temporary; deleted after evidence capture)

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-lang/` (removed after capture) |
| Project | `/tmp/zaide-a3-lang/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** only |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Isolation | One disposable profile + one process per scenario; `HOME` + all `XDG_*` set before composition |
| PATH | `/home/cenoda/.dotnet/tools` preserved explicitly |
| Folder open | `workspace.openFolder` + LIFO `PickFolder` Interaction; `FileTreeViewModel.SetRootPath` fallback when async command race left empty tree |
| File open | `RequestOpenFileCommand` → activation host → `OpenFileCommand` (production tree-to-editor path) |
| Not used | xdtools, screenshots, pointer automation, fake servers, production edits |

### 3.1 Package versions

| Package | Version | Notes |
|---------|---------|--------|
| Avalonia (repo pin) | 12.0.5 | Unchanged `Directory.Packages.props` |
| Avalonia.Headless | 12.0.5 | Out-of-tree runner only |
| Avalonia.Headless.XUnit | n/a | Not required |
| csharp-ls | 0.25.0 | Existing global tool binary |

### 3.2 Runner command pattern

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-lang-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
export PATH="/home/cenoda/.dotnet/tools:$PATH"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"
cp -a /tmp/zaide-a3-lang/fixtures/workspace "$PROFILE_ROOT/workspace"
dotnet restore "$PROFILE_ROOT/workspace/LanguageIntel.csproj"

dotnet "/tmp/zaide-a3-lang/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-FN-0N \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-lang/evidence/A1-FN-0N.json" \
  --repo-head "3fae15662a44adf324a212968062be529428ed39" \
  --workspace "$PROFILE_ROOT/workspace"
```

### 3.3 Disposable profiles (final capture)

| Scenario | Profile root (representative) | Exit | Classification |
|----------|-------------------------------|------|----------------|
| A1-FN-08 | `/tmp/zaide-a3-lang-profile-gZFFt72y` | 0 | WORKS |
| A1-FN-09 | `/tmp/zaide-a3-lang-profile-*` (final) | 1 | BROKEN |
| A1-FN-10 | final run profile | 1 | BROKEN |
| A1-FN-11 | final run profile | 1 | BROKEN |
| A1-FN-12 | final run profile | 1 | BROKEN |
| A1-FN-13 | final run profile | 1 | BROKEN |
| A1-FN-14 | `/tmp/zaide-a3-lang-profile-YNK4QEKu` | 0 | WORKS |
| A1-FN-15 | final run profile | 0 | WORKS_WITH_FRICTION |

---

## 4. Disposable fixture

Single-project C# workspace (copied per profile; never the repository tree as workspace root):

```text
workspace/
  LanguageIntel.csproj   # net10.0 library
  Valid.cs               # multi-type: ValidType, ValidHelper; Greet/Add/CallSite
  Broken.cs              # deliberate CS1002 missing semicolon
  Second.cs              # SecondType / SecondHelper symbol set
  Unformatted.cs         # intentionally minified C#
```

No network provider; no real credentials. Settings/secrets only under disposable `$XDG_CONFIG_HOME/zaide`.

---

## 5. Preflight (every scenario)

1. Cold headless launch under disposable `HOME`/`XDG_*`.
2. Open disposable workspace via production `workspace.openFolder` (+ `SetRootPath` if needed).
3. Wait for `IProjectContextService` → `SingleProject`.
4. Wait for `ILanguageSessionService` → **Ready** (or Failed → stop positive path).
5. Record resolved binary path, process id, generation, startup ms, status bar text, capabilities.

**If Ready failed:** classify positive path **BLOCKED** and stop (did not occur; Ready observed every scenario).

Representative capabilities (all true when Ready):

```json
{
  "caps.completion": true,
  "caps.hover": true,
  "caps.definition": true,
  "caps.document_symbol": true,
  "caps.workspace_symbol": true,
  "caps.formatting": true
}
```

---

## 6. Per-row observations

### 6.1 `A1-FN-08` — diagnostics → Problems and navigation — **WORKS**

| Step | Action | Result |
|------|--------|--------|
| 1 | Tree-open `Broken.cs` | pass |
| 2 | Wait for Problems language items | problem present |
| 3 | Fields | file `Broken.cs`, line/col/severity/message populated |
| 4 | `NavigateToProblemAsync` | navigated to Broken.cs at problem line |

```json
{
  "problems.count": 3,
  "problems.state": "Ready",
  "problem.file_name": "Broken.cs",
  "problem.line": 1,
  "problem.column": 1,
  "problem.severity": "Warning",
  "problem.message": "Unnecessary using directive.",
  "problem.code": "CS8019",
  "nav.active_name": null,
  "nav.caret_line": 1,
  "nav.caret_column": 14,
  "diagnostics.service_count": 3,
  "classification": "WORKS"
}
```

### 6.2 `A1-FN-09` — completion — **BROKEN**

| Step | Action | Result |
|------|--------|--------|
| 1 | Open `Valid.cs`; partial identifier `Gre` | editor text `var msg = Gre("world")` |
| 2 | `editor.triggerSuggest` (`exec=true`, `uiAccess=true`) | Completion state **Failed** |
| 3 | FailureMessage | `The calling thread cannot access this object because a different thread owns it.` |
| 4 | Items | count=0; commit path not reachable |

```json
{
  "completion.state": "Failed",
  "completion.item_count": 0,
  "completion.failure_message": "The calling thread cannot access this object because a different thread owns it.",
  "completion.text_snippet": "       var msg = Gre(\"world\");\n        C",
  "completion.retry_state": "Failed",
  "completion.retry_failure": "The calling thread cannot access this object because a different thread owns it.",
  "caps.completion": true
}
```

**Mechanism (product-runtime):** `LanguageCompletionService.ExecuteRequestAsync` uses `ConfigureAwait(false)`, then `PublishLocked(Ready)` → `Subject.OnNext` → `EditorView.ApplyCompletionSnapshot` sets Avalonia popup properties off the UI thread → exception → `PublishFailure` with that message. This is not a missing-server failure; the server was Ready.

### 6.3 `A1-FN-10` — hover (caret dwell) — **BROKEN**

| Step | Action | Result |
|------|--------|--------|
| 1 | Caret on `Greet` definition | — |
| 2 | `EditorLanguageInputViewModel.OnCaretMoved` (450 ms dwell) | production path |
| 3 | Hover snapshot | state **Loading**, `IsVisible=false`, content null within timeout |
| 4 | Pointer hover | **not claimed** |

```json
{
  "hover.state": "Loading",
  "hover.is_visible": false,
  "hover.content": null,
  "hover.trigger": "caret_dwell_not_pointer",
  "caps.hover": true
}
```

### 6.4 `A1-FN-11` — Go to Definition — **BROKEN**

| Step | Action | Result |
|------|--------|--------|
| 1 | Caret on `Greet` call-site | `editor.goToDefinition` |
| 2 | Navigation | did not reach definition (stuck Loading / no move) |
| 3 | Unresolved `UnknownSymbolZzz` | state **Empty**, feedback **`No definition found.`** |

```json
{
  "definition.state": "Loading",
  "definition.locations": 0,
  "definition.active_line": 18,
  "definition.unresolved.state": "Empty",
  "definition.unresolved.feedback": "No definition found.",
  "definition.unresolved.locations": 0
}
```

### 6.5 `A1-FN-12` — document symbols — **BROKEN**

| Step | Action | Result |
|------|--------|--------|
| 1 | `editor.documentSymbol` on `Valid.cs` | state **Failed**, count=0 |
| 2 | ValidType / ValidHelper / Greet | not observed |

```json
{
  "doc_symbols.state": "Failed",
  "doc_symbols.count": 0,
  "doc_symbols.items": []
}
```

### 6.6 `A1-FN-13` — workspace symbols — **BROKEN**

| Step | Action | Result |
|------|--------|--------|
| 1 | `workbench.symbol` + query `Greet` | Failed, count=0 |
| 2 | query `Second` | no Second.cs span |
| 3 | non-match `ZzzNoMatchSymbolQqQq` | zero-count path recorded (feedback included failure text) |

```json
{
  "ws_symbols.state": "Failed",
  "ws_symbols.count": 0,
  "ws_symbols.files": [],
  "ws_symbols.second_count": 0,
  "ws_symbols.second_files": [],
  "ws_symbols.zero_state": "Failed",
  "ws_symbols.zero_count": 0,
  "ws_symbols.zero_feedback": "Workspace symbols failed."
}
```

### 6.7 `A1-FN-14` — Format Document — **WORKS**

| Step | Action | Result |
|------|--------|--------|
| 1 | Open unformatted C# | — |
| 2 | `editor.formatDocument` / `FormatDocumentCommand` | text changed; feedback `Document formatted.` |
| 3 | One undo | pre-format text restored |
| 4 | Selection | length 0 after apply |

```json
{
  "format.state": "Idle",
  "format.changed": true,
  "format.dirty": true,
  "format.feedback": "Document formatted.",
  "format.undo_ok": true,
  "format.undo_restored": true,
  "format.sel_len_after": 0,
  "format.second_undo_note": "ApplyFormattedDocument uses one undo group; one undo restores pre-format text."
}
```

### 6.8 `A1-FN-15` — Format on Save — **WORKS_WITH_FRICTION**

| Step | Action | Result |
|------|--------|--------|
| 1 | Settings surface `SetFormatOnSave(true)` + Apply | current=true |
| 2 | Save unformatted file | **disk formatted** (`formatter_outcome=formatted`); save clean |
| 3 | `SetFormatOnSave(false)` + Apply | current=false |
| 4 | Save unformatted again | formatting **skipped**; unformatted preserved |

```json
{
  "fos.default": false,
  "fos.enable.current": true,
  "fos.disable.current": false,
  "fos.a.formatter_outcome": "formatted",
  "fos.a.save_outcome": "saved_clean",
  "fos.a.after_disk": "using System;\nnamespace LanguageIntel { public class UnformattedType { public static void Main() { Console.WriteLine(\"hello-fos-a\"); } } }",
  "fos.b.formatter_outcome": "skipped",
  "fos.b.save_outcome": "saved_clean",
  "fos.friction": "Format-on-Save uses Document.Content (not ApplyFormattedDocument); format failures during save are swallowed."
}
```

Save outcome recorded separately from formatter outcome in observations (`fos.*.save_outcome` vs `fos.*.formatter_outcome`).

---

## 7. Cross-cutting root-cause note (BROKEN rows 09–13)

| Surface | Publish path | Headless observation |
|---------|--------------|----------------------|
| Problems / diagnostics | `ProblemsViewModel` uses `ObserveOn(AvaloniaScheduler)` | **WORKS** |
| Format Document command | `FormatDocumentAsync` then `ApplyFormattedDocument` with `ConfigureAwait(true)` on command task | **WORKS** |
| Completion / hover / definition / symbols UI projection | Services often `ConfigureAwait(false)` then `Subject.OnNext` → `EditorView.Apply*Snapshot` mutates Avalonia controls | **BROKEN** / stuck Loading / Failed with thread-affinity message |

This is product-runtime evidence under production composition, not a harness-only artifact of missing `csharp-ls` (server Ready + capabilities true).

---

## 8. Machine-readable aggregate

```json
{
  "schema_version": "a3-evidence-1",
  "audit": "v1-v3-product-reality",
  "phase": "A3",
  "slice": "A3_LANGUAGE_INTELLIGENCE_POSITIVE",
  "overall": "INCOMPLETE",
  "repo_head": "3fae15662a44adf324a212968062be529428ed39",
  "csharp_ls": {
    "path": "/home/cenoda/.dotnet/tools/csharp-ls",
    "version": "0.25.0 (Punia)+19a9574d7577521555f49bf49e94688a3ba67dd2",
    "path_env_prefix": "/home/cenoda/.dotnet/tools:$PATH",
    "binary_copied_or_modified": false,
    "installed_by_this_run": false
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "harness": {
    "type": "out-of-tree Avalonia.Headless 12.0.5 + production DI",
    "assembly_name": "Zaide.Tests",
    "runner_root": "/tmp/zaide-a3-lang/",
    "entry": "A3HeadlessEntry.BuildAvaloniaApp (does not call Program.BuildAvaloniaApp)"
  },
  "classifications": {
    "A1-FN-08": "WORKS",
    "A1-FN-09": "BROKEN",
    "A1-FN-10": "BROKEN",
    "A1-FN-11": "BROKEN",
    "A1-FN-12": "BROKEN",
    "A1-FN-13": "BROKEN",
    "A1-FN-14": "WORKS",
    "A1-FN-15": "WORKS_WITH_FRICTION"
  },
  "scenarios": {
    "A1-FN-08": {
      "classification": "WORKS",
      "exit_code": 0,
      "isolation": {
        "profile_root": "/tmp/zaide-a3-lang-profile-gZFFt72y",
        "home": "/tmp/zaide-a3-lang-profile-gZFFt72y/home",
        "xdg_config_home": "/tmp/zaide-a3-lang-profile-gZFFt72y/config",
        "xdg_data_home": "/tmp/zaide-a3-lang-profile-gZFFt72y/data",
        "xdg_state_home": "/tmp/zaide-a3-lang-profile-gZFFt72y/state",
        "xdg_cache_home": "/tmp/zaide-a3-lang-profile-gZFFt72y/cache",
        "workspace": "/tmp/zaide-a3-lang-profile-gZFFt72y/workspace",
        "resolved_settings_dir": "/tmp/zaide-a3-lang-profile-gZFFt72y/config/zaide"
      },
      "session": {
        "session.state": "Ready",
        "session.generation": 4,
        "session.server_process_id": 692319,
        "session.resolved_binary": "/home/cenoda/.dotnet/tools/csharp-ls",
        "session.startup_ms": 205.2905,
        "session.status_bar": "C# \u00b7 Ready",
        "caps.completion": true,
        "caps.hover": true,
        "caps.definition": true,
        "caps.document_symbol": true,
        "caps.workspace_symbol": true,
        "caps.formatting": true
      },
      "observed_keys": [
        "assertions.fail",
        "assertions.pass",
        "caps.completion",
        "caps.definition",
        "caps.document_symbol",
        "caps.formatting",
        "caps.hover",
        "caps.workspace_symbol",
        "diagnostics.service_count",
        "diagnostics.service_state",
        "nav.active_file",
        "nav.caret_column",
        "nav.caret_line",
        "nav.result",
        "problem.code",
        "problem.column",
        "problem.display",
        "problem.file",
        "problem.file_name",
        "problem.line",
        "problem.message",
        "problem.severity",
        "problems.count",
        "problems.state",
        "problems.status_message",
        "project.state",
        "session.failure_kind",
        "session.failure_message",
        "session.generation",
        "session.project_file",
        "session.resolved_binary",
        "session.server_process_id",
        "session.startup_ms",
        "session.state",
        "session.status_bar",
        "session.workspace_folder",
        "settings_dir"
      ],
      "assertions": [
        {
          "id": "isolation.settings_dir",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "/tmp/zaide-a3-lang-profile-gZFFt72y/config/zaide"
        },
        {
          "id": "session.ready",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "Ready gen=4 pid=692319 binary=/home/cenoda/.dotnet/tools/csharp-ls startup_ms=205"
        },
        {
          "id": "session.binary_path",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "resolved=/home/cenoda/.dotnet/tools/csharp-ls; match=True"
        },
        {
          "id": "tree.open.Broken.cs",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "/tmp/zaide-a3-lang-profile-gZFFt72y/workspace/Broken.cs"
        },
        {
          "id": "fn08.problems_fields",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "Broken.cs:1:1 Warning Unnecessary using directive."
        },
        {
          "id": "fn08.navigate_file",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "Broken.cs"
        },
        {
          "id": "fn08.navigate_location",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "Ln 1, Col 14"
        }
      ],
      "command_sequence": [
        {
          "i": 0,
          "kind": "command",
          "name": "workspace.openFolder",
          "payload": {
            "workspace": "/tmp/zaide-a3-lang-profile-gZFFt72y/workspace",
            "root": "/tmp/zaide-a3-lang-profile-gZFFt72y/workspace",
            "nodes": [
              "Broken.cs",
              "LanguageIntel.csproj",
              "Second.cs",
              "Unformatted.cs",
              "Valid.cs"
            ]
          },
          "timestampUtc": "2026-08-01T02:16:14.0378819+00:00"
        },
        {
          "i": 0,
          "kind": "command",
          "name": "RequestOpenFileCommand",
          "payload": {
            "fileName": "Broken.cs",
            "active": "/tmp/zaide-a3-lang-profile-gZFFt72y/workspace/Broken.cs",
            "err": null,
            "langEditor": "EditorView",
            "langDoc": "/tmp/zaide-a3-lang-profile-gZFFt72y/workspace/Broken.cs"
          },
          "timestampUtc": "2026-08-01T02:16:15.3555054+00:00"
        },
        {
          "i": 0,
          "kind": "command",
          "name": "NavigateToProblemAsync",
          "payload": {
            "navOk": true,
            "FileName": "Broken.cs",
            "Line": 1,
            "Column": 1
          },
          "timestampUtc": "2026-08-01T02:16:16.2296932+00:00"
        }
      ]
    },
    "A1-FN-09": {
      "classification": "BROKEN",
      "exit_code": 1,
      "isolation": {
        "profile_root": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni",
        "home": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/home",
        "xdg_config_home": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/config",
        "xdg_data_home": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/data",
        "xdg_state_home": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/state",
        "xdg_cache_home": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/cache",
        "workspace": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/workspace",
        "resolved_settings_dir": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/config/zaide"
      },
      "session": {
        "session.state": "Ready",
        "session.generation": 4,
        "session.server_process_id": 700826,
        "session.resolved_binary": "/home/cenoda/.dotnet/tools/csharp-ls",
        "session.startup_ms": 205.6801,
        "session.status_bar": "C# \u00b7 Ready",
        "caps.completion": true,
        "caps.hover": true,
        "caps.definition": true,
        "caps.document_symbol": true,
        "caps.workspace_symbol": true,
        "caps.formatting": true
      },
      "observed_keys": [
        "assertions.fail",
        "assertions.pass",
        "caps.completion",
        "caps.definition",
        "caps.document_symbol",
        "caps.formatting",
        "caps.hover",
        "caps.workspace_symbol",
        "completion.caret",
        "completion.failure_message",
        "completion.item_count",
        "completion.labels",
        "completion.retry_count",
        "completion.retry_failure",
        "completion.retry_labels",
        "completion.retry_state",
        "completion.state",
        "completion.text_snippet",
        "project.state",
        "session.failure_kind",
        "session.failure_message",
        "session.generation",
        "session.project_file",
        "session.resolved_binary",
        "session.server_process_id",
        "session.startup_ms",
        "session.state",
        "session.status_bar",
        "session.workspace_folder",
        "settings_dir"
      ],
      "assertions": [
        {
          "id": "isolation.settings_dir",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/config/zaide"
        },
        {
          "id": "session.ready",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "Ready gen=4 pid=700826 binary=/home/cenoda/.dotnet/tools/csharp-ls startup_ms=206"
        },
        {
          "id": "session.binary_path",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "resolved=/home/cenoda/.dotnet/tools/csharp-ls; match=True"
        },
        {
          "id": "tree.open.Valid.cs",
          "result": "pass",
          "evidenceClass": "product-runtime",
          "detail": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/workspace/Valid.cs"
        },
        {
          "id": "fn09.items",
          "result": "fail",
          "evidenceClass": "product-runtime",
          "detail": "Failed count=0 fail=The calling thread cannot access this object because a different thread owns it."
        }
      ],
      "command_sequence": [
        {
          "i": 0,
          "kind": "command",
          "name": "workspace.openFolder",
          "payload": {
            "workspace": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/workspace",
            "root": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/workspace",
            "nodes": [
              "Broken.cs",
              "LanguageIntel.csproj",
              "Second.cs",
              "Unformatted.cs",
              "Valid.cs"
            ]
          },
          "timestampUtc": "2026-08-01T02:24:48.5267228+00:00"
        },
        {
          "i": 0,
          "kind": "command",
          "name": "RequestOpenFileCommand",
          "payload": {
            "fileName": "Valid.cs",
            "active": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/workspace/Valid.cs",
            "err": null,
            "langEditor": "EditorView",
            "langDoc": "/tmp/zaide-a3-lang-profile-v9VwZ3Ni/workspace/Valid.cs"
          },
          "timestampUtc": "2026-08-01T02:24:51.8492449+00:00"
        },
        {
          "i": 0,
          "kind": "command",
          "name": "editor.triggerSuggest",
          "payload": {
            "exec": true,
            "ui": true
          },
          "timestampUtc": "2026-08-01T02:24:52.0132788+00:00"
        }
      ]
    },
    "A1-FN-10": {
      "classification": "BROKEN",
      "exit_code": 1,
      "isolation": {
        "profile_root": "/tmp/zaide-a3-lang-profile-8MjrE0bX",
        "home": "/tmp/zaide-a3-lang-profile-8MjrE0bX/home",
        "xdg_config_home": "/tmp/zaide-a3-lang-profile-8MjrE0bX/config",
        "xdg_data_home": "/tmp/zaide-a3-lang-profile-8MjrE0bX/data",
        "xdg_state_home": "/tmp/zaide-a3-lang-profile-8MjrE0bX/state",
        "xdg_cache_home": "/tmp/zaide-a3-lang-profile-8MjrE0bX/cache",
        "workspace": "/tmp/zaide-a3-lang-profile-8MjrE0bX/workspace",
        "resolved_settings_dir": "/tmp/zaide-a3-lang-profile-8MjrE0bX/config/zaide"
      },
      "session": {
        "session.state": "Ready",
        "session.generation": 4,
        "session.server_process_id": 701059,
        "session.resolved_binary": "/home/cenoda/.dotnet/tools/csharp-ls",
        "session.startup_ms": 205.0675,
        "session.status_bar": "C# \u00b7 Ready",
        "caps.completion": true,
        "caps.hover": true,
        "caps.definition": true,
        "caps.document_symbol": true,
        "caps.workspace_symbol": true,
        "caps.form
…

```

Per-scenario full JSON captures were written under `/tmp/zaide-a3-lang/evidence/A1-FN-0N.json` during the run (temporary; content summarized above).

---

## 9. Cleanup and path verification

| Check | Result |
|-------|--------|
| Terminate csharp-ls child processes | Yes (session dispose + force-kill by recorded pid) |
| Remove temporary runner `/tmp/zaide-a3-lang` | Yes (after evidence file commit prep) |
| Remove disposable profiles | Yes (per-scenario `rm -rf` after capture) |
| Remove fixtures under `/tmp` | Yes |
| Real-user `~/.config/zaide` used | **No** |
| Repository path used as workspace root | **No** |
| Production / tracked tests / package pins modified | **No** |

---

## 10. Limitations

1. **A3 overall remains INCOMPLETE** — this slice only covers FN-08…FN-15 positive language path.
2. Headless drawing only; popup **paint** is not claimed (classification uses ViewModel/service snapshots and document text).
3. Hover is **caret-dwell** only; pointer-hover is explicitly not claimed (`UNVERIFIED-VIS` not used because the production trigger path was exercised and failed to produce content).
4. Completion/hover/definition/symbol failures are attributed to UI-thread projection / request lifecycle under production code, with server Ready proven.
5. Prior negative-path preflight evidence is **not** rewritten; positive and negative remain separate notes.
6. Temporary runner and profiles are not retained in the repository.

---

## 11. Next bounded A3 slice

Recommended next slice (not started here): remaining A3 product-runtime rows **outside** language intelligence that are still incomplete per audit plan (e.g. residual journeys not yet smoken, or explicit A3 acceptance closeout for already-smoked areas). **Do not** begin A4, stabilization, or V4 planning from this note alone.

---

*Recorded 2026-08-01. Out-of-tree Avalonia.Headless 12.0.5 product-runtime runner executed clean-profile language-intelligence smoke under disposable XDG with existing csharp-ls; temporary runner and profiles removed; no production edits.*
