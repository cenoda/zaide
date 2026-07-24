#!/usr/bin/env bash
# Phase 16 M3 — single-candidate TC-T01 observational qualification smoke.
# Requires PHASE16_M3_QUALIFICATION_GRANT=1 and a one-shot DeepSeek sub-key file.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_ROOT="${PHASE16_ARTIFACT_ROOT:-/tmp/phase16-artifacts}"
PHASE_ROOT="$ARTIFACT_ROOT/phase-16"
SESSION_ID="m3q-$(date -u +%Y%m%dT%H%M%SZ)-$(openssl rand -hex 4)"
SESSION_ROOT="$PHASE_ROOT/records/m3-qualification/$SESSION_ID"
DNS_ROOT="$PHASE_ROOT/records/dns-binding/$SESSION_ID"
PROBE_DIR="$SESSION_ROOT/probes"
RUN_DIR="$SESSION_ROOT/run"
CRED_DIR="$PHASE_ROOT/credentials"
QWEN_ROOT="$PHASE_ROOT/artifacts/qwen-code/v0.20.1/inspect/qwen-code"
QWEN_BIN="$QWEN_ROOT/bin/qwen"
FIXTURE_SRC="$ROOT/Fixtures/TC-T01"
WORKSPACE="$PHASE_ROOT/tuning/TC-T01/workspace-$SESSION_ID"
PROMPT_FILE="$PHASE_ROOT/tuning/TC-T01/prompt-$SESSION_ID.md"
SUBKEY_ONCE="${PHASE16_DEEPSEEK_SUBKEY_ONCE:-$CRED_DIR/subkey.once}"
EXAMPLE_BLOCK_IP="93.184.216.34"
STOP_REASON=""
KEY_CONSUMED=NO
PROVIDER_LAUNCH_ATTEMPTED=NO
CANDIDATE_EXECUTION=NO
# Overall inner budget: DNS/egress probes + Qwen wall (120s) + post-qwen verify + slack.
# Must finish well under any external safety wrapper so post-balance always runs.
INNER_OVERALL_TIMEOUT_SEC="${PHASE16_INNER_OVERALL_TIMEOUT_SEC:-200}"
# After qwen-result.env appears, allow this many extra seconds for verify then reap.
POST_QWEN_VERIFY_BUDGET_SEC="${PHASE16_POST_QWEN_VERIFY_BUDGET_SEC:-45}"
UNSHARE_PID=""
SLIRP_PID=""
# Captured when force_reap reaps the unshare child so a later wait does not
# replace the real exit with bash 127 ("pid is not a child of this shell").
INNER_REAPED_EXIT=""
INNER_WAIT_STATUS=""
INNER_WAIT_EXIT=""

log_step() {
  local step="$1"
  local code="$2"
  printf '%s step=%s exit=%s utc=%s\n' "$SESSION_ID" "$step" "$code" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$SESSION_ROOT/commands-ledger.txt"
}

