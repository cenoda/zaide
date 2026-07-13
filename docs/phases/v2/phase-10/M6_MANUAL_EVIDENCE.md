# Phase 10 M6: Manual Linux Evidence — Document Formatting + Format on Save

**Date:** 2026-07-14  
**Host:** Linux  
**Server:** csharp-ls 0.25.0 (`/home/cenoda/.dotnet/tools/csharp-ls`)  
**Client:** production `CsharpLsSession` + `LanguageFormattingService` via
`tools/Phase10M6FormattingSmoke/`

## Command

```bash
export PATH="$PATH:$HOME/.dotnet/tools"

dotnet build tools/Phase10M6FormattingSmoke/Phase10M6FormattingSmoke.csproj
dotnet run --project tools/Phase10M6FormattingSmoke/Phase10M6FormattingSmoke.csproj --no-build -- \
  tools/Phase10M0LanguageIntelligenceProof/fixture
```

## Environment

| Item | Value |
|---|---|
| OS | Linux |
| csharp-ls | 0.25.0 (via `LanguageServerBinaryLocator`) |
| Fixture | `tools/Phase10M0LanguageIntelligenceProof/fixture` |
| Sample | deliberately unformatted `Unformatted.cs` written by the smoke |
| Evidence path | `docs/phases/v2/phase-10/M6_MANUAL_EVIDENCE.md` |

## Observed result (exact)

```text
Fixture: /home/cenoda/zaide/tools/Phase10M0LanguageIntelligenceProof/fixture
csharp-ls: /home/cenoda/.dotnet/tools/csharp-ls
Sample: /home/cenoda/zaide/tools/Phase10M0LanguageIntelligenceProof/fixture/Unformatted.cs
Session state=Ready gen=4 failure=
Format outcome: kind=Applied feedback=Document formatted.
PASS formatDocument: sourceLen=118 formattedLen=167
--- formatted preview (first 200 chars) ---
using System;
namespace Demo
{
    public class Unformatted
    {
        public static void Main()
        {
            Console.WriteLine("hello");
        }
    }
}
PASS undo: one undo restores entire original document
PASS formatOnSave: accepted formatting written once
PASS failureStillSaves: kind=Unavailable; wrote current content unchanged
Capabilities: DocumentFormattingSupported=True
PASS Phase 10 M6 formatting smoke
```

## What this proves

1. **Format Document** through the production session negotiates
   `documentFormattingProvider` and returns non-empty, valid edits for a
   deliberately unformatted C# file (118 → 167 chars).
2. **Single undo** via the M0-proven `StartUndoGroup` / `EndUndoGroup` seam
   restores the entire original document in one step.
3. **Format on Save** applies accepted formatting to in-memory content and
   writes the formatted text once.
4. **Failure path still saves:** formatting an inactive/unavailable document
   does not mutate text; save writes the current content unchanged.

## Command inventory (M6)

| Command id | Default gesture | Role |
|---|---|---|
| `editor.formatDocument` | `Ctrl+Shift+I` | Format Document (whole document) |

## Format-on-Save contract (locked M0, exercised)

1. Format runs before the file write when `editor.formatOnSave` is true.
2. Accepted formatting updates in-memory `Document.Content`, then that text is written.
3. Unsupported / failure / cancellation / stale / invalid leaves text unchanged and still saves current content.
4. Formatting never re-triggers save.

## Settings schema (M6)

| Item | Value |
|---|---|
| Property | `EditorSettings.FormatOnSave` / JSON `formatOnSave` |
| Default | `false` |
| Schema version | `2` (v1→v2 migration registered in production `SettingsService`) |
| Serializer ceiling | `schemaVersion` 1–2 accepted; `> 2` rejected |

## Limitation

This smoke exercises the production formatting service pipeline end-to-end on
Linux with a real server. Desktop `Ctrl+Shift+I` gesture materialization and
Settings panel toggle click-through are covered by unit/command/settings tests
on the shared editor path; the smoke validates protocol, edit application,
undo grouping, and the Format-on-Save write contract.
