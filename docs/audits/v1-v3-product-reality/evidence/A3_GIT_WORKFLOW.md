# A3 Clean-Profile Smoke — Git Workflow (`A1-GT-01` … `A1-GT-04`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 Git workflow execution slice only** — rows
`A1-GT-01` through `A1-GT-04`.
**Evidence date:** 2026-08-01
**Repo head at run:** `9b343012ac9b06ebb38443a3a9d65fe22f7d3021`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (GT-01…GT-04 only) |
| **A3 slice** | Git workflow (`A1-GT-01`…`A1-GT-04`) |
| **A3 as a whole** | **Incomplete** — Townhall, agents, permissions, trace, memory, restart-recovery, residual journeys **not executed** in this note |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / desktop pointer automation / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| Prior A2 / A3 evidence rewritten | **No** |
| Real user `~/.config/zaide` read or written by this run | **No** (disposable `HOME` + `XDG_*` only) |
| Real user Git config / profile used as product identity | **No** (disposable `HOME` + empty `GIT_CONFIG_GLOBAL` / `GIT_CONFIG_SYSTEM`; fixture-local identity only) |
| Zaide repository itself opened as workspace | **No** (disposable `/tmp` Git fixtures only) |
| Push / pull / merge / rebase exercised | **No** (out of scope) |
| Registry unit tests used as A3 proof | **No** (explicitly forbidden) |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_GIT.md](./A2_GIT.md)

**Out of scope for this slice (explicit):**

- Push, pull, merge, rebase, stash, remote creation, PR workflows
- Townhall, agents, permissions, trace, memory, restart-recovery
- A4, stabilization, V4 planning
- Production code, tracked tests, package pins, audit policy edits
- Unit tests as A3 proof
- Visual paint of Source Control panel, status bar iconography, or diff monospace styling (**UNVERIFIED-VIS** where noted)

---

## 1. Four-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-GT-01` | **WORKS** | Cold start shows `NotARepository` / `"no repo"`. Opening a non-repo disposable folder keeps empty change lists, panel `StatusMessage` (“No repository — open a folder inside a git repository”), and status bar `BranchText="no repo"`. Opening a disposable Git repo yields `LastRefreshStatus=Success`, branch `master` matching `git branch --show-current`, status bar parity, and truthful unstaged set (`readme.txt`/`beta.txt` Modified, `image.bin` Modified, `gamma-new.txt` Added; staged empty). Explicit `RefreshCommand` keeps Success + branch stable. |
| `A1-GT-02` | **WORKS** | `SelectFileCommand` on modified text opens a read-only Source Control diff tab (`IsReadOnly=true`, `IsSourceControlDiff=true`, key `readme.txt`, comparison `"Changes"`) with unified diff content (`diff --git` / `@@` / actual line change). Binary `image.bin` opens read-only diff tab with exact notice `"Binary file — diff not available"`. Selection path `readme.txt` preserved across `RefreshCommand`; open diff tab remains read-only. |
| `A1-GT-03` | **WORKS** | Empty/whitespace commit message → `CommitError="Commit message cannot be empty."` (no mutation). Nothing staged → `CommitError="Nothing staged to commit."`. Stage one file moves `a.txt` to staged (CLI `git diff --cached` confirms). Unstage restores unstaged. Stage-all stages `a.txt`/`b.txt`/`c.txt`. Local commit with fixture-local identity succeeds: HEAD advances, log subject matches, commit message cleared, lists empty, post-mutation refresh Success. Missing identity on second disposable repo → truthful `CommitError` about configuring `user.name`/`user.email`. |
| `A1-GT-04` | **WORKS** | Attached HEAD: `CurrentBranchName` and status bar `BranchText` are `master` (not SHA); `Branches` marks current. Detached HEAD disposable fixture: CLI symbolic-ref fails, empty branch name; VM and status bar show full 40-char SHA `ef9fe2021aec7df8632a1e974a074167719a613c` matching `git rev-parse HEAD`. |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:**