record_execution_state() {
  {
    echo "provider_launch_attempted=$PROVIDER_LAUNCH_ATTEMPTED"
    echo "candidate_execution=$CANDIDATE_EXECUTION"
    echo "key_consumed=$KEY_CONSUMED"
    echo "qwen_exit_source=${QWEN_EXIT_SOURCE:-none}"
    echo "provider_execution_label=${PROVIDER_EXECUTION_LABEL:-}"
    echo "execution_state_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  } > "$SESSION_ROOT/execution.env"
}

stop_with() {
  STOP_REASON="$1"
  force_reap_children "stop_with"
  record_execution_state
  echo "STOP_REASON=$STOP_REASON" >> "$SESSION_ROOT/summary.env"
  echo "QUALIFICATION_VERDICT=NO-GO" >> "$SESSION_ROOT/summary.env"
  echo "session_end_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$SESSION_ROOT/summary.env"
  echo "M3 qualification STOP: $STOP_REASON" >&2
  exit 1
}

# Key was consumed but Qwen never launched. Must not reuse a historical qwen_exit.
stop_with_no_provider_execution() {
  PROVIDER_LAUNCH_ATTEMPTED=NO
  CANDIDATE_EXECUTION=NO
  QWEN_EXIT_SOURCE=none
  PROVIDER_EXECUTION_LABEL="no candidate launch / no provider execution"
  STOP_REASON="$1"
  force_reap_children "stop_with_no_provider_execution"
  record_execution_state
  echo "STOP_REASON=$STOP_REASON" >> "$SESSION_ROOT/summary.env"
  echo "QUALIFICATION_VERDICT=NO-GO" >> "$SESSION_ROOT/summary.env"
  echo "provider_launch_attempted=NO" >> "$SESSION_ROOT/summary.env"
  echo "candidate_execution=NO" >> "$SESSION_ROOT/summary.env"
  echo "qwen_exit_source=none" >> "$SESSION_ROOT/summary.env"
  echo "provider_execution_label=no candidate launch / no provider execution" >> "$SESSION_ROOT/summary.env"
  echo "session_end_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$SESSION_ROOT/summary.env"
  echo "M3 qualification STOP (no provider execution): $STOP_REASON" >&2
  exit 1
}

resolve_current_session_qwen_exit() {
  if [ ! -f "$RUN_DIR/qwen-result.env" ]; then
    echo ""
    return 0
  fi
  awk -F= '$1=="qwen_exit"{print $2}' "$RUN_DIR/qwen-result.env"
}

# Kill/reap unshare + slirp4netns so finalization (balance-after, workspace
# verify, cleanup) always runs after Qwen exit or timeout. Idempotent.
# Must run in the same shell that started the background jobs (not a subshell).
force_reap_children() {
  local reason="${1:-unspecified}"
  local reap_utc
  reap_utc="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  {
    echo "reap_reason=$reason"
    echo "reap_utc=$reap_utc"
  } >> "$RUN_DIR/reap.env" 2>/dev/null || true

  if [ -n "${SLIRP_PID:-}" ] && kill -0 "$SLIRP_PID" 2>/dev/null; then
    kill -TERM "$SLIRP_PID" 2>/dev/null || true
  fi
  if [ -n "${UNSHARE_PID:-}" ] && kill -0 "$UNSHARE_PID" 2>/dev/null; then
    # unshare --fork: signal the host-visible child (process group leader).
    kill -TERM -- "-$UNSHARE_PID" 2>/dev/null || kill -TERM "$UNSHARE_PID" 2>/dev/null || true
  fi
  # Brief grace, then SIGKILL if still alive.
  local _i
  for _i in 1 2 3 4 5; do
    local still=0
    if [ -n "${UNSHARE_PID:-}" ] && kill -0 "$UNSHARE_PID" 2>/dev/null; then still=1; fi
    if [ -n "${SLIRP_PID:-}" ] && kill -0 "$SLIRP_PID" 2>/dev/null; then still=1; fi
    [ "$still" -eq 0 ] && break
    sleep 0.2
  done
  if [ -n "${UNSHARE_PID:-}" ] && kill -0 "$UNSHARE_PID" 2>/dev/null; then
    kill -KILL -- "-$UNSHARE_PID" 2>/dev/null || kill -KILL "$UNSHARE_PID" 2>/dev/null || true
  fi
  if [ -n "${SLIRP_PID:-}" ] && kill -0 "$SLIRP_PID" 2>/dev/null; then
    kill -KILL "$SLIRP_PID" 2>/dev/null || true
  fi
  if [ -n "${UNSHARE_PID:-}" ]; then
    local unshare_wait_ec=0
    set +e
    wait "$UNSHARE_PID" 2>/dev/null
    unshare_wait_ec=$?
    set -e
    # bash wait returns 127 when the pid is not a child (already reaped).
    # Keep the first real child exit; never overwrite it with 127.
    if [ "$unshare_wait_ec" -ne 127 ]; then
      INNER_REAPED_EXIT="$unshare_wait_ec"
      echo "force_reap_unshare_exit=$unshare_wait_ec" >> "$RUN_DIR/reap.env" 2>/dev/null || true
    else
      echo "force_reap_unshare_wait=not_a_child" >> "$RUN_DIR/reap.env" 2>/dev/null || true
    fi
  fi
  if [ -n "${SLIRP_PID:-}" ]; then
    wait "$SLIRP_PID" 2>/dev/null || true
  fi
}

# Resolve the unshare child exit without inventing a synthetic status.
# Prefer a successful wait(2) result from this shell; fall back to a prior
# force_reap capture. Never treat bash 127 ("not a child") as the child exit.
resolve_unshare_exit_code() {
  local wait_ec=0
  set +e
  wait "$UNSHARE_PID" 2>/dev/null
  wait_ec=$?
  set -e
  if [ "$wait_ec" -ne 127 ]; then
    echo "$wait_ec"
    return 0
  fi
  if [ -n "${INNER_REAPED_EXIT:-}" ]; then
    echo "$INNER_REAPED_EXIT"
    return 0
  fi
  # Last resort: process is gone and no captured wait status. Return empty so
  # caller records an explicit missing-status rather than bash 127.
  echo ""
  return 0
}

# Bounded wait for unshare: sets INNER_WAIT_STATUS / INNER_WAIT_EXIT when the
# process exits, or when qwen-result.env is present and post-qwen budget elapses,
# or overall timeout. Never hangs. Must NOT run under command substitution —
# wait only works for children of this shell (not a $(...) subshell).
wait_inner_with_reap_budget() {
  local start_epoch
  start_epoch="$(date +%s)"
  local qwen_seen_epoch=""
  local status="running"
  local exit_code=""

  INNER_WAIT_STATUS="running"
  INNER_WAIT_EXIT=""

  while true; do
    local now elapsed
    now="$(date +%s)"
    elapsed=$((now - start_epoch))

    if ! kill -0 "$UNSHARE_PID" 2>/dev/null; then
      exit_code="$(resolve_unshare_exit_code)"
      if [ -n "$exit_code" ]; then
        status="exited"
      else
        status="exited_status_unavailable"
        exit_code=""
      fi
      break
    fi

    if [ -f "$RUN_DIR/qwen-result.env" ]; then
      if [ -z "$qwen_seen_epoch" ]; then
        qwen_seen_epoch="$now"
        echo "qwen_result_observed_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$RUN_DIR/reap.env"
      fi
      local post=$((now - qwen_seen_epoch))
      if [ "$post" -ge "$POST_QWEN_VERIFY_BUDGET_SEC" ]; then
        status="post_qwen_budget_exceeded"
        force_reap_children "post_qwen_verify_budget"
        # Prefer the real reaped child exit over a synthetic timeout code when
        # the process actually exited during the post-qwen budget window.
        if [ -n "${INNER_REAPED_EXIT:-}" ]; then
          exit_code="$INNER_REAPED_EXIT"
        else
          exit_code=124
        fi
        break
      fi
    fi

    if [ "$elapsed" -ge "$INNER_OVERALL_TIMEOUT_SEC" ]; then
      status="overall_timeout"
      force_reap_children "overall_inner_timeout"
      if [ -n "${INNER_REAPED_EXIT:-}" ]; then
        exit_code="$INNER_REAPED_EXIT"
      else
        exit_code=124
      fi
      break
    fi

    sleep 0.25
  done

  # Ensure slirp is reaped even on clean unshare exit.
  if [ -n "${SLIRP_PID:-}" ]; then
    if kill -0 "$SLIRP_PID" 2>/dev/null; then
      kill -TERM "$SLIRP_PID" 2>/dev/null || true
      sleep 0.2
      kill -KILL "$SLIRP_PID" 2>/dev/null || true
    fi
    wait "$SLIRP_PID" 2>/dev/null || true
  fi

  {
    echo "inner_wait_status=$status"
    echo "inner_wait_exit=${exit_code}"
    echo "inner_wait_elapsed_sec=$(( $(date +%s) - start_epoch ))"
    echo "inner_overall_timeout_sec=$INNER_OVERALL_TIMEOUT_SEC"
    echo "post_qwen_verify_budget_sec=$POST_QWEN_VERIFY_BUDGET_SEC"
    echo "inner_reaped_exit=${INNER_REAPED_EXIT:-}"
  } >> "$RUN_DIR/reap.env"

  INNER_WAIT_STATUS="$status"
  INNER_WAIT_EXIT="${exit_code}"
}

