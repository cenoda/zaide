# Phase 10 M5: Manual Linux Evidence — Go to Definition + Symbols

**Date:** 2026-07-13  
**Host:** Linux  
**Server:** csharp-ls 0.25.0 (`/home/cenoda/.dotnet/tools/csharp-ls`)  
**Client:** production `CsharpLsSession` + `LanguageNavigationService` +
`LanguageSymbolService` via `tools/Phase10M5NavigationSymbolsSmoke/`

## Command

```bash
export PATH="$PATH:$HOME/.dotnet/tools"

dotnet build tools/Phase10M5NavigationSymbolsSmoke/Phase10M5NavigationSymbolsSmoke.csproj
dotnet run --project tools/Phase10M5NavigationSymbolsSmoke/Phase10M5NavigationSymbolsSmoke.csproj --no-build -- \
  tools/Phase10M0LanguageIntelligenceProof/fixture
```

## Environment

| Item | Value |
|---|---|
| OS | Linux |
| csharp-ls | 0.25.0 (Punia)+19a9574d7577521555f49bf49e94688a3ba67dd2 |
| Fixture | `tools/Phase10M0LanguageIntelligenceProof/fixture` |
| Evidence path | `docs/phases/v2/phase-10/M5_MANUAL_EVIDENCE.md` |

## Observed result (exact)

```text
Fixture: /home/cenoda/zaide/tools/Phase10M0LanguageIntelligenceProof/fixture
csharp-ls: /home/cenoda/.dotnet/tools/csharp-ls
Session state=Ready gen=4 failure=
PASS definition: file=Sample.cs line=12 char=25
PASS documentSymbol: count=7 first=Broken()
PASS documentSymbol navigate: Sample.cs:18
PASS workspaceSymbol: count=1 first=string Sample.Greet(string name)
PASS workspaceSymbol navigate: Sample.cs:12
PASS stale: cancelled/stale definition after tab switch did not navigate
PASS Phase 10 M5 definition/symbols smoke
```

## What this proves

1. **Go to Definition** across the production session negotiates
   `definitionProvider` and returns a valid location for `Sample.Greet`
   (Sample.cs line 12).
2. **Document symbols** list symbols for the active live document (7 symbols)
   and produce a navigable location for the selected symbol.
3. **Workspace symbols** for query `Greet` return a navigable match.
4. After switching the active document mid-flight, a stale/cancelled definition
   response cannot be taken for navigation and does not leave a chooser open.

## Command inventory (M5)

| Command id | Default gesture | Role |
|---|---|---|
| `editor.goToDefinition` | `F12` | Go to Definition |
| `editor.documentSymbol` | `Ctrl+Shift+O` | Document symbols surface |
| `workbench.symbol` | `Ctrl+T` | Workspace symbols surface |

## Navigation path (locked)

1. Open/activate target via `EditorTabViewModel.OpenFileCommand` only.
2. Re-validate URI/range against the live document text.
3. Request selection via `EditorViewModel.RequestNavigate`.

## Limitation

This smoke exercises the production service pipeline end-to-end on Linux with a
real server. Desktop multi-result chooser and symbol-popup click-through are not
automated; unit and input-routing tests cover presentation contracts and
command registration on the shared editor path.
