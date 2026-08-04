#!/usr/bin/env bash
# M4-scoped A1-TC-05 force-quit producer driver.
# Publishes the out-of-tree producer, runs the real parent controller for each
# sibling backend, and validates machine-readable evidence fields (not merely
# "Passed!"). Does not run the full M5 A3 matrix.
set -euo pipefail

ROOT="/tmp/zaide-a3-agent-path"
RUNNER_DIR="$ROOT/runner"
OUT_DIR="$ROOT/out/Release/net10.0"
EVIDENCE_DIR="$ROOT/evidence"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_HEAD="$(git -C "$REPO_ROOT" rev-parse HEAD)"

test -f "$RUNNER_DIR/Zaide.Tests.csproj"
test -f "$RUNNER_DIR/Program.cs"

mkdir -p "$OUT_DIR" "$EVIDENCE_DIR" "$ROOT/acp-fixture"

echo "==> restore/publish M4 force-quit producer"
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
  python3 - "$evidence_path" "$backend_id" "$REPO_HEAD" <<'PY'
import json, sys
path, backend, repo_head = sys.argv[1], sys.argv[2], sys.argv[3]
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
if doc.get("scenarioId") != "A1-TC-05":
    raise SystemExit(f"scenarioId mismatch: {doc.get('scenarioId')}")
if doc.get("exitCode") != 0:
    raise SystemExit(f"exitCode non-zero: {doc.get('exitCode')} failures={doc.get('failures')}")

obs = doc.get("observed") or {}
required_obs = [
    "child.pid", "child.pgid", "scenario.token", "profile.root", "workspace.root",
    "checkpoint.session_id", "checkpoint.run_id", "kill.observed_dead",
    "cleanup.result", "assertion.pass_count", "assertion.total_count",
]
missing_obs = [k for k in required_obs if k not in obs]
if missing_obs:
    raise SystemExit(f"missing observed fields: {missing_obs}")

# Restart classification and resend IDs must be present (flat or prefixed).
restart_keys = [k for k in obs if k.startswith("restart.")]
if not restart_keys:
    raise SystemExit("missing restart.* evidence fields")

# Force-kill proof: child must have been observed dead after kill.
if str(obs.get("kill.observed_dead")).lower() not in ("true", "1"):
    raise SystemExit(f"kill.observed_dead not true: {obs.get('kill.observed_dead')}")

# Assertions must exist and all pass.
assertions = doc.get("assertions") or []
if not assertions:
    raise SystemExit("no assertions recorded")
failed = [a for a in assertions if a.get("result") != "pass"]
if failed:
    raise SystemExit(f"failed assertions: {failed}")

if int(doc.get("assertionPassCount") or 0) < 1:
    raise SystemExit("assertionPassCount < 1")

print(f"OK evidence {path} backend={backend} assertions={doc['assertionPassCount']}/{doc['assertionTotal']}")
PY
}

for backend_id in native-harness acp; do
  scenario_root="$(mktemp -d "/tmp/zaide-a3-agent-path-${backend_id}-XXXXXXXX")"
  profile_root="$scenario_root/profile"
  workspace_root="$scenario_root/workspace"
  state_dir="$scenario_root/state"
  evidence_path="$EVIDENCE_DIR/A1-TC-05-${backend_id}.json"
  mkdir -p "$profile_root/home" "$profile_root/config" "$profile_root/data" \
    "$profile_root/state" "$profile_root/cache" "$workspace_root" "$state_dir"

  echo "==> run A1-TC-05 force-quit controller backend=$backend_id"
  # Failure cleanup guard only — timeout is NOT the positive force-quit proof.
  # The controller force-kills the admitted-running child process group itself.
  set +e
  timeout --signal=TERM --kill-after=15s 240s \
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
      --dll "$OUT_DIR/Zaide.Tests.dll"
  rc=$?
  set -e

  if [[ $rc -ne 0 ]]; then
    echo "controller failed rc=$rc for backend=$backend_id" >&2
    if [[ -f "$evidence_path" ]]; then
      echo "---- evidence (partial) ----" >&2
      cat "$evidence_path" >&2 || true
    fi
    exit "$rc"
  fi

  test -f "$evidence_path"
  validate_evidence "$evidence_path" "$backend_id"
  # Retain scenario roots for independent audit; do not delete profile/workspace.
  printf '{"backend":"%s","evidence":"%s","scenario_root":"%s","result":"pass"}\n' \
    "$backend_id" "$evidence_path" "$scenario_root"
done

echo "M4 force-quit A3 producer: native-harness + acp PASS"
