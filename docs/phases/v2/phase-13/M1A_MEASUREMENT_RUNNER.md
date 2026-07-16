# Phase 13 M1a: Automated Measurement Runner

## Scope

`tools/phase13-measure.py` is opt-in, local evidence tooling. It has no
production reference, does not write application settings, adds no telemetry,
and does not download a language server or debug adapter. It measures:

- startup to a visible XWayland window owned by the newly launched application
  PID;
- the existing real-server completion/hover/stale-result smoke;
- the existing real `dotnet` Build, Run, and Test fixtures;
- the existing real-NetCoreDbg breakpoint, step, and stop proof; and
- the M0 test-only app-internal editor open/edit/save/restore and 8 MiB
  document-load seams (see below).

The runner writes a new evidence directory containing:

- `raw-samples.tsv` — one row for every accepted or failed process sample;
- `raw-samples.json` — the same samples with command output retained;
- `summary.json` — command environment, fixture SHA-256 identities, median,
  minimum, maximum, range, population variance, standard deviation, budget,
  and pass/fail gate.

Fixture preparation is deliberately outside each timed action. The LSP fixture
copy and the prebuilt smoke/executable are checked before timing; restore,
build, fixture generation, and adapter acquisition are not included. For the
app-internal seams, three untimed warm-up samples (JIT/path priming) are also
outside the clock.

## Run

Build the existing targets before timing, generate the existing large-file
fixture only when the large-file area needs it, and then run the runner:

```bash
dotnet build Zaide.slnx --no-restore
dotnet build tools/Phase10M4CompletionHoverSmoke/Phase10M4CompletionHoverSmoke.csproj --no-restore
python3 tools/phase13-generate-large-file.py /tmp/zaide-phase13/large-file-8MiB.txt
ZAIDE_NETCOREDBG_PATH=/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg \
  python3 tools/phase13-measure.py --output /tmp/zaide-phase13/measurements/m1a-<UTC>
```

Process-only areas (original M1a):

```bash
python3 tools/phase13-measure.py --areas startup lsp build run test dap \
  --output /tmp/zaide-phase13/measurements/m1a-<UTC>
```

App-internal editor and large-file areas (M0 extension):

```bash
dotnet build Zaide.slnx --no-restore
python3 tools/phase13-generate-large-file.py /tmp/zaide-phase13/large-file-8MiB.txt
python3 tools/phase13-measure.py --areas editor large-file \
  --output /tmp/zaide-phase13/measurements/m0-editor-<UTC>
```

The default is five samples per selected area. `--areas startup lsp build run
test dap editor large-file` selects all areas. Build sample 1 is explicitly
`cold`; the remaining build samples are `warm`. A run exits non-zero if any
sample fails, a sample exceeds its numeric budget (when a budget is locked),
or the accepted-sample range exceeds ten percent of the median. No outlier is
silently discarded.

## App-Internal Editor / Large-File Seam

| Item | Value |
|---|---|
| Seam type | Test-only, local, in-process (no production telemetry) |
| Implementation | `tests/Zaide.Tests/Services/Phase13M0EditorMeasurementSeam.cs` |
| Focused tests | `tests/Zaide.Tests/Services/Phase13M0EditorMeasurementTests.cs` |
| Orchestration | `tools/phase13-measure.py --areas editor large-file` invokes the opt-in test `MeasurementRunner_WritesEvidence_WhenOutputEnvSet` with `ZAIDE_PHASE13_EDITOR_MEASURE_*` env vars |
| Clock | `Stopwatch.GetTimestamp` high-resolution monotonic test-host clock |
| Editor path | `OpenFileCommand` → `TextContent` edit → `SaveCommand` → close → re-open; post-save in-memory and on-disk content must match; source fixture is never mutated (private work copy) |
| Large-file path | `OpenFileCommand` document load of the generated 8 MiB fixture; content length and fixture SHA verified |
| Not used | `xdotool`, AT-SPI, XTest, synthetic keyboard/pointer, human stopwatch |
| Not claimed | Avalonia UI rendering, keyboard routing, focus, or Linux desktop smoke (M4b) |

## M1a Evidence Status

M1a establishes repeatable automation for Startup, LSP, Build, Run, Test, and
DAP. Its raw evidence is local and timestamped under the selected output
directory; it is intentionally not committed because timing samples are
machine-specific.

The M0 app-internal extension measures editor open/edit/save/restore and 8 MiB
document load. Five functional samples exist for both rows
(`/tmp/zaide-phase13/measurements/m0-editor-20260716T-final/`), but both rows
**fail the 10% range variance gate**, so numeric budgets are not locked and
M0/M1b remain blocked. M1a alone never closed M0; the extension supplies the
missing measurement site but does not yet supply a variance-passing baseline.

On the supported Wayland desktop, the runner uses `xdotool` only to observe a
new visible XWayland startup window. It never injects keys. In particular, an
injected X11 `Ctrl+Space` event is not proof that Avalonia command routing ran;
live completion invocation remains not validated until it is performed through
truthful desktop interaction.