| Class | Meaning in this slice |
|-------|------------------------|
| `product-runtime` | Production DI + `MainWindow` + Source Control ViewModel / commands / editor diff tabs under headless |
| `cli-truth` | Disposable-repo `git` CLI cross-check under isolated `HOME` / empty global+system git config |
| `visual-only` | Pixel paint of panel rows, branch icon, or diff coloring — **not claimed** (`UNVERIFIED-VIS`) |

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-gt/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-gt/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** only |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Isolation | One disposable profile **per independent scenario process**; `HOME` + all absolute `XDG_*` set **before** production composition |
| Git isolation | Empty `GIT_CONFIG_GLOBAL` + `GIT_CONFIG_SYSTEM` under profile; fixture-local `user.name`/`user.email` only where commits must succeed |
| Folder open | Production `OpenFolderCommand` + LIFO `PickFolder` Interaction → disposable `/tmp` workspace |
| Observation | `SourceControlViewModel`, `StatusBarViewModel.BranchText`, `EditorTabs` read-only diff tabs, CLI git truth |
| Not used | xdtools, OS screenshots, desktop pointer automation, manual UI, service replacements, unit tests as proof |

### 2.1 Isolation protocol

| Variable | Disposable value |
|----------|------------------|
| `HOME` | `$PROFILE_ROOT/home` |
| `XDG_CONFIG_HOME` | `$PROFILE_ROOT/config` (absolute) |
| `XDG_DATA_HOME` | `$PROFILE_ROOT/data` |
| `XDG_STATE_HOME` | `$PROFILE_ROOT/state` |
| `XDG_CACHE_HOME` | `$PROFILE_ROOT/cache` |
| `GIT_CONFIG_GLOBAL` | `$PROFILE_ROOT/empty.gitconfig` (empty file) |
| `GIT_CONFIG_SYSTEM` | `$PROFILE_ROOT/empty-system.gitconfig` (empty file) |

Preflight asserted `SettingsPathResolver.GetSettingsDirectory()` under `$XDG_CONFIG_HOME/zaide` and **not** the real-user `/home/cenoda/.config/zaide`.

Workspace fixtures lived only under `/tmp/zaide-a3-gt/fixtures/` — never the Zaide repository tree as workspace root.

### 2.2 Package versions

| Package | Version | Notes |
|---------|---------|--------|
| Avalonia (repo pin) | 12.0.5 | Unchanged `Directory.Packages.props` |
| Avalonia.Headless | 12.0.5 | Out-of-tree runner only |
| Avalonia.Headless.XUnit | n/a | Not required |
| ReactiveUI.Avalonia.ME.DI (via app) | 12.0.3 | Production DI bootstrap |
| LibGit2Sharp (via app) | production pin | Real discovery / status / diff / mutation seam |

### 2.3 Runner command pattern

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-gt-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
export GIT_CONFIG_GLOBAL="${PROFILE_ROOT}/empty.gitconfig"
export GIT_CONFIG_SYSTEM="${PROFILE_ROOT}/empty-system.gitconfig"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"
: > "$GIT_CONFIG_GLOBAL" "$GIT_CONFIG_SYSTEM"

dotnet "/tmp/zaide-a3-gt/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-GT-0N \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-gt/evidence/A1-GT-0N.json" \
  --repo-head "9b343012ac9b06ebb38443a3a9d65fe22f7d3021" \
  --workspace "/tmp/zaide-a3-gt/fixtures/run/..." \
  [--workspace2 "..."] [--non-repo "..."]
```

### 2.4 Disposable profiles (final capture)

| Scenario | Profile root | Exit | Assertions |
|----------|--------------|------|------------|
| `A1-GT-01` | `/tmp/zaide-a3-gt-profile-fbB2djK8` | **0** | 24/24 pass |
| `A1-GT-02` | `/tmp/zaide-a3-gt-profile-ywyxCdog` | **0** | 20/20 pass |
| `A1-GT-03` | `/tmp/zaide-a3-gt-profile-79ODqBY2` | **0** | 23/23 pass |
| `A1-GT-04` | `/tmp/zaide-a3-gt-profile-7OB5m3mR` | **0** | 10/10 pass |

**Total:** 77 product-runtime assertions, all pass on final capture.

---

## 3. Disposable fixtures

All under `/tmp/zaide-a3-gt/fixtures/` only (copied per scenario under `fixtures/run/`).

### 3.1 Non-repository folder

```text
non-repo/
  notes.txt
```

### 3.2 Attached-branch repo with mixed changes (GT-01/02/04 attached)

```text
repo-attached/   (branch master)
  readme.txt     # Modified vs HEAD
  beta.txt       # Modified vs HEAD
  alpha.txt      # clean
  image.bin      # Modified binary vs HEAD
  gamma-new.txt  # Untracked / Added
