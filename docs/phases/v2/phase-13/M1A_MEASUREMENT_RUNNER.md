# Phase 13 M1a: Automated Measurement Runner

## Scope

`tools/phase13-measure.py` is opt-in, local evidence tooling. It has no
production reference, does not write application settings, adds no telemetry,
and does not download a language server or debug adapter. It measures only the
already-defined executable seams:

- startup to a visible XWayland window owned by the newly launched application
  PID;
- the existing real-server completion/hover/stale-result smoke;
- the existing real `dotnet` Build, Run, and Test fixtures; and
- the existing real-NetCoreDbg breakpoint, step, and stop proof.

The runner writes a new evidence directory containing:

- `raw-samples.tsv` — one row for every accepted or failed process sample;
- `raw-samples.json` — the same samples with command output retained;
- `summary.json` — command environment, fixture SHA-256 identities, median,
  minimum, maximum, range, population variance, standard deviation, budget,
  and pass/fail gate.

Fixture preparation is deliberately outside each timed action. The LSP fixture
copy and the prebuilt smoke/executable are checked before timing; restore,
build, fixture generation, and adapter acquisition are not included.

## Run

Build the existing targets before timing, generate the existing large-file
fixture only when the manual row needs it, and then run the runner:

```bash
dotnet build Zaide.slnx --no-restore
dotnet build tools/Phase10M4CompletionHoverSmoke/Phase10M4CompletionHoverSmoke.csproj --no-restore
python3 tools/phase13-generate-large-file.py /tmp/zaide-phase13/large-file-8MiB.txt
ZAIDE_NETCOREDBG_PATH=/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg \
  python3 tools/phase13-measure.py --output /tmp/zaide-phase13/measurements/m1a-<UTC>
```

The default is five samples per selected area. `--areas startup lsp build run
test dap` selects all executable areas. Build sample 1 is explicitly `cold`;
the remaining samples are `warm`. A run exits non-zero if any sample fails, a
sample exceeds its numeric budget, or the accepted-sample range exceeds ten
percent of the median. No outlier is silently discarded.

## Remaining App-Internal Automation Rows

The completed runner does not attempt editor open/edit/save or 8 MiB rendering.
Those rows are not human stopwatch tasks. A follow-up M0-only extension must
add a local, test-only app-internal measurement seam that calls the same
application command paths for document load, edit, save, restore, and large
document load. It must record the fixture SHA-256, five raw samples, clock
boundary, and post-save restoration result.

The extension must not use injected keyboard or pointer input, and it must not
add production telemetry or persistent measurement collection. It provides
numeric performance evidence only; the real Linux desktop smoke and
keyboard/focus/status audit remain M4b responsibilities.

On the supported Wayland desktop, the runner uses `xdotool` only to observe a
new visible XWayland startup window. It never injects keys. In particular, an
injected X11 `Ctrl+Space` event is not proof that Avalonia command routing ran;
live completion invocation remains not validated until it is performed through
truthful desktop interaction.

## M1a Evidence Status

M1a establishes repeatable automation for Startup, LSP, Build, Run, Test, and
DAP. Its raw evidence is local and timestamped under the selected output
directory; it is intentionally not committed because timing samples are
machine-specific. The planned M0-only app-internal extension must still measure
editor open/edit/save and 8 MiB rendering, so M1a alone cannot close M0 or
authorize M1b.
