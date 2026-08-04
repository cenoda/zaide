#!/usr/bin/env bash
# Phase 22.3 M5 owned-row dual-backend A3 matrix driver.
# Runs the complete approved A3 matrix for both Native Harness and ACP
# using the vendored out-of-tree producer. Validates machine-readable
# evidence per scenario (not merely "Passed!"). Native Harness and ACP
# remain independent siblings — no wrapping, fallback, cross-backend
# retry, silent resume, action replay, or permission replay.
set -euo pipefail

ROOT="/tmp/zaide-a3-agent-path"
RUNNER_DIR="$ROOT/runner"
OUT_DIR="$ROOT/out/Release/net10.0"
EVIDENCE_DIR="$ROOT/evidence"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_RUNNER="$REPO_ROOT/tests/a3-agent-path/runner"
REPO_HEAD="$(git -C "$REPO_ROOT" rev-parse HEAD)"

SCENARIOS=(
  A1-AS-02
  A1-TH-05
  A1-MR-03
  A1-TP-01
  A1-TP-02
  A1-TP-03
  A1-TC-05
  A1-TC-09
)

test -d "$REPO_RUNNER"
test -f "$REPO_RUNNER/Zaide.Tests.csproj"
test -f "$REPO_RUNNER/Program.cs"

mkdir -p "$ROOT" "$OUT_DIR" "$EVIDENCE_DIR" "$ROOT/acp-fixture"
mkdir -p "$RUNNER_DIR"
rsync -a --delete "$REPO_RUNNER/" "$RUNNER_DIR/"
# Out-of-tree publish requires an absolute project reference to the repository.
sed -i "s|<ProjectReference Include=\".*Zaide.csproj\" />|<ProjectReference Include=\"$REPO_ROOT/src/Zaide.csproj\" />|" \
  "$RUNNER_DIR/Zaide.Tests.csproj"

echo "==> restore/publish M5 dual-backend A3 matrix producer"
dotnet restore "$RUNNER_DIR/Zaide.Tests.csproj"
dotnet publish "$RUNNER_DIR/Zaide.Tests.csproj" \
  --no-restore -c Release -o "$OUT_DIR"

test -f "$OUT_DIR/Zaide.Tests.dll"

echo "==> build repository ACP fake-agent fixture"
dotnet build "$REPO_ROOT/tests/fixtures/acp-fake-agent/AcpFakeAgent.csproj" \
  -o "$ROOT/acp-fixture"
test -x "$ROOT/acp-fixture/AcpFakeAgent" || test -f "$ROOT/acp-fixture/AcpFakeAgent.dll"

# Prefer native binary when present; otherwise run via dotnet + dll.
ACP_FIXTURE="$ROOT/acp-fixture/AcpFakeAgent"
if [[ ! -x "$ACP_FIXTURE" && -f "$ROOT/acp-fixture/AcpFakeAgent.dll" ]]; then
  ACP_FIXTURE="$ROOT/acp-fixture/AcpFakeAgent.dll"
fi

validate_evidence() {
  local evidence_path="$1"
  local backend_id="$2"
  local scenario_id="$3"
  python3 - "$evidence_path" "$backend_id" "$scenario_id" "$REPO_HEAD" <<'PY'
import json, sys
path, backend, scenario, repo_head = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
with open(path, encoding="utf-8") as f:
    doc = json.load(f)

required_top = [
    "schemaVersion", "phase", "scenarioId", "backendId", "repoHead",
    "startedAtUtc", "finishedAtUtc", "host", "isolation", "observed",
    "assertions", "assertionPassCount", "assertionTotal", "exitCode",
]
missing = [k for k in required_top if k not in doc]
if missing:
    raise SystemExit(f"missing top-level fields: {missing}")

if doc.get("backendId") != backend:
    raise SystemExit(f"backendId mismatch: {doc.get('backendId')} != {backend}")
if doc.get("repoHead") != repo_head:
    raise SystemExit(f"repoHead mismatch: {doc.get('repoHead')} != {repo_head}")
if doc.get("scenarioId") != scenario:
    raise SystemExit(f"scenarioId mismatch: {doc.get('scenarioId')} != {scenario}")
if doc.get("exitCode") != 0:
    raise SystemExit(f"exitCode non-zero: {doc.get('exitCode')} failures={doc.get('failures')}")

obs = doc.get("observed") or {}

required_obs_baseline = [
    "child.pid", "child.pgid", "scenario.token", "profile.root", "workspace.root",
    "cleanup.result", "assertion.pass_count", "assertion.total_count",
]
missing_obs = [k for k in required_obs_baseline if k not in obs]
if missing_obs:
    raise SystemExit(f"missing observed fields: {missing_obs}")

# scenario.id / backend.id are written by the M5 scenario controller;
# the M4 A1-TC-05 force-quit controller wrote scenario_id / backend_id.
# Accept either form for scenario/backend identification.
scenario_id_ok = (
    str(obs.get("scenario.id", "")) == scenario
    or str(obs.get("scenario_id", "")) == scenario
    or doc.get("scenarioId") == scenario
)
if not scenario_id_ok:
    raise SystemExit(
        f"scenario id mismatch: observed={obs.get('scenario.id') or obs.get('scenario_id')} "
        f"expected={scenario} doc={doc.get('scenarioId')}")
backend_id_ok = (
    str(obs.get("backend.id", "")) == backend
    or str(obs.get("backend_id", "")) == backend
    or doc.get("backendId") == backend
)
if not backend_id_ok:
    raise SystemExit(
        f"backend id mismatch: observed={obs.get('backend.id') or obs.get('backend_id')} "
        f"expected={backend} doc={doc.get('backendId')}")

assertions = doc.get("assertions") or []
if not assertions:
    raise SystemExit("no assertions recorded")
failed = [a for a in assertions if a.get("result") != "pass"]
if failed:
    raise SystemExit(f"failed assertions: {failed}")

if int(doc.get("assertionPassCount") or 0) < 1:
    raise SystemExit("assertionPassCount < 1")

if str(obs.get("cleanup.result", "")).split(":", 1)[0] != "ok":
    raise SystemExit(f"cleanup.result not ok: {obs.get('cleanup.result')}")

print(
    f"OK evidence {path} backend={backend} scenario={scenario} "
    f"assertions={doc['assertionPassCount']}/{doc['assertionTotal']}")
PY
}