```

| Field | Value |
|-------|--------|
| Initial HEAD | `40b128e1399a1d3baf2ab0166beba52845e2fe34` |
| Branch | `master` |
| Fixture-local identity | `A3 Audit <a3-audit@example.invalid>` |

### 3.3 Mutation repo (GT-03 success path)

```text
repo-mutation-template/
  a.txt b.txt c.txt   # all Modified unstaged
```

Local identity configured for successful commit. Base HEAD `83b55ebd535903261b290f9eff7c4cd5413b0de2`.

### 3.4 No-identity repo (GT-03 identity error)

```text
repo-noidentity-template/
  seed.txt   # Modified unstaged; local user.name/user.email unset
```

### 3.5 Detached HEAD repo (GT-04)

```text
repo-detached/
  d.txt
```

| Field | Value |
|-------|--------|
| Detached at | `ef9fe2021aec7df8632a1e974a074167719a613c` (first of two commits) |

---

## 4. `A1-GT-01` — discovery, status, modified files, non-repo truth

### 4.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; clean disposable profile | product-runtime |
| 2 | Observe SC default non-repo state | product-runtime |
| 3 | `OpenFolderCommand` → non-repo path via PickFolder Interaction | product-runtime |
| 4 | CLI `git rev-parse` confirms not a repo | cli-truth |
| 5 | `OpenFolderCommand` → disposable Git repo; switch to Source Control | product-runtime |
| 6 | Compare branch + change lists to `git branch` / `git status --porcelain` | product-runtime + cli-truth |
| 7 | Explicit `RefreshCommand` | product-runtime |

### 4.2 Observed results

| Check | Result |
|-------|--------|
| Cold `LastRefreshStatus` | `NotARepository` |
| Cold / non-repo branch labels | `"no repo"` (VM + status bar) |
| Non-repo `StatusMessage` | `No repository — open a folder inside a git repository` |
| Repo `LastRefreshStatus` | `Success` |
| Branch | `master` (matches CLI) |
| Unstaged | 4 paths: `beta.txt`, `gamma-new.txt`, `image.bin`, `readme.txt` |
| Staged | 0 |
| CLI porcelain | ` M beta.txt` / ` M image.bin` / ` M readme.txt` / `?? gamma-new.txt` |

### 4.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-GT-01",
  "exitCode": 0,
  "isolation": {
    "profileRoot": "/tmp/zaide-a3-gt-profile-fbB2djK8",
    "resolvedSettingsDir": "/tmp/zaide-a3-gt-profile-fbB2djK8/config/zaide",
    "workspace": "/tmp/zaide-a3-gt/fixtures/run/gt01-repo",
    "nonRepo": "/tmp/zaide-a3-gt/fixtures/run/gt01-nonrepo",
    "preflightOk": true
  },
  "observedViewModelState": {
    "nonrepo.CurrentBranchName": "no repo",
    "nonrepo.BranchText": "no repo",
    "nonrepo.LastRefreshStatus": "NotARepository",
    "repo.CurrentBranchName": "master",
    "repo.BranchText": "master",
    "repo.LastRefreshStatus": "Success",
    "repo.UnstagedCount": 4,
    "repo.StagedCount": 0,
    "repo.UnstagedPaths": [
      "beta.txt:Modified:False",
      "gamma-new.txt:Added:False",
      "image.bin:Modified:False",
      "readme.txt:Modified:False"
    ],
    "repo.cli_branch": "master",
    "repo.cli_status": " M beta.txt\n M image.bin\n M readme.txt\n?? gamma-new.txt\n"
  },
  "classificationHint": "WORKS"
}
```

### 4.4 Classification rationale — **WORKS**

All discovery, truthful non-repo labels, live status projection, and CLI parity checks passed under production composition. Visual paint of the Source Control list rows and status-bar Git icon is **UNVERIFIED-VIS** (not required for this functional row).

---

## 5. `A1-GT-02` — select, unified diff, binary notice, read-only tab, selection preservation

### 5.1 Sequence

| Step | Action | Result |
|------|--------|--------|
| 1 | Open disposable attached repo | Success; 4 unstaged |
| 2 | `SelectFileCommand` on `readme.txt` | Diff tab opens |
| 3 | Inspect tab | `IsReadOnly`, `IsSourceControlDiff`, unified patch |
| 4 | `SelectFileCommand` on `image.bin` | Binary notice |
| 5 | Re-select `readme.txt`; `RefreshCommand` | Selection path preserved; tab still read-only |

### 5.2 Machine-readable excerpt

