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

log_step() {
  local step="$1"
  local code="$2"
  printf '%s step=%s exit=%s utc=%s\n' "$SESSION_ID" "$step" "$code" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$SESSION_ROOT/commands-ledger.txt"
}

stop_with() {
  STOP_REASON="$1"
  echo "STOP_REASON=$STOP_REASON" >> "$SESSION_ROOT/summary.env"
  echo "QUALIFICATION_VERDICT=NO-GO" >> "$SESSION_ROOT/summary.env"
  echo "session_end_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$SESSION_ROOT/summary.env"
  echo "M3 qualification STOP: $STOP_REASON" >&2
  exit 1
}

mkdir -p "$SESSION_ROOT" "$DNS_ROOT" "$PROBE_DIR" "$RUN_DIR" "$CRED_DIR" "$(dirname "$WORKSPACE")"
chmod 700 "$CRED_DIR" "$SESSION_ROOT" "$DNS_ROOT"
: > "$SESSION_ROOT/commands-ledger.txt"
{
  echo "SESSION_ID=$SESSION_ID"
  echo "TASK_ID=TC-T01"
  echo "CANDIDATE_SLUG=qwen-code"
  echo "EVIDENCE_CLASS=observational"
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
[ -n "$DEEPSEEK_API_KEY" ] || stop_with "sub-key one-shot file was empty"
{
  echo "key_source=phase16_one_shot_file"
  echo "key_file_path=$SUBKEY_ONCE"
  echo "created_at_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "one_shot_file_deleted_at_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "value_disclosed=NO"
} > "$KEY_LIFECYCLE"
log_step "credential_load_one_shot" 0

# --- Cost tracking pre-check ---
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
[ "$BALANCE_BEFORE_EC" -eq 0 ] || stop_with "balance pre-check failed; cannot prove cost tracking"

# --- Inner netns proof + Qwen launch (credential via env; never written to disk) ---
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
  echo "--approval-mode"
  echo "plan"
  echo "--model"
  echo "deepseek-v4-flash"
  echo "--output-format"
  echo "json"
  echo "--max-session-turns"
  echo "5"
  echo "--max-wall-time"
  echo "60s"
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
  --ro-bind "$PHASE16_HOSTS_FILE" /etc/hosts \
  --ro-bind "$PHASE16_RESOLV_EMPTY" /etc/resolv.conf \
  --dev /dev --proc /proc \
  --chdir "$PHASE16_WORKSPACE" --clearenv \
  --setenv HOME "$SANDBOX_HOME" \
  --setenv PATH /usr/bin:/bin \
  --setenv DEEPSEEK_API_KEY "$DEEPSEEK_API_KEY" \
  -- "$PHASE16_QWEN_BIN" \
    -p "$PROMPT_TEXT" \
    --approval-mode plan \
    --model deepseek-v4-flash \
    --output-format json \
    --max-session-turns 5 \
    --max-wall-time 60s \
  > "$PHASE16_RUN_DIR/qwen.stdout" 2> "$PHASE16_RUN_DIR/qwen.stderr"
QWEN_EC=$?
set -e
echo "qwen_exit=$QWEN_EC" > "$PHASE16_RUN_DIR/qwen-result.env"

for stream in qwen.stdout qwen.stderr; do
  sed -E 's/sk-[A-Za-z0-9_-]+/[REDACTED]/g' "$PHASE16_RUN_DIR/$stream" > "$PHASE16_RUN_DIR/${stream}.redacted" || true
done

set +e
( cd "$PHASE16_WORKSPACE" && dotnet build Tuning.T01.slnx --no-incremental > "$PHASE16_RUN_DIR/verify-build.log" 2>&1 )
BUILD_EC=$?
( cd "$PHASE16_WORKSPACE" && dotnet test Tuning.T01.slnx --no-build >> "$PHASE16_RUN_DIR/verify-build.log" 2>&1 )
TEST_EC=$?
set -e
{
  echo "build_exit=$BUILD_EC"
  echo "test_exit=$TEST_EC"
} > "$PHASE16_RUN_DIR/verify-result.env"

sleep 0.5
EOS
chmod +x "$INNER"

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
export DEEPSEEK_API_KEY

rm -f /tmp/phase16-ns-pid /tmp/phase16-ns-ready
unshare --user --map-root-user --net --mount --fork --pid --mount-proc bash "$INNER" \
  >"$RUN_DIR/unshare.stdout" 2>"$RUN_DIR/unshare.stderr" &
UNSHARE_PID=$!
# slirp4netns requires the host-visible PID from unshare --fork, not inner $$ (PID ns = 1).
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
[ -f /tmp/phase16-ns-ready ] || stop_with "inner netns never signaled ready for slirp4netns"
slirp4netns --configure --mtu=65520 --disable-host-loopback "$NSPID" tap0 \
  >"$RUN_DIR/slirp4netns.stdout" 2>"$RUN_DIR/slirp4netns.stderr" &
SLIRP_PID=$!
SLIRP_ATTACH_EC=1
for _ in $(seq 1 30); do
  if [ -s "$RUN_DIR/slirp4netns.stderr" ]; then
    if grep -q 'sent tapfd' "$RUN_DIR/slirp4netns.stderr" 2>/dev/null; then
      SLIRP_ATTACH_EC=0
      break
    fi
    if grep -qE 'failed|Permission denied' "$RUN_DIR/slirp4netns.stderr" 2>/dev/null; then
      stop_with "slirp4netns attach failed (see slirp4netns.stderr)"
    fi
  fi
  if ! kill -0 "$SLIRP_PID" 2>/dev/null; then
    stop_with "slirp4netns exited before attach completed"
  fi
  sleep 0.1
done
log_step "slirp4netns_attach" "$SLIRP_ATTACH_EC"
[ "$SLIRP_ATTACH_EC" -eq 0 ] || stop_with "slirp4netns attach did not confirm tapfd handoff"

set +e
wait "$UNSHARE_PID"
INNER_EC=$?
wait "$SLIRP_PID" 2>/dev/null || true
set -e
log_step "inner_qualification" "$INNER_EC"

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
} >> "$KEY_LIFECYCLE"