record_workspace_tc_t01_result() {
  local fetch_count retrieve_count verified
  # Host-side check on the bound workspace (does not require inner process).
  fetch_count="$(grep -R --include='*.cs' -F 'FetchData' "$WORKSPACE" 2>/dev/null | wc -l | tr -d ' ')"
  retrieve_count="$(grep -R --include='*.cs' -F 'RetrieveData' "$WORKSPACE" 2>/dev/null | wc -l | tr -d ' ')"
  verified=NO
  if [ "${fetch_count:-0}" -eq 0 ] && [ "${retrieve_count:-0}" -gt 0 ]; then
    verified=YES
  fi
  {
    echo "fetchdata_count=${fetch_count:-0}"
    echo "retrievedata_count=${retrieve_count:-0}"
    echo "tc_t01_rename_verified=$verified"
    echo "workspace_check_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  } > "$RUN_DIR/workspace-result.env"
  cp "$RUN_DIR/workspace-result.env" "$SESSION_ROOT/workspace-result.env" 2>/dev/null || true
  echo "$verified"
}

# Launch an inner script under unshare+slirp4netns and bounded wait/reap.
# Must run in the parent shell (wait is not valid under command substitution).
#
# Contract: always return 0 after publishing status in INNER_WAIT_EXIT.
# Never `return <non-zero>` after re-enabling `set -e` inside this function.
# Bash 5.x defers a function-local `set -e` until the call returns; a non-zero
# return then aborts the whole shell even when the caller used `set +e`, which
# skipped balance-after / workspace / cleanup after qwen_exit=55 (session
# m3q-20260724T072341Z-8f567943).
launch_netns_inner() {
  local inner_script="$1"
  local ledger_step="$2"

  INNER_REAPED_EXIT=""
  INNER_WAIT_STATUS=""
  INNER_WAIT_EXIT=""
  UNSHARE_PID=""
  SLIRP_PID=""

  rm -f /tmp/phase16-ns-pid /tmp/phase16-ns-ready
  : > "$RUN_DIR/reap.env"
  unshare --user --map-root-user --net --mount --fork --pid --mount-proc bash "$inner_script" \
    >"$RUN_DIR/unshare.stdout" 2>"$RUN_DIR/unshare.stderr" &
  UNSHARE_PID=$!
  NSPID="$UNSHARE_PID"
  {
    echo "unshare_pid=$UNSHARE_PID"
    echo "host_ns_pid=$NSPID"
  } > "$RUN_DIR/netns-session.env"
  for _ in $(seq 1 100); do
    if [ -f /tmp/phase16-ns-ready ]; then
      break
    fi
    sleep 0.1
  done
  if [ ! -f /tmp/phase16-ns-ready ]; then
    echo "inner netns never signaled ready for slirp4netns" > "$RUN_DIR/fatal.txt"
    INNER_WAIT_STATUS="launch_failed"
    INNER_WAIT_EXIT=1
    log_step "$ledger_step" 1
    return 0
  fi
  slirp4netns --configure --mtu=65520 --disable-host-loopback "$NSPID" tap0 \
    >"$RUN_DIR/slirp4netns.stdout" 2>"$RUN_DIR/slirp4netns.stderr" &
  SLIRP_PID=$!
  local slirp_attach_ec=1
  for _ in $(seq 1 30); do
    if [ -s "$RUN_DIR/slirp4netns.stderr" ]; then
      if grep -q 'sent tapfd' "$RUN_DIR/slirp4netns.stderr" 2>/dev/null; then
        slirp_attach_ec=0
        break
      fi
      if grep -qE 'failed|Permission denied' "$RUN_DIR/slirp4netns.stderr" 2>/dev/null; then
        echo "slirp4netns attach failed (see slirp4netns.stderr)" > "$RUN_DIR/fatal.txt"
        INNER_WAIT_STATUS="slirp_attach_failed"
        INNER_WAIT_EXIT=1
        log_step "slirp4netns_attach" 1
        log_step "$ledger_step" 1
        return 0
      fi
    fi
    if ! kill -0 "$SLIRP_PID" 2>/dev/null; then
      echo "slirp4netns exited before attach completed" > "$RUN_DIR/fatal.txt"
      INNER_WAIT_STATUS="slirp_exited_early"
      INNER_WAIT_EXIT=1
      log_step "slirp4netns_attach" 1
      log_step "$ledger_step" 1
      return 0
    fi
    sleep 0.1
  done
  log_step "slirp4netns_attach" "$slirp_attach_ec"
  if [ "$slirp_attach_ec" -ne 0 ]; then
    echo "slirp4netns attach did not confirm tapfd handoff" > "$RUN_DIR/fatal.txt"
    INNER_WAIT_STATUS="slirp_attach_timeout"
    INNER_WAIT_EXIT=1
    log_step "$ledger_step" 1
    return 0
  fi

  # Wait/reap in this shell. Keep errexit off for the remainder of the function
  # so a non-zero child status cannot sticky-abort the parent (see contract).
  set +e
  wait_inner_with_reap_budget
  local inner_ec=1
  if [ -n "${INNER_WAIT_EXIT}" ]; then
    inner_ec="$INNER_WAIT_EXIT"
  else
    INNER_WAIT_EXIT=1
    inner_ec=1
  fi
  log_step "$ledger_step" "$inner_ec"
  # Always return 0: real unshare/inner status is INNER_WAIT_EXIT only.
  return 0
}

run_egress_preflight() {
  local inner_egress="$RUN_DIR/inner_egress_preflight.sh"
  cat > "$inner_egress" <<'EOS'
#!/usr/bin/env bash
set -euo pipefail
export PATH=/usr/bin:/bin
: "${PHASE16_RUN_DIR:?}"
: "${PHASE16_PROBE_DIR:?}"
: "${PHASE16_BOUND_IPV4:?}"
: "${PHASE16_EXAMPLE_BLOCK_IP:?}"
: "${PHASE16_NFT_SCRIPT:?}"

ip link set lo up
echo ready > /tmp/phase16-ns-ready
for i in $(seq 1 150); do
  if ip link show tap0 >/dev/null 2>&1 && ip -4 addr show tap0 2>/dev/null | grep -q 'inet '; then
    break
  fi
  sleep 0.1
done
if ! ip link show tap0 >/dev/null 2>&1; then
  echo "tap0_missing" > "$PHASE16_RUN_DIR/fatal.txt"
  exit 2
fi

"$PHASE16_NFT_SCRIPT" "$PHASE16_BOUND_IPV4" > "$PHASE16_RUN_DIR/nft-ruleset.txt"

set +e
curl -sS --http1.1 --proto '=https' --tlsv1.2 --max-time 12 --connect-timeout 8 \
  --resolve "example.com:443:${PHASE16_EXAMPLE_BLOCK_IP}" \
  -o "$PHASE16_PROBE_DIR/block.body" "https://example.com/" \
  >"$PHASE16_PROBE_DIR/block.curl" 2>"$PHASE16_PROBE_DIR/block.err"
BLOCK_EC=$?
curl -sS --http1.1 --proto '=https' --tlsv1.2 --max-time 15 --connect-timeout 10 \
  --resolve "api.deepseek.com:443:${PHASE16_BOUND_IPV4}" \
  -o "$PHASE16_PROBE_DIR/allow.body" "https://api.deepseek.com/" \
  >"$PHASE16_PROBE_DIR/allow.curl" 2>"$PHASE16_PROBE_DIR/allow.err"
ALLOW_EC=$?
set -e
if [ "$ALLOW_EC" -ne 0 ] || [ "$BLOCK_EC" -eq 0 ]; then
  echo "egress_probe_failed allow=$ALLOW_EC block=$BLOCK_EC" > "$PHASE16_RUN_DIR/fatal.txt"
  exit 3
fi
echo "egress_preflight=GO" > "$PHASE16_RUN_DIR/egress-preflight.env"
exit 0
EOS
  chmod +x "$inner_egress"
  launch_netns_inner "$inner_egress" "egress_preflight"
}

mkdir -p "$SESSION_ROOT" "$DNS_ROOT" "$PROBE_DIR" "$RUN_DIR" "$CRED_DIR" "$(dirname "$WORKSPACE")"
chmod 700 "$CRED_DIR" "$SESSION_ROOT" "$DNS_ROOT"
: > "$SESSION_ROOT/commands-ledger.txt"
rm -f "$RUN_DIR/qwen-result.env" "$RUN_DIR/exact-argv.txt" "$RUN_DIR/fatal.txt"
PROVIDER_LAUNCH_ATTEMPTED=NO
CANDIDATE_EXECUTION=NO
QWEN_EXIT_SOURCE=none
PROVIDER_EXECUTION_LABEL=""
record_execution_state
{
  echo "SESSION_ID=$SESSION_ID"
  echo "TASK_ID=TC-T01"
  echo "CANDIDATE_SLUG=qwen-code"
  echo "EVIDENCE_CLASS=observational"
  echo "fresh_session=YES"
  echo "prior_session_verdict_reused=NO"
  echo "locked_max_session_turns=24"
  echo "session_start_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "grant_env=${PHASE16_M3_QUALIFICATION_GRANT:-unset}"
} > "$SESSION_ROOT/summary.env"

if [ "${PHASE16_M3_QUALIFICATION_GRANT:-}" != "1" ]; then
  stop_with "qualification grant env PHASE16_M3_QUALIFICATION_GRANT=1 not set"
fi

for tool in bwrap slirp4netns nft curl unshare getent dotnet; do
  command -v "$tool" >/dev/null 2>&1 || stop_with "required tool missing: $tool"
done
log_step "tool_inventory" 0

[ -x "$QWEN_BIN" ] || stop_with "pinned qwen executable missing at $QWEN_BIN"
log_step "qwen_binary_present" 0

# --- DNS binding gate (host-side resolution only; nft applied inside netns) ---
RESOLVER_CMD="getent ahostsv4 api.deepseek.com"
set +e
RESOLUTION_OUT="$($RESOLVER_CMD 2>"$DNS_ROOT/resolution.err")"
RESOLUTION_EC=$?
set -e
mapfile -t RESOLVED_IPS < <(printf '%s\n' "$RESOLUTION_OUT" | awk '/STREAM/ {print $1}' | sort -u)
DISTINCT_A_COUNT="${#RESOLVED_IPS[@]}"
{
  echo "RESOLVER_CMD=$RESOLVER_CMD"
  echo "EXIT_CODE=$RESOLUTION_EC"
  echo "DISTINCT_A_COUNT=$DISTINCT_A_COUNT"
  echo "RESOLVED_AT_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "RESOLVER_STDOUT_BEGIN"
  printf '%s\n' "$RESOLUTION_OUT"
  echo "RESOLVER_STDOUT_END"
} > "$DNS_ROOT/resolution.env"
log_step "dns_resolution" "$RESOLUTION_EC"

if [ "$RESOLUTION_EC" -ne 0 ] || [ "$DISTINCT_A_COUNT" -ne 1 ]; then
  stop_with "dns binding failed: resolution exit=$RESOLUTION_EC distinct_a=$DISTINCT_A_COUNT"
fi
BOUND_IPV4="${RESOLVED_IPS[0]}"
{
  echo "HOSTNAME=api.deepseek.com"
  echo "BOUND_IPV4=$BOUND_IPV4"
  echo "BINDING_VERDICT=PENDING"
} > "$DNS_ROOT/summary.env"

{
  echo "127.0.0.1 localhost"
  echo "::1 localhost"
  echo "$BOUND_IPV4 api.deepseek.com"
} > "$DNS_ROOT/hosts-injection.txt"
HOSTS_FILE="$DNS_ROOT/hosts-injection.txt"
printf 'nameserver 127.0.0.1\n' > "$DNS_ROOT/resolv-empty.txt"

{
  echo "nft add rule inet phase16_egress output ip daddr ${BOUND_IPV4}/32 tcp dport 443 accept"
} > "$DNS_ROOT/nft-allowlist.txt"

HOSTS_IPV4="$(awk '$2=="api.deepseek.com"{print $1}' "$HOSTS_FILE")"
NFT_IPV4="$(sed -n 's/.*daddr \([0-9.]*\)\/32.*/\1/p' "$DNS_ROOT/nft-allowlist.txt")"
{
  echo "FRESH_IPV4=$BOUND_IPV4"
  echo "HOSTS_IPV4=$HOSTS_IPV4"
  echo "NFT_IPV4=$NFT_IPV4"
  if [ "$BOUND_IPV4" = "$HOSTS_IPV4" ] && [ "$BOUND_IPV4" = "$NFT_IPV4" ]; then
    echo "CONSISTENT=YES"
  else
    echo "CONSISTENT=NO"
  fi
} > "$DNS_ROOT/consistency-check.env"
CONSISTENT="$(awk -F= '$1=="CONSISTENT"{print $2}' "$DNS_ROOT/consistency-check.env")"
log_step "dns_triple_consistency" 0
[ "$CONSISTENT" = "YES" ] || stop_with "dns triple-consistency failed"

# --- Materialize TC-T01 workspace ---
"$FIXTURE_SRC/materialize-fixture.sh" "$WORKSPACE"
cp "$FIXTURE_SRC/prompt.md" "$PROMPT_FILE"
log_step "materialize_tc_t01" 0

# --- Isolation pre-check ---
BWRAP_CHECK="$RUN_DIR/bwrap-check.sh"
cat > "$BWRAP_CHECK" <<'EOS'
#!/usr/bin/env bash
set -euo pipefail
test -w "$PHASE16_WORKSPACE"
touch "$PHASE16_WORKSPACE/.write-ok"
! touch /.phase16-host-write 2>/dev/null
EOS
chmod +x "$BWRAP_CHECK"
set +e
bwrap --unshare-all --die-with-parent --ro-bind / / \
  --bind "$WORKSPACE" "$WORKSPACE" \
  --dev /dev --proc /proc \
  --chdir "$WORKSPACE" --clearenv \
  --setenv PHASE16_WORKSPACE "$WORKSPACE" \
  -- /usr/bin/bash "$BWRAP_CHECK"
BWRAP_EC=$?
set -e
log_step "isolation_bwrap_precheck" "$BWRAP_EC"
[ "$BWRAP_EC" -eq 0 ] || stop_with "bubblewrap isolation pre-check failed exit=$BWRAP_EC"

export PHASE16_RUN_DIR="$RUN_DIR"
export PHASE16_PROBE_DIR="$PROBE_DIR"
export PHASE16_BOUND_IPV4="$BOUND_IPV4"
export PHASE16_EXAMPLE_BLOCK_IP="$EXAMPLE_BLOCK_IP"
export PHASE16_HOSTS_FILE="$HOSTS_FILE"
export PHASE16_RESOLV_EMPTY="$DNS_ROOT/resolv-empty.txt"
export PHASE16_QWEN_BIN="$QWEN_BIN"
export PHASE16_WORKSPACE="$WORKSPACE"
export PHASE16_PROMPT_FILE="$PROMPT_FILE"
export PHASE16_NFT_SCRIPT="$ROOT/Scripts/apply_allowlist_nft.sh"

# --- Egress preflight (no credential; abort before key consumption) ---
set +e
run_egress_preflight
# launch_netns_inner always returns 0; real status is INNER_WAIT_EXIT.
EGRESS_PREFLIGHT_EC="${INNER_WAIT_EXIT:-1}"
set -e
if [ "$EGRESS_PREFLIGHT_EC" -ne 0 ]; then
  if [ -f "$RUN_DIR/fatal.txt" ]; then
    stop_with "$(cat "$RUN_DIR/fatal.txt")"
  fi
  stop_with "egress preflight failed exit=$EGRESS_PREFLIGHT_EC"
fi
log_step "egress_preflight" 0

# --- Credential gate: dedicated one-shot sub-key (never ~/.config / ambient) ---
KEY_LIFECYCLE="$SESSION_ROOT/key-lifecycle.env"
{
  echo "key_source=phase16_one_shot_file"
  echo "key_file_path=$SUBKEY_ONCE"
  echo "created_at_utc="
  echo "one_shot_file_deleted_at_utc="
  echo "value_disclosed=NO"
} > "$KEY_LIFECYCLE"

if [ ! -f "$SUBKEY_ONCE" ]; then
  stop_with "no dedicated Phase16 DeepSeek sub-key one-shot file at $SUBKEY_ONCE"
fi
DEEPSEEK_API_KEY="$(tr -d '\r\n' < "$SUBKEY_ONCE")"
rm -f "$SUBKEY_ONCE"
sync
[ -n "$DEEPSEEK_API_KEY" ] || stop_with_no_provider_execution "sub-key one-shot file was empty"
KEY_CONSUMED=YES
{
  echo "key_source=phase16_one_shot_file"
  echo "key_file_path=$SUBKEY_ONCE"
  echo "created_at_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "one_shot_file_deleted_at_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "value_disclosed=NO"
} > "$KEY_LIFECYCLE"
log_step "credential_load_one_shot" 0
record_execution_state

# --- Cost tracking pre-check (requires consumed key) ---
PRIOR_SPEND="0.00"
PRIOR_SPEND_FILE="$PHASE_ROOT/records/campaign-spend.env"
if [ -f "$PRIOR_SPEND_FILE" ]; then
  # shellcheck disable=SC1090
  source "$PRIOR_SPEND_FILE"
  PRIOR_SPEND="${PHASE16_CAMPAIGN_SPEND_USD:-0.00}"
fi
set +e
curl -sS --max-time 15 \
  -H "Authorization: Bearer ${DEEPSEEK_API_KEY}" \
  "https://api.deepseek.com/user/balance" > "$RUN_DIR/balance-before.json" 2>"$RUN_DIR/balance-before.err"
BALANCE_BEFORE_EC=$?
set -e
log_step "balance_before" "$BALANCE_BEFORE_EC"
[ "$BALANCE_BEFORE_EC" -eq 0 ] || stop_with_no_provider_execution "balance pre-check failed; cannot prove cost tracking"

# --- Inner netns + Qwen launch (credential via env; never written to disk) ---
PROVIDER_LAUNCH_ATTEMPTED=YES
rm -f "$RUN_DIR/qwen-result.env"
record_execution_state
INNER="$RUN_DIR/inner_qualification.sh"
cat > "$INNER" <<'EOS'
#!/usr/bin/env bash
set -euo pipefail
export PATH=/usr/bin:/bin
: "${PHASE16_RUN_DIR:?}"
: "${PHASE16_PROBE_DIR:?}"
: "${PHASE16_BOUND_IPV4:?}"
: "${PHASE16_EXAMPLE_BLOCK_IP:?}"
: "${PHASE16_HOSTS_FILE:?}"
: "${PHASE16_RESOLV_EMPTY:?}"
: "${PHASE16_QWEN_BIN:?}"
: "${PHASE16_WORKSPACE:?}"
: "${PHASE16_PROMPT_FILE:?}"
: "${PHASE16_NFT_SCRIPT:?}"
: "${DEEPSEEK_API_KEY:?}"

ip link set lo up
echo "$$" > /tmp/phase16-ns-pid
echo ready > /tmp/phase16-ns-ready
for i in $(seq 1 150); do
  if ip link show tap0 >/dev/null 2>&1 && ip -4 addr show tap0 2>/dev/null | grep -q 'inet '; then
    break
  fi
  sleep 0.1
done
if ! ip link show tap0 >/dev/null 2>&1; then
  echo "tap0_missing" > "$PHASE16_RUN_DIR/fatal.txt"
  exit 2
fi

"$PHASE16_NFT_SCRIPT" "$PHASE16_BOUND_IPV4" > "$PHASE16_RUN_DIR/nft-ruleset.txt"

set +e
curl -sS --http1.1 --proto '=https' --tlsv1.2 --max-time 12 --connect-timeout 8 \
  --resolve "example.com:443:${PHASE16_EXAMPLE_BLOCK_IP}" \
  -o "$PHASE16_PROBE_DIR/block.body" "https://example.com/" \
  >"$PHASE16_PROBE_DIR/block.curl" 2>"$PHASE16_PROBE_DIR/block.err"
BLOCK_EC=$?
curl -sS --http1.1 --proto '=https' --tlsv1.2 --max-time 15 --connect-timeout 10 \
  --resolve "api.deepseek.com:443:${PHASE16_BOUND_IPV4}" \
  -o "$PHASE16_PROBE_DIR/allow.body" "https://api.deepseek.com/" \
  >"$PHASE16_PROBE_DIR/allow.curl" 2>"$PHASE16_PROBE_DIR/allow.err"
ALLOW_EC=$?
set -e
if [ "$ALLOW_EC" -ne 0 ] || [ "$BLOCK_EC" -eq 0 ]; then
  echo "egress_probe_failed allow=$ALLOW_EC block=$BLOCK_EC" > "$PHASE16_RUN_DIR/fatal.txt"
  exit 3
fi

PROMPT_TEXT="$(tr -d '\r' < "$PHASE16_PROMPT_FILE")"
{
  echo "$PHASE16_QWEN_BIN"
  echo "-p"
  echo "$PROMPT_TEXT"
  echo "--auth-type"
  echo "openai"
  echo "--openai-base-url"
  echo "https://api.deepseek.com"
  echo "--approval-mode"
  echo "yolo"
  echo "--model"
  echo "deepseek-v4-flash"
  echo "--output-format"
  echo "json"
  echo "--max-session-turns"
  echo "24"
  echo "--max-wall-time"
  echo "120s"
} > "$PHASE16_RUN_DIR/exact-argv.txt"

SANDBOX_HOME="$PHASE16_WORKSPACE/.sandbox-home"
mkdir -p "$SANDBOX_HOME"
set +e
bwrap \
  --unshare-all --share-net \
  --die-with-parent --new-session \
  --ro-bind / / \
  --bind "$PHASE16_WORKSPACE" "$PHASE16_WORKSPACE" \
  --bind "$SANDBOX_HOME" "$SANDBOX_HOME" \
  --tmpfs /etc \
  --ro-bind "$PHASE16_HOSTS_FILE" /etc/hosts \
  --ro-bind "$PHASE16_RESOLV_EMPTY" /etc/resolv.conf \
  --dev /dev --proc /proc \
  --chdir "$PHASE16_WORKSPACE" --clearenv \
  --setenv HOME "$SANDBOX_HOME" \
  --setenv PATH /usr/bin:/bin \
  --setenv DEEPSEEK_API_KEY "$DEEPSEEK_API_KEY" \
  -- "$PHASE16_QWEN_BIN" \
    -p "$PROMPT_TEXT" \
    --auth-type openai \
    --openai-base-url https://api.deepseek.com \
    --approval-mode yolo \
    --model deepseek-v4-flash \
    --output-format json \
    --max-session-turns 24 \
    --max-wall-time 120s \
  > "$PHASE16_RUN_DIR/qwen.stdout" 2> "$PHASE16_RUN_DIR/qwen.stderr"
QWEN_EC=$?
set -e
# Write qwen-result.env immediately so the outer orchestrator can observe exit
# even if post-Qwen verify hangs and is later reaped.
echo "qwen_exit=$QWEN_EC" > "$PHASE16_RUN_DIR/qwen-result.env"
if [ "$QWEN_EC" -ne 0 ]; then
  echo "qwen_launch_failed exit=$QWEN_EC" > "$PHASE16_RUN_DIR/fatal.txt"
  exit 4
fi

for stream in qwen.stdout qwen.stderr; do
  sed -E 's/sk-[A-Za-z0-9_-]+/[REDACTED]/g' "$PHASE16_RUN_DIR/$stream" > "$PHASE16_RUN_DIR/${stream}.redacted" || true
done

# Bounded post-Qwen verify (host may also reap on budget; do not hang forever).
set +e
timeout 40s bash -c 'cd "$0" && dotnet build Tuning.T01.slnx --no-incremental' "$PHASE16_WORKSPACE" \
  > "$PHASE16_RUN_DIR/verify-build.log" 2>&1
BUILD_EC=$?
timeout 40s bash -c 'cd "$0" && dotnet test Tuning.T01.slnx --no-build' "$PHASE16_WORKSPACE" \
  >> "$PHASE16_RUN_DIR/verify-build.log" 2>&1
TEST_EC=$?
set -e
{
  echo "build_exit=$BUILD_EC"
  echo "test_exit=$TEST_EC"
} > "$PHASE16_RUN_DIR/verify-result.env"

sleep 0.2
EOS
chmod +x "$INNER"

export DEEPSEEK_API_KEY

set +e
launch_netns_inner "$INNER" "inner_qualification"
# launch_netns_inner always returns 0 after wait/reap (or launch failure).
# Real unshare/inner status lives only in INNER_WAIT_EXIT — never rely on $?
# from the function after a non-zero child (bash sticky set -e abort; session
# m3q-20260724T072341Z-8f567943 lost balance-after for this reason).
INNER_EC="${INNER_WAIT_EXIT:-1}"
set -e
if [ -f "$RUN_DIR/qwen-result.env" ]; then
  CANDIDATE_EXECUTION=YES
  QWEN_EXIT_SOURCE=current_run_dir
fi
record_execution_state

# --- Always-run finalization path (balance, workspace, cleanup) ---
# Must run after every current-session outcome: Qwen exit 0/non-zero, unshare
# non-zero, outer reap timeout, or launch failure after key consumption.
set +e
curl -sS --max-time 15 \
  -H "Authorization: Bearer ${DEEPSEEK_API_KEY}" \
  "https://api.deepseek.com/user/balance" > "$RUN_DIR/balance-after.json" 2>"$RUN_DIR/balance-after.err"
BALANCE_AFTER_EC=$?
set -e
unset DEEPSEEK_API_KEY
export DEEPSEEK_API_KEY=
log_step "balance_after" "$BALANCE_AFTER_EC"

{
  echo "revoke_attempted=console_required"
  echo "one_shot_file_deleted_at_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "balance_after_exit=$BALANCE_AFTER_EC"
} >> "$KEY_LIFECYCLE"

ORPHAN="NO"
pgrep -af "qwen|bwrap|unshare" 2>/dev/null | grep -F "workspace-$SESSION_ID" >/dev/null 2>&1 && ORPHAN="YES" || true
{
  echo "orphan_detected=$ORPHAN"
  echo "cleanup_status=reaped"
  echo "cleanup_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
} > "$RUN_DIR/cleanup.env"
rm -f /tmp/phase16-ns-pid /tmp/phase16-ns-ready

# Host-side TC-T01 workspace result (independent of inner hang/reap).
TC_T01_VERIFIED="$(record_workspace_tc_t01_result)"
log_step "workspace_tc_t01_check" 0

cp "$DNS_ROOT/consistency-check.env" "$SESSION_ROOT/dns-consistency.env"
cp "$RUN_DIR/exact-argv.txt" "$SESSION_ROOT/exact-argv.txt" 2>/dev/null || true
cp "$RUN_DIR/qwen-result.env" "$SESSION_ROOT/qwen-result.env" 2>/dev/null || true
cp "$RUN_DIR/verify-result.env" "$SESSION_ROOT/verify-result.env" 2>/dev/null || true
cp "$RUN_DIR/netns-session.env" "$SESSION_ROOT/netns-session.env" 2>/dev/null || true
cp "$RUN_DIR/reap.env" "$SESSION_ROOT/reap.env" 2>/dev/null || true
cp "$RUN_DIR/cleanup.env" "$SESSION_ROOT/cleanup.env" 2>/dev/null || true
cp "$RUN_DIR/balance-after.json" "$SESSION_ROOT/balance-after.json" 2>/dev/null || true
cp "$RUN_DIR/balance-before.json" "$SESSION_ROOT/balance-before.json" 2>/dev/null || true

if [ -f "$RUN_DIR/fatal.txt" ]; then
  if [ "$KEY_CONSUMED" = "YES" ] && [ "$CANDIDATE_EXECUTION" != "YES" ]; then
    stop_with_no_provider_execution "$(cat "$RUN_DIR/fatal.txt")"
  fi
  stop_with "$(cat "$RUN_DIR/fatal.txt")"
fi

QWEN_EXIT="$(resolve_current_session_qwen_exit)"
if [ -z "$QWEN_EXIT" ]; then
  if [ "$KEY_CONSUMED" = "YES" ] && [ "$PROVIDER_LAUNCH_ATTEMPTED" = "YES" ]; then
    stop_with_no_provider_execution "qwen_result_missing_after_inner_wait"
  fi
  stop_with "qwen_result_missing_after_inner_wait"
fi
QWEN_EXIT_SOURCE=current_run_dir
CANDIDATE_EXECUTION=YES
record_execution_state
[ "${QWEN_EXIT:-1}" -eq 0 ] || stop_with "qwen launch failed exit=${QWEN_EXIT:-unknown}"

# GO requires Qwen exit 0 AND verified TC-T01 workspace rename (write-capable path).
if [ "$TC_T01_VERIFIED" != "YES" ]; then
  echo "BINDING_VERDICT=GO" >> "$DNS_ROOT/summary.env"
  {
    echo "QUALIFICATION_VERDICT=NO-GO"
    echo "STOP_REASON=tc_t01_workspace_change_not_verified"
    echo "qwen_exit=$QWEN_EXIT"
    echo "tc_t01_rename_verified=$TC_T01_VERIFIED"
    echo "balance_after_exit=$BALANCE_AFTER_EC"
    echo "session_end_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo "evidence_class=observational"
    echo "comparative_claims=FORBIDDEN"
  } >> "$SESSION_ROOT/summary.env"
  log_step "qualification_verdict_nogo_workspace" 1
  echo "M3 qualification NO-GO (workspace): $SESSION_ID" >&2
  echo "SESSION_ID=$SESSION_ID"
  exit 1
fi

# Inner non-zero after forced reap is acceptable if Qwen+workspace already OK,
# but clean exit is preferred; record status and continue to GO only when verified.
if [ "$INNER_EC" -ne 0 ] && [ "$INNER_EC" -ne 124 ]; then
  # Unexpected inner failure after verified rename: still observational caution.
  stop_with "inner qualification failed exit=$INNER_EC after verified workspace"
fi

echo "BINDING_VERDICT=GO" >> "$DNS_ROOT/summary.env"
{
  echo "QUALIFICATION_VERDICT=GO"
  echo "STOP_REASON="
  echo "qwen_exit=$QWEN_EXIT"
  echo "tc_t01_rename_verified=YES"
  echo "balance_after_exit=$BALANCE_AFTER_EC"
  echo "session_end_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "evidence_class=observational"
  echo "comparative_claims=FORBIDDEN"
} >> "$SESSION_ROOT/summary.env"

echo "M3 qualification complete: $SESSION_ID"
echo "SESSION_ID=$SESSION_ID"