```json
{
  "scenarioId": "A1-GT-02",
  "exitCode": 0,
  "observedViewModelState": {
    "diff.readme.IsReadOnly": true,
    "diff.readme.IsSourceControlDiff": true,
    "diff.readme.SourceControlDiffKey": "readme.txt",
    "diff.readme.DisplayName": "readme.txt — Changes",
    "diff.readme.content_preview": "diff --git a/readme.txt b/readme.txt\nindex 77715a0..aee8df5 100644\n--- a/readme.txt\n+++ b/readme.txt\n@@ -1 +1 @@\n-Hello original\n+Hello modified for A3 GT\n",
    "binary.active.IsReadOnly": true,
    "binary.active.IsSourceControlDiff": true,
    "binary.active.SourceControlDiffKey": "image.bin",
    "binary.active.content": "Binary file — diff not available",
    "selection.path_before_refresh": "readme.txt",
    "selection.path_after_refresh": "readme.txt",
    "tabs.diff_count": 2
  },
  "classificationHint": "WORKS"
}
```

### 5.3 Classification rationale — **WORKS**

Unified text diff, binary notice, read-only editor tab flags, and refresh-safe selection all observed through production `SelectFileCommand` → `ISourceControlDiffTabService` → editor tab strip. Line-level add/delete **background coloring** is not claimed (**UNVERIFIED-VIS** / known plain monospace rendering per A2).

---

## 6. `A1-GT-03` — stage / unstage / stage-all / validation / commit / identity / refresh

### 6.1 Sequence

| Step | Action | Result |
|------|--------|--------|
| 1 | Open mutation fixture (3 unstaged) | Success |
| 2 | Commit with whitespace message | `Commit message cannot be empty.` |
| 3 | Commit with message, nothing staged | `Nothing staged to commit.` |
| 4 | Stage `a.txt` | Staged=1, unstaged=2; CLI cached has `a.txt` |
| 5 | Unstage `a.txt` | Staged=0; unstaged has `a.txt` again |
| 6 | Stage all | Staged=`a.txt,b.txt,c.txt` |
| 7 | Commit `"A3 GT-03 local commit smoke"` | HEAD advances; lists empty; message cleared |
| 8 | Open no-identity fixture; stage; commit | Identity `CommitError` |

### 6.2 Machine-readable excerpt

```json
{
  "scenarioId": "A1-GT-03",
  "exitCode": 0,
  "observedViewModelState": {
    "empty_msg.CommitError": "Commit message cannot be empty.",
    "nothing_staged.CommitError": "Nothing staged to commit.",
    "stage_one.Staged": ["a.txt"],
    "stage_one.Unstaged": ["b.txt", "c.txt"],
    "stage_one.cli_cached": "a.txt\n",
    "unstage.Staged": [],
    "unstage.Unstaged": ["a.txt", "b.txt", "c.txt"],
    "stage_all.Staged": ["a.txt", "b.txt", "c.txt"],
    "commit.head_before": "83b55ebd535903261b290f9eff7c4cd5413b0de2",
    "commit.head_after": "08095a2cc5856c5d07a8d9da9a0c32abaded2fa0",
    "commit.log_subject": "A3 GT-03 local commit smoke",
    "commit.CommitError": null,
    "commit.CommitMessage": "",
    "commit.Unstaged": 0,
    "commit.Staged": 0,
    "commit.LastRefreshStatus": "Success",
    "identity.CommitError": "Git user identity is not configured. Set user.name and user.email in your git config."
  },
  "classificationHint": "WORKS"
}
```

### 6.3 Classification rationale — **WORKS**

All mutation, multi-layer validation, identity error reporting, and post-mutation refresh checks passed. Push path intentionally **not** exercised. Red `CommitError` text-block **paint** is **UNVERIFIED-VIS**; the string value on the ViewModel is proven.

---

## 7. `A1-GT-04` — attached branch and detached-HEAD SHA

### 7.1 Sequence

| Step | Action | Result |
|------|--------|--------|
| 1 | Open attached `master` fixture | Branch labels = `master` |
| 2 | Open detached-HEAD fixture | Labels = full commit SHA |

### 7.2 Machine-readable excerpt