ORPHAN="NO"
pgrep -af "qwen-code.*workspace-$SESSION_ID" >/dev/null 2>&1 && ORPHAN="YES" || true
echo "orphan_detected=$ORPHAN" > "$RUN_DIR/cleanup.env"
rm -f /tmp/phase16-ns-pid /tmp/phase16-ns-ready

cp "$DNS_ROOT/consistency-check.env" "$SESSION_ROOT/dns-consistency.env"
cp "$RUN_DIR/exact-argv.txt" "$SESSION_ROOT/exact-argv.txt" 2>/dev/null || true
cp "$RUN_DIR/qwen-result.env" "$SESSION_ROOT/qwen-result.env" 2>/dev/null || true
cp "$RUN_DIR/verify-result.env" "$SESSION_ROOT/verify-result.env" 2>/dev/null || true
cp "$RUN_DIR/netns-session.env" "$SESSION_ROOT/netns-session.env" 2>/dev/null || true

if [ -f "$RUN_DIR/fatal.txt" ]; then
  stop_with "$(cat "$RUN_DIR/fatal.txt")"
fi
[ "$INNER_EC" -eq 0 ] || stop_with "inner qualification failed exit=$INNER_EC"

echo "BINDING_VERDICT=GO" >> "$DNS_ROOT/summary.env"
{
  echo "QUALIFICATION_VERDICT=GO"
  echo "STOP_REASON="
  echo "session_end_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "evidence_class=observational"
  echo "comparative_claims=FORBIDDEN"
} >> "$SESSION_ROOT/summary.env"

echo "M3 qualification complete: $SESSION_ID"
echo "SESSION_ID=$SESSION_ID"
