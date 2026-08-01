# A3 Language Intelligence Preflight — Negative Path and Blocker Evidence

**Date:** 2026-08-01  
**Scope:** `A1-FN-08` through `A1-FN-15` only  
**Overall status:** **INCOMPLETE** — the requested missing-`csharp-ls` product-runtime negative path could not be exercised because the current environment has `csharp-ls` installed and discoverable.

## Safety and scope

- No production code, tracked tests, package pins, or audit policy were modified.
- No `csharp-ls` installation, fake language server, or test double was used.
- No Build, Run, Test, debugging, Git, Townhall, agents, permissions, trace, memory, restart, stabilization, A4, or V4-planning rows were executed.
- No `xdtools` or desktop automation was used.
- The only repository change from this run is this evidence file.

## Exact binary lookup evidence

The initial shell environment was:

```text
PATH=/opt/cursor/usr/share/cursor/resources/app/node_modules/@vscode/ripgrep/bin:/home/cenoda/.bun/bin:/home/cenoda/.grok/bin:/home/cenoda/.local/bin:/home/cenoda/hf-venv/bin:/home/cenoda/.kimi-code/bin:/home/cenoda/.local/bin:/opt/cursor/usr/bin:/home/cenoda/.bun/bin:/home/cenoda/.cargo/bin:/usr/local/sbin:/usr/local/bin:/usr/bin:/opt/cuda/bin:/var/lib/flatpak/exports/bin:/usr/lib/jvm/default/bin:/usr/bin/site_perl:/usr/bin/vendor_perl:/usr/bin/core_perl:/var/lib/snapd/bin:/home/cenoda/.lmstudio/bin:/home/cenoda/.local/share/JetBrains/Toolbox/scripts:/home/cenoda/.lmstudio/bin:/home/cenoda/.dotnet/tools:/home/cenoda/.lmstudio/bin:/home/cenoda/.local/share/JetBrains/Toolbox/scripts
```

Commands and exact results:

```text
$ command -v csharp-ls
/home/cenoda/.dotnet/tools/csharp-ls

$ type -a csharp-ls
csharp-ls is /home/cenoda/.dotnet/tools/csharp-ls

$ readlink -f ~/.dotnet/tools/csharp-ls
/home/cenoda/.dotnet/tools/csharp-ls
```

An isolated lookup was also performed with `/home/cenoda/.dotnet/tools`
removed from `PATH`:

```text
$ PATH="<original PATH with /home/cenoda/.dotnet/tools removed>" command -v csharp-ls
<no output; exit status 1>
```

This confirms that the binary is available at the production conventional
location, even though it is not found when that directory is removed from
`PATH`. The requested “missing on PATH and conventional location” condition
was therefore not present.

## Disposable fixture and cleanup

The disposable run created and removed:

```text
run root: /tmp/zaide-a3-language.eMhghd
profile:  /tmp/zaide-a3-language.eMhghd/profile
fixture:  /tmp/zaide-a3-language.eMhghd/fixture
project:  /tmp/zaide-a3-language.eMhghd/fixture/NegativePath.csproj
source:   /tmp/zaide-a3-language.eMhghd/fixture/Broken.cs
```

Fixture project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

Deliberately invalid source:

```csharp
namespace NegativePath; public class Broken { public void M() { int value = ; } }
```

The fixture and profile were removed in the same disposable shell command.
The command verified that the run root no longer existed (`CLEANED`). No
real-user or repository workspace path was used for the fixture or profile.

## Production-path execution result

The production tree-to-editor path and UI language-session observations were
**not executed**. The requested negative-path premise was unavailable, and
the permitted scope explicitly forbids using a fake server or changing the
environment to manufacture the missing-server condition. The fixture was
created only to document the intended disposable scenario and was cleaned up.

Consequently, this run has no runtime observation for:

- session state or failure kind;
- status-bar text or Problems feedback;
- Problems surface availability;
- command enabled/disabled state;
- command execution through the production tree-to-editor path.

The following source-defined behavior is recorded as a preflight reference,
not as runtime evidence: production maps a missing binary to
`LanguageSessionState.Failed` / `MissingServerBinary`, status text
`C# · Failed`, and Problems text
`C# language server not found. Install with: dotnet tool install -g csharp-ls`.
This was not observed in A3.

