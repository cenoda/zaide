#!/usr/bin/env bash
# Re-validate all M5 A3 evidence files using the fixed validate_evidence function.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_HEAD="$(git -C "$REPO_ROOT" rev-parse HEAD)"

EVIDENCE_DIR="/tmp/zaide-a3-agent-path/evidence"

declare -a pairs=(
  "A1-AS-02:native-harness"
  "A1-TH-05:native-harness"
  "A1-MR-03:native-harness"
  "A1-TP-01:native-harness"
  "A1-TP-02:native-harness"
  "A1-TP-03:native-harness"
  "A1-TC-05:native-harness"
  "A1-TC-09:native-harness"
  "A1-AS-02:acp"
  "A1-TH-05:acp"
  "A1-MR-03:acp"
  "A1-TP-01:acp"
  "A1-TP-02:acp"
  "A1-TP-03:acp"
  "A1-TC-05:acp"
  "A1-TC-09:acp"
)

pass=0
fail=0
for pair in "${pairs[@]}"; do
  scenario_id="${pair%%:*}"
  backend_id="${pair##*:}"
  evidence_path="$EVIDENCE_DIR/${scenario_id}-${backend_id}.json"
  if [[ ! -f "$evidence_path" ]]; then
    echo "MISSING $evidence_path"
    fail=$((fail+1))
    continue
  fi
  if python3 - "$evidence_path" "$backend_id" "$scenario_id" "$REPO_HEAD" <<'PY'
import json, sys
path, backend, scenario, repo_head = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
with open(path, encoding="utf-8") as f:
    doc = json.load(f)

required_top = ["schemaVersion", "phase", "scenarioId", "backendId", "repoHead",
    "startedAtUtc", "finishedAtUtc", "host", "isolation", "observed",
    "assertions", "assertionPassCount", "assertionTotal", "exitCode"]
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

print(f"OK {backend} {scenario} {doc['assertionPassCount']}/{doc['assertionTotal']}")
PY
  then
    pass=$((pass+1))
  else
    echo "FAIL $evidence_path"
    fail=$((fail+1))
  fi
done

echo "VALIDATION: $pass pass, $fail fail"
if [[ $fail -gt 0 ]]; then
  exit 1
fi
echo "M5 A3 matrix evidence: ALL VALIDATED"