# Run the full A3 matrix for both backends.
declare -a passed_pairs=()
declare -a failed_pairs=()

for backend_id in native-harness acp; do
  for scenario_id in "${SCENARIOS[@]}"; do
    scenario_root="$(mktemp -d "/tmp/zaide-a3-agent-path-${backend_id}-${scenario_id}-XXXXXXXX")"
    profile_root="$scenario_root/profile"
    workspace_root="$scenario_root/workspace"
    state_dir="$scenario_root/state"
    evidence_path="$EVIDENCE_DIR/${scenario_id}-${backend_id}.json"
    mkdir -p "$profile_root/home" "$profile_root/config" "$profile_root/data" \
      "$profile_root/state" "$profile_root/cache" "$workspace_root" "$state_dir"

    echo "==> run ${scenario_id} backend=${backend_id}"
    set +e
    timeout --signal=TERM --kill-after=15s 360s \
      env HOME="$profile_root/home" \
        XDG_CONFIG_HOME="$profile_root/config" \
        XDG_DATA_HOME="$profile_root/data" \
        XDG_STATE_HOME="$profile_root/state" \
        XDG_CACHE_HOME="$profile_root/cache" \
      dotnet "$OUT_DIR/Zaide.Tests.dll" \
        --role controller \
        --backend "$backend_id" \
        --profile "$profile_root" \
        --workspace "$workspace_root" \
        --evidence "$evidence_path" \
        --repo-head "$REPO_HEAD" \
        --acp-fixture "$ACP_FIXTURE" \
        --state-dir "$state_dir" \
        --dll "$OUT_DIR/Zaide.Tests.dll" \
        --scenario "$scenario_id"
    rc=$?
    set -e

    if [[ $rc -ne 0 ]] || [[ ! -f "$evidence_path" ]]; then
      echo "controller failed rc=$rc for backend=$backend_id scenario=$scenario_id" >&2
      if [[ -f "$evidence_path" ]]; then
        echo "---- evidence (partial) ----" >&2
        cat "$evidence_path" >&2 || true
      fi
      failed_pairs+=("${backend_id}/${scenario_id}")
      continue
    fi

    if ! validate_evidence "$evidence_path" "$backend_id" "$scenario_id"; then
      echo "evidence validation failed for backend=$backend_id scenario=$scenario_id" >&2
      failed_pairs+=("${backend_id}/${scenario_id}")
      continue
    fi
    passed_pairs+=("${backend_id}/${scenario_id}")
    printf '{"backend":"%s","scenario":"%s","evidence":"%s","scenario_root":"%s","result":"pass"}\n' \
      "$backend_id" "$scenario_id" "$evidence_path" "$scenario_root"
  done
done

echo
echo "M5 A3 matrix: PASSED ${#passed_pairs[@]} / FAILED ${#failed_pairs[@]}"
for pair in "${passed_pairs[@]}"; do
  echo "  PASS  $pair"
done
for pair in "${failed_pairs[@]}"; do
  echo "  FAIL  $pair"
done

if [[ "${#failed_pairs[@]}" -gt 0 ]]; then
  exit 1
fi

echo "M5 A3 matrix PASS: native-harness + acp for all owned rows"
