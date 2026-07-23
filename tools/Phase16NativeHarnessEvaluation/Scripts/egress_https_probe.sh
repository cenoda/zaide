#!/usr/bin/env bash
# Phase 16 M3 egress HTTPS probe — repository-controlled, no credentials in probe itself.
set -uo pipefail
label="${1:?label}"
url="${2:?url}"
out_dir="${PHASE16_EGRESS_OUT:?PHASE16_EGRESS_OUT required}"
mkdir -p "$out_dir"
ts=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
body="$out_dir/${label}.body"
err="$out_dir/${label}.err"
meta="$out_dir/${label}.meta"
set +e
curl -sS \
  --http1.1 \
  --proto '=https' \
  --tlsv1.2 \
  --max-time 15 \
  --connect-timeout 10 \
  -o "$body" \
  -w "http_code=%{http_code}\nremote_ip=%{remote_ip}\nremote_port=%{remote_port}\nssl_verify_result=%{ssl_verify_result}\ntime_namelookup=%{time_namelookup}\ntime_connect=%{time_connect}\ntime_appconnect=%{time_appconnect}\ntime_total=%{time_total}\nnum_connects=%{num_connects}\n" \
  "$url" >"$meta.curl" 2>"$err"
ec=$?
set -e
{
  echo "label=$label"
  echo "url=$url"
  echo "timestamp_utc=$ts"
  echo "curl_exit=$ec"
  cat "$meta.curl"
  if [ -f "$body" ]; then
    echo "body_bytes=$(wc -c < "$body" | tr -d ' ')"
    echo -n "body_sample_redacted="
    head -c 120 "$body" | tr -cd '[:print:]\n' | tr '\n' ' ' | head -c 120
    echo
  else
    echo "body_bytes=0"
  fi
  if [ -s "$err" ]; then
    echo -n "stderr_redacted="
    tr -cd '[:print:]\n' < "$err" | head -c 400
    echo
  fi
} > "$meta"
echo "PROBE_RESULT label=$label curl_exit=$ec"
exit "$ec"
