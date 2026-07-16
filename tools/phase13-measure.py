#!/usr/bin/env python3
"""Run the opt-in Phase 13 release measurements.

This is local evidence tooling, not application instrumentation.  It runs the
already-defined executable Startup, LSP, Build, Run, Test, and DAP seams, plus
the M0 test-only app-internal editor open/edit/save/restore and large-document
load seams.  It never injects keyboard or pointer input, never claims that an
X11 key event reached Avalonia command routing on a Wayland desktop, and does
not replace M4b Linux desktop smoke or keyboard/focus/status evidence.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import signal
import statistics
import subprocess
import sys
import tempfile
import time
from dataclasses import asdict, dataclass
from datetime import UTC, datetime
from typing import Callable


ROOT = Path(__file__).resolve().parent.parent
PROCESS_AREAS = ("startup", "lsp", "build", "run", "test", "dap")
APP_INTERNAL_AREAS = ("editor", "large-file")
DEFAULT_AREAS = PROCESS_AREAS + APP_INTERNAL_AREAS
BUDGETS_MS = {
    "startup": 1000,
    "lsp": 8000,
    "build-cold": 2500,
    "build-warm": 600,
    "run": 1000,
    "test": 1500,
    "dap": 2000,
    # Locked after M0 app-internal five-sample baseline (see M0_RELEASE_BASELINE_PROOF.md).
    # Values are filled by the first accepted baseline run; until then the gate
    # still enforces sample success and the 10% range rule when a budget is set.
}


@dataclass
class Sample:
    area: str
    classification: str
    sample: int
    command: list[str]
    elapsed_ms: float
    exit_code: int | None
    status: str
    note: str
    fixture_sha256: dict[str, str]
    stdout: str
    stderr: str


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def fixture_hashes(paths: list[Path]) -> dict[str, str]:
    hashes = {}
    for path in paths:
        try:
            label = str(path.relative_to(ROOT))
        except ValueError:
            label = str(path)
        hashes[label] = sha256(path)
    return hashes


def run_command(command: list[str], timeout_seconds: float, environment: dict[str, str] | None = None) -> tuple[float, int | None, str, str, str]:
    start = time.monotonic_ns()
    try:
        completed = subprocess.run(
            command,
            cwd=ROOT,
            env=environment,
            text=True,
            capture_output=True,
            timeout=timeout_seconds,
            check=False,
        )
        elapsed = (time.monotonic_ns() - start) / 1_000_000
        status = "pass" if completed.returncode == 0 else "fail"
        return elapsed, completed.returncode, status, completed.stdout, completed.stderr
    except subprocess.TimeoutExpired as error:
        elapsed = (time.monotonic_ns() - start) / 1_000_000
        stdout = error.stdout.decode() if isinstance(error.stdout, bytes) else error.stdout or ""
        stderr = error.stderr.decode() if isinstance(error.stderr, bytes) else error.stderr or ""
        return elapsed, None, "fail", stdout, f"timeout after {timeout_seconds}s\n{stderr}"
    except OSError as error:
        elapsed = (time.monotonic_ns() - start) / 1_000_000
        return elapsed, None, "fail", "", str(error)


def run_startup(sample_number: int, app: Path, timeout_seconds: float) -> Sample:
    command = [str(app)]
    hashes = fixture_hashes([app])
    if not shutil.which("xdotool"):
        return Sample("startup", "desktop", sample_number, command, 0, None, "fail", "xdotool is required to observe the new XWayland window", hashes, "", "")

    start = time.monotonic_ns()
    process = subprocess.Popen(command, cwd=ROOT, start_new_session=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    stdout = ""
    stderr = ""
    try:
        deadline = time.monotonic() + timeout_seconds
        while time.monotonic() < deadline:
            lookup = subprocess.run(
                ["xdotool", "search", "--onlyvisible", "--pid", str(process.pid), "--name", "Zaide"],
                text=True,
                capture_output=True,
                check=False,
            )
            if lookup.returncode == 0 and lookup.stdout.strip():
                elapsed = (time.monotonic_ns() - start) / 1_000_000
                return Sample("startup", "desktop", sample_number, command, elapsed, 0, "pass", "new visible XWayland window owned by launched PID", hashes, lookup.stdout, lookup.stderr)
            if process.poll() is not None:
                stdout, stderr = process.communicate()
                elapsed = (time.monotonic_ns() - start) / 1_000_000
                return Sample("startup", "desktop", sample_number, command, elapsed, process.returncode, "fail", "application exited before a visible window was found", hashes, stdout, stderr)
            time.sleep(0.02)
        elapsed = (time.monotonic_ns() - start) / 1_000_000
        return Sample("startup", "desktop", sample_number, command, elapsed, None, "fail", f"no visible window within {timeout_seconds}s", hashes, stdout, stderr)
    finally:
        if process.poll() is None:
            os.killpg(process.pid, signal.SIGTERM)
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)


def command_sample(area: str, classification: str, sample_number: int, command: list[str], timeout_seconds: float, hashes: dict[str, str], environment: dict[str, str] | None = None) -> Sample:
    elapsed, exit_code, status, stdout, stderr = run_command(command, timeout_seconds, environment)
    return Sample(area, classification, sample_number, command, elapsed, exit_code, status, "", hashes, stdout, stderr)


def lsp_samples(count: int, timeout_seconds: float) -> list[Sample]:
    smoke = ROOT / "tools/Phase10M4CompletionHoverSmoke/bin/Debug/net10.0/Phase10M4CompletionHoverSmoke.dll"
    fixture = ROOT / "tools/Phase10M0LanguageIntelligenceProof/fixture"
    if not smoke.exists():
        raise RuntimeError(f"LSP smoke is not built: {smoke}. Build the existing smoke outside the timed action first.")
    with tempfile.TemporaryDirectory(prefix="zaide-phase13-lsp-") as temporary:
        copied = Path(temporary) / "fixture"
        shutil.copytree(fixture, copied, ignore=shutil.ignore_patterns("bin", "obj"))
        hashes = fixture_hashes([fixture / "Fixture.csproj", fixture / "Sample.cs", smoke])
        command = ["dotnet", str(smoke), str(copied)]
        return [command_sample("lsp", "real-server", index, command, timeout_seconds, hashes) for index in range(1, count + 1)]


def process_samples(area: str, count: int, timeout_seconds: float, adapter: Path | None) -> list[Sample]:
    console_project = ROOT / "tests/fixtures/workflow-console/WorkflowConsole.csproj"
    console_source = ROOT / "tests/fixtures/workflow-console/Program.cs"
    tests_project = ROOT / "tests/fixtures/workflow-tests-pass/WorkflowTestsPass.csproj"
    test_source = ROOT / "tests/fixtures/workflow-tests-pass/PassingTests.cs"
    if area == "build":
        command = ["dotnet", "build", str(console_project), "--no-restore"]
        hashes = fixture_hashes([console_project, console_source])
        return [command_sample("build", "cold" if index == 1 else "warm", index, command, timeout_seconds, hashes) for index in range(1, count + 1)]
    if area == "run":
        command = ["dotnet", "run", "--project", str(console_project), "--no-restore"]
        hashes = fixture_hashes([console_project, console_source])
        return [command_sample("run", "process", index, command, timeout_seconds, hashes) for index in range(1, count + 1)]
    if area == "test":
        command = ["dotnet", "test", str(tests_project), "--no-restore"]
        hashes = fixture_hashes([tests_project, test_source])
        return [command_sample("test", "process", index, command, timeout_seconds, hashes) for index in range(1, count + 1)]
    if area == "dap":
        if adapter is None or not adapter.is_file():
            raise RuntimeError("DAP requires --netcoredbg or ZAIDE_NETCOREDBG_PATH pointing to the existing NetCoreDbg executable.")
        command = ["dotnet", "test", "Zaide.slnx", "--no-build", "--filter", "FullyQualifiedName=Zaide.Tests.Services.M4DebugExecutionProofTests.ProductionProof_LaunchBreakpointStepAndStop"]
        hashes = fixture_hashes([console_project, console_source, adapter])
        environment = os.environ.copy()
        environment["ZAIDE_NETCOREDBG_PATH"] = str(adapter)
        return [command_sample("dap", "real-adapter", index, command, timeout_seconds, hashes, environment) for index in range(1, count + 1)]
    raise ValueError(area)


def ensure_large_file_fixture(path: Path) -> None:
    """Generate the deterministic 8 MiB fixture outside the timed action when missing."""
    expected = "0a014ac760b7eb31cd7b75b2aa1a897b7fe430571a5ac874a3c8706c54c9ffd9"
    if path.is_file() and sha256(path) == expected:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    generator = ROOT / "tools/phase13-generate-large-file.py"
    completed = subprocess.run(
        ["python3", str(generator), str(path)],
        cwd=ROOT,
        text=True,
        capture_output=True,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(f"large-file fixture generation failed: {completed.stderr or completed.stdout}")
    if sha256(path) != expected:
        raise RuntimeError(f"large-file fixture hash mismatch at {path}")


def app_internal_samples(areas: list[str], count: int, timeout_seconds: float) -> list[Sample]:
    """Drive the test-only EditorTabViewModel/OpenFile/Save seam via the test host.

    Timing is performed inside the test host with Stopwatch.GetTimestamp (monotonic).
    This function only orchestrates fixture prep and evidence collection; setup is
    outside each sample's clock boundary.
    """
    editor_fixture = ROOT / "tests/fixtures/workflow-console/Program.cs"
    large_fixture = Path("/tmp/zaide-phase13/large-file-8MiB.txt")
    if "large-file" in areas:
        ensure_large_file_fixture(large_fixture)

    staging = Path(tempfile.mkdtemp(prefix="zaide-phase13-editor-measure-"))
    environment = os.environ.copy()
    environment["ZAIDE_PHASE13_EDITOR_MEASURE_OUTPUT"] = str(staging)
    environment["ZAIDE_PHASE13_EDITOR_MEASURE_AREAS"] = ",".join(areas)
    environment["ZAIDE_PHASE13_EDITOR_MEASURE_SAMPLES"] = str(count)

    command = [
        "dotnet",
        "test",
        "Zaide.slnx",
        "--no-build",
        "--filter",
        "FullyQualifiedName=Zaide.Tests.Services.Phase13M0EditorMeasurementTests.MeasurementRunner_WritesEvidence_WhenOutputEnvSet",
    ]
    elapsed, exit_code, status, stdout, stderr = run_command(command, timeout_seconds, environment)
    if status != "pass":
        note = f"app-internal measurement host failed after {elapsed:.3f} ms (setup not timed per sample)"
        hashes = fixture_hashes([editor_fixture] + ([large_fixture] if large_fixture.is_file() else []))
        return [
            Sample(
                area=area,
                classification="setup",
                sample=0,
                command=command,
                elapsed_ms=0,
                exit_code=exit_code,
                status="fail",
                note=f"{note}: {stderr or stdout}",
                fixture_sha256=hashes,
                stdout=stdout,
                stderr=stderr,
            )
            for area in areas
        ]

    samples: list[Sample] = []
    for area in areas:
        raw_path = staging / area / "raw-samples.json"
        if not raw_path.is_file():
            samples.append(
                Sample(
                    area=area,
                    classification="setup",
                    sample=0,
                    command=command,
                    elapsed_ms=0,
                    exit_code=exit_code,
                    status="fail",
                    note=f"missing in-process evidence file: {raw_path}",
                    fixture_sha256={},
                    stdout=stdout,
                    stderr=stderr,
                )
            )
            continue

        raw_samples = json.loads(raw_path.read_text())
        for entry in raw_samples:
            fixture_path = Path(entry["FixturePath"])
            samples.append(
                Sample(
                    area=entry["Area"],
                    classification=entry["Classification"],
                    sample=int(entry["Sample"]),
                    command=command
                    + [
                        f"in-process:{entry['Area']}",
                        entry.get("ClockBoundary", ""),
                    ],
                    elapsed_ms=float(entry["ElapsedMs"]),
                    exit_code=0 if entry["Status"] == "pass" else 1,
                    status=entry["Status"],
                    note=entry.get("Note", ""),
                    fixture_sha256={str(fixture_path): entry["FixtureSha256"]},
                    stdout=json.dumps(entry),
                    stderr="",
                )
            )
    return samples


def summarize(samples: list[Sample]) -> list[dict[str, object]]:
    groups: dict[tuple[str, str], list[Sample]] = {}
    for sample in samples:
        groups.setdefault((sample.area, sample.classification), []).append(sample)
    summaries = []
    for (area, classification), group in groups.items():
        timings = [sample.elapsed_ms for sample in group]
        passed = all(sample.status == "pass" for sample in group)
        budget_key = f"{area}-{classification}" if area == "build" else area
        budget = BUDGETS_MS.get(budget_key)
        median = statistics.median(timings)
        minimum, maximum = min(timings), max(timings)
        variance = statistics.pvariance(timings) if len(timings) > 1 else 0.0
        standard_deviation = statistics.pstdev(timings) if len(timings) > 1 else 0.0
        range_ms = maximum - minimum
        variance_pass = median > 0 and range_ms <= median * 0.10
        if budget is None:
            # Baseline-capture mode: require all samples pass and variance gate.
            # A numeric release budget is locked later from the recorded median.
            budget_pass = passed and variance_pass
            gate = "PASS" if budget_pass else "FAIL"
        else:
            budget_pass = passed and maximum <= budget and variance_pass
            gate = "PASS" if budget_pass else "FAIL"
        summaries.append({
            "area": area,
            "classification": classification,
            "sample_count": len(group),
            "pass_count": sum(sample.status == "pass" for sample in group),
            "median_ms": round(median, 3),
            "min_ms": round(minimum, 3),
            "max_ms": round(maximum, 3),
            "range_ms": round(range_ms, 3),
            "variance_ms2": round(variance, 3),
            "standard_deviation_ms": round(standard_deviation, 3),
            "budget_ms": budget,
            "budget_pass": budget_pass,
            "gate": gate,
        })
    return summaries


def environment_snapshot() -> dict[str, object]:
    command = ["dotnet", "--info"]
    _, _, _, stdout, stderr = run_command(command, 30)
    process_snapshot = subprocess.run(
        ["ps", "-eo", "pid=,comm=,args="], text=True, capture_output=True, check=False
    ).stdout.splitlines()
    related_processes = [
        line.strip() for line in process_snapshot
        if any(marker in line.lower() for marker in ("zaide", "dotnet", "csharp-ls", "netcoredbg"))
    ]
    return {
        "timestamp_utc": datetime.now(UTC).isoformat(),
        "cwd": str(ROOT),
        "platform": platform.platform(),
        "machine": platform.machine(),
        "python": sys.version,
        "display": os.getenv("DISPLAY"),
        "wayland_display": os.getenv("WAYLAND_DISPLAY"),
        "xdg_session_type": os.getenv("XDG_SESSION_TYPE"),
        "xdg_current_desktop": os.getenv("XDG_CURRENT_DESKTOP"),
        "csharp_ls": shutil.which("csharp-ls"),
        "netcoredbg": os.getenv("ZAIDE_NETCOREDBG_PATH"),
        "concurrent_load_observation": {
            "captured_after_samples": True,
            "related_processes": related_processes,
            "note": "Review this snapshot before accepting a run as quiet-machine evidence; the runner never suppresses or removes ambient processes.",
        },
        "dotnet_info": stdout or stderr,
    }


def write_evidence(output: Path, samples: list[Sample], summaries: list[dict[str, object]], environment: dict[str, object]) -> None:
    output.mkdir(parents=True, exist_ok=False)
    serializable_samples = [asdict(sample) for sample in samples]
    (output / "raw-samples.json").write_text(json.dumps(serializable_samples, indent=2) + "\n")
    (output / "summary.json").write_text(json.dumps({"environment": environment, "summaries": summaries}, indent=2) + "\n")
    with (output / "raw-samples.tsv").open("w", newline="") as stream:
        fields = ["area", "classification", "sample", "elapsed_ms", "exit_code", "status", "note", "command", "fixture_sha256"]
        writer = csv.DictWriter(stream, fieldnames=fields, delimiter="\t")
        writer.writeheader()
        for sample in serializable_samples:
            writer.writerow({
                **{field: sample[field] for field in fields if field in sample},
                "command": json.dumps(sample["command"]),
                "fixture_sha256": json.dumps(sample["fixture_sha256"], sort_keys=True),
            })


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--areas",
        nargs="+",
        choices=list(DEFAULT_AREAS),
        default=list(DEFAULT_AREAS),
        help="areas to measure (process seams and/or app-internal editor seams)",
    )
    parser.add_argument("--samples", type=int, default=5, help="samples per area (default: 5)")
    parser.add_argument("--output", type=Path, help="new evidence directory (default: /tmp/zaide-phase13/measurements/<UTC timestamp>)")
    parser.add_argument("--app", type=Path, default=ROOT / "src/bin/Debug/net10.0/Zaide", help="prebuilt desktop executable for startup samples")
    parser.add_argument("--netcoredbg", type=Path, default=os.getenv("ZAIDE_NETCOREDBG_PATH"), help="existing NetCoreDbg executable; never downloaded")
    parser.add_argument("--timeout", type=float, default=60, help="per process sample timeout in seconds")
    parser.add_argument(
        "--app-internal-timeout",
        type=float,
        default=120,
        help="timeout for the combined in-process editor/large-file measurement host (seconds)",
    )
    parser.add_argument("--startup-timeout", type=float, default=10, help="per startup sample timeout in seconds")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.samples < 1:
        raise SystemExit("--samples must be positive")
    output = args.output or Path("/tmp/zaide-phase13/measurements") / datetime.now(UTC).strftime("%Y%m%dT%H%M%SZ")
    adapter = Path(args.netcoredbg) if args.netcoredbg else None
    samples: list[Sample] = []
    current_area = "setup"
    try:
        process_requested = [area for area in args.areas if area in PROCESS_AREAS]
        app_internal_requested = [area for area in args.areas if area in APP_INTERNAL_AREAS]

        for area in process_requested:
            current_area = area
            if area == "startup":
                if not args.app.is_file():
                    raise RuntimeError(f"startup executable is not built: {args.app}. Build it before the timed action.")
                samples.extend(run_startup(index, args.app, args.startup_timeout) for index in range(1, args.samples + 1))
            elif area == "lsp":
                samples.extend(lsp_samples(args.samples, args.timeout))
            else:
                samples.extend(process_samples(area, args.samples, args.timeout, adapter))

        if app_internal_requested:
            current_area = ",".join(app_internal_requested)
            samples.extend(
                app_internal_samples(
                    app_internal_requested,
                    args.samples,
                    args.app_internal_timeout,
                )
            )
    except (OSError, RuntimeError, ValueError) as error:
        samples.append(Sample(current_area, "setup", 0, [], 0, None, "fail", str(error), {}, "", ""))

    summaries = summarize(samples)
    environment = environment_snapshot()
    write_evidence(output, samples, summaries, environment)
    print(f"evidence={output}")
    for summary in summaries:
        print("{area}/{classification}: {gate}; median={median_ms} ms; min={min_ms}; max={max_ms}; variance={variance_ms2} ms^2; budget={budget_ms}".format(**summary))
    return 0 if all(summary["gate"] == "PASS" for summary in summaries) else 1


if __name__ == "__main__":
    raise SystemExit(main())