```json
{
  "scenarioId": "A1-GT-04",
  "exitCode": 0,
  "observedViewModelState": {
    "attached.cli_branch": "master",
    "attached.CurrentBranchName": "master",
    "attached.BranchText": "master",
    "attached.Branches": ["master:current=True"],
    "detached.cli_head": "ef9fe2021aec7df8632a1e974a074167719a613c",
    "detached.cli_symbolic_ref_exit": 1,
    "detached.cli_branch": "",
    "detached.CurrentBranchName": "ef9fe2021aec7df8632a1e974a074167719a613c",
    "detached.BranchText": "ef9fe2021aec7df8632a1e974a074167719a613c"
  },
  "classificationHint": "WORKS"
}
```

### 7.3 Classification rationale — **WORKS**

Attached branch name and detached full SHA both match CLI truth on `SourceControlViewModel.CurrentBranchName` and `StatusBarViewModel.BranchText`. Branch ComboBox **checkout** is not claimed (A2: selection-only, non-mutating). Status-bar icon paint is **UNVERIFIED-VIS**.

---

## 8. Machine-readable aggregate

```json
{
  "schema_version": "a3-evidence-1",
  "audit": "v1-v3-product-reality",
  "phase": "A3",
  "slice": "A3_GIT_WORKFLOW",
  "overall": "INCOMPLETE",
  "repo_head": "9b343012ac9b06ebb38443a3a9d65fe22f7d3021",
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "harness": {
    "type": "out-of-tree Avalonia.Headless 12.0.5 + production DI",
    "assembly_name": "Zaide.Tests",
    "runner_root": "/tmp/zaide-a3-gt/",
    "entry": "A3HeadlessEntry.BuildAvaloniaApp (does not call Program.BuildAvaloniaApp)"
  },
  "classifications": {
    "A1-GT-01": "WORKS",
    "A1-GT-02": "WORKS",
    "A1-GT-03": "WORKS",
    "A1-GT-04": "WORKS"
  },
  "assertion_totals": {
    "A1-GT-01": "24/24",
    "A1-GT-02": "20/20",
    "A1-GT-03": "23/23",
    "A1-GT-04": "10/10",
    "all": "77/77"
  },
  "profiles": {
    "A1-GT-01": "/tmp/zaide-a3-gt-profile-fbB2djK8",
    "A1-GT-02": "/tmp/zaide-a3-gt-profile-ywyxCdog",
    "A1-GT-03": "/tmp/zaide-a3-gt-profile-79ODqBY2",
    "A1-GT-04": "/tmp/zaide-a3-gt-profile-7OB5m3mR"
  }
}
```

---

## 9. Visual-only claims (`UNVERIFIED-VIS`)

| Claim | Status |
|-------|--------|
| Source Control list-row paint / icons | **UNVERIFIED-VIS** |
| Status bar Git-branch icon glyph | **UNVERIFIED-VIS** |
| Diff tab monospace add/delete background coloring | **UNVERIFIED-VIS** (content text proven; coloring not claimed) |
| Red `CommitError` TextBlock color `#E05555` | **UNVERIFIED-VIS** (string value proven on ViewModel) |

These do **not** downgrade the functional rows above; they are explicitly out of headless drawing claims.

---

## 10. Blockers and residual notes

| Item | Severity | Notes |
|------|----------|-------|
| No background filesystem watcher | Known product limitation (A2) | External CLI edits require explicit refresh / panel switch / post-mutation refresh — exercised via explicit refresh |
| Branch ComboBox non-checkout | Known product limitation (A2) | Not exercised as mutation; display path covered via status bar |
| Push / pull / merge / rebase | Out of scope | Not executed; not classified |
| A3 overall | Incomplete | Only GT-01…GT-04 of remaining A3 journeys |

**No scenario BLOCKED.** All four rows exited process code **0** with all listed assertions **pass**.

---

## 11. Status line

**A3 Git workflow smoke (`A1-GT-01`…`A1-GT-04`): executed (product-runtime smoke).**

| Row | Classification |
|-----|----------------|
| `A1-GT-01` | **WORKS** |
| `A1-GT-02` | **WORKS** |
| `A1-GT-03` | **WORKS** |
| `A1-GT-04` | **WORKS** |

**A3 as a whole: incomplete.**

**A4 / stabilization / V4: not begun.**

**Townhall, agents, permissions, trace/memory, restart, A4: not begun.**

---

*Recorded 2026-08-01. Out-of-tree Avalonia.Headless 12.0.5 product-runtime runner executed clean-profile Git workflow smoke under disposable XDG + isolated git config with disposable `/tmp` repositories; temporary runner, profiles, and fixtures removed; no production edits.*