## Commands attempted

Only the following non-product commands were executed:

1. `command -v csharp-ls`
2. `type -a csharp-ls`
3. `readlink -f ~/.dotnet/tools/csharp-ls`
4. An isolated `PATH` lookup with the conventional tools directory removed
5. Disposable fixture creation and cleanup

Not executed: Problems, completion, hover, Go to Definition, Document Symbols,
Workspace Symbols, Format Document, and Format on Save. No positive behavior
for these operations is claimed.

## Per-row classification

The classifications below distinguish the unavailable positive path from
unverified visual/control behavior. No row is upgraded to `WORKS`.

| Row | Classification | Evidence status |
|---|---|---|
| `A1-FN-08` | `BLOCKED` | Positive diagnostics smoke blocked by the environment state not matching the requested missing-server negative path; Problems/session runtime remains unobserved. |
| `A1-FN-09` | `BLOCKED` | Completion requires a running language server; no positive behavior was exercised. |
| `A1-FN-10` | `BLOCKED` | Hover requires a running language server; no positive behavior was exercised. |
| `A1-FN-11` | `BLOCKED` | Go to Definition requires a running language server; no positive behavior was exercised. |
| `A1-FN-12` | `BLOCKED` | Document Symbols requires a running language server; no positive behavior was exercised. |
| `A1-FN-13` | `BLOCKED` | Workspace Symbols requires a running language server; no positive behavior was exercised. |
| `A1-FN-14` | `BLOCKED` | Format Document requires a running language server; no positive behavior was exercised. |
| `A1-FN-15` | `UNVERIFIED` | The Format on Save toggle was not visually exercised. Any toggle-only result must be recorded separately from the formatting effect; formatting cannot be verified without a running server. |

## Machine-readable evidence

```yaml
audit: v1-v3-product-reality
phase: A3
slice: A3_LANGUAGE_INTELLIGENCE_PREFLIGHT
scope:
  rows: [A1-FN-08, A1-FN-09, A1-FN-10, A1-FN-11, A1-FN-12, A1-FN-13, A1-FN-14, A1-FN-15]
  negative_path_only: true
  overall: INCOMPLETE
binary:
  name: csharp-ls
  path_lookup:
    command_v: /home/cenoda/.dotnet/tools/csharp-ls
    type_a: "csharp-ls is /home/cenoda/.dotnet/tools/csharp-ls"
    conventional_location: /home/cenoda/.dotnet/tools/csharp-ls
  missing_from_path_without_dotnet_tools: true
  missing_from_conventional_location: false
execution:
  production_tree_to_editor_path: NOT_EXECUTED
  production_runtime: NOT_EXECUTED
  reason: "Requested missing-server condition was unavailable; no fake or modified server environment permitted."
fixture:
  project: /tmp/zaide-a3-language.eMhghd/fixture/NegativePath.csproj
  source: /tmp/zaide-a3-language.eMhghd/fixture/Broken.cs
  invalid_source: true
  profile: /tmp/zaide-a3-language.eMhghd/profile
  cleaned: true
commands:
  problems: NOT_ATTEMPTED
  completion: NOT_ATTEMPTED
  hover: NOT_ATTEMPTED
  go_to_definition: NOT_ATTEMPTED
  document_symbols: NOT_ATTEMPTED
  workspace_symbols: NOT_ATTEMPTED
  format_document: NOT_ATTEMPTED
  format_on_save_toggle: NOT_ATTEMPTED
classifications:
  A1-FN-08: BLOCKED
  A1-FN-09: BLOCKED
  A1-FN-10: BLOCKED
  A1-FN-11: BLOCKED
  A1-FN-12: BLOCKED
  A1-FN-13: BLOCKED
  A1-FN-14: BLOCKED
  A1-FN-15: UNVERIFIED
```

## Prerequisite for a later positive smoke

A later positive language-intelligence smoke requires an explicitly controlled,
disposable environment in which the real production `csharp-ls` binary is
available and runnable, plus a disposable single-project C# workspace opened
through the production tree-to-editor path. That later run must observe
`Ready`, exercise each requested command, and separately verify the
Format-on-Save toggle and its formatting effect. This A3 preflight remains
incomplete and does not authorize A4, stabilization, or V4 planning.
