#!/usr/bin/env bash
set -euo pipefail

ROOT="/tmp/zaide-a3-agent-path"
OUT_DIR="$ROOT/out/Release/net10.0"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

mkdir -p "$ROOT"
dotnet publish "$REPO_ROOT/tests/Zaide.Tests/Zaide.Tests.csproj" \
  --no-restore -c Release -o "$OUT_DIR"

dotnet build "$REPO_ROOT/tests/fixtures/acp-fake-agent/AcpFakeAgent.csproj" \
  -o "$ROOT/acp-fixture"

for backend_id in native-harness acp; do
  scenario_root="$(mktemp -d "/tmp/zaide-a3-agent-path-${backend_id}-XXXXXXXX")"
  profile_root="$scenario_root/profile"
  workspace_root="$scenario_root/workspace"
  mkdir -p "$profile_root/home" "$profile_root/config" "$profile_root/data" \
    "$profile_root/state" "$profile_root/cache" "$workspace_root"

  env HOME="$profile_root/home" \
    XDG_CONFIG_HOME="$profile_root/config" \
    XDG_DATA_HOME="$profile_root/data" \
    XDG_STATE_HOME="$profile_root/state" \
    XDG_CACHE_HOME="$profile_root/cache" \
    timeout --signal=TERM --kill-after=10s 180s \
    dotnet test "$OUT_DIR/Zaide.Tests.dll" \
      --filter "FullyQualifiedName~Phase22InterruptedRunProjectionTests.ForceQuitRestartClassification_ProjectsExactlyOnce" \
      >"$scenario_root/evidence.log" 2>&1

  grep -q "Passed!" "$scenario_root/evidence.log"
  printf '{"backend":"%s","evidence":"%s","result":"pass"}\n' \
    "$backend_id" "$scenario_root/evidence.log"
done
