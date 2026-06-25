#!/usr/bin/env bash
set -euo pipefail

echo "╔══════════════════════════════════════════════════════════╗"
echo "║   Zaide → Google Drive sync setup                      ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# ── 1. Check rclone ──────────────────────────────────────────────────────
if ! command -v rclone &>/dev/null; then
    echo "❌ rclone not found. Install it: https://rclone.org/install/"
    exit 1
fi
echo "✅ rclone $(rclone version --check 2>&1 | head -1)"

# ── 2. Google API Client ID (fixes rate limits) ──────────────────────────
CONFIG_FILE="${HOME}/.config/rclone/rclone.conf"
NEED_CLIENT_ID=false

if grep -q "^client_id = $" "${CONFIG_FILE}" 2>/dev/null; then
    NEED_CLIENT_ID=true
elif ! grep -q "^client_id" "${CONFIG_FILE}" 2>/dev/null; then
    NEED_CLIENT_ID=true
fi

if ${NEED_CLIENT_ID}; then
    echo ""
    echo "──────────────────────────────────────────────────────────"
    echo "  ⚠️  Without a personal client ID, Google rate-limits"
    echo "     rclone to ~1 request/minute — syncing is impossible."
    echo ""
    echo "  Fix it in 2 minutes (one-time):"
    echo ""
    echo "  1. Open: https://console.cloud.google.com/apis/credentials"
    echo "  2. Create a project (or use an existing one)"
    echo "  3. Click '+ CREATE CREDENTIALS' → 'OAuth client ID'"
    echo "  4. Choose 'Desktop app', give it a name (e.g. 'rclone')"
    echo "  5. Copy the Client ID and Client Secret"
    echo "──────────────────────────────────────────────────────────"
    echo ""
    read -rp "Client ID: " CLIENT_ID
    read -rp "Client Secret: " CLIENT_SECRET

    if [[ -n "${CLIENT_ID}" && -n "${CLIENT_SECRET}" ]]; then
        rclone config update gdrive client_id="${CLIENT_ID}" client_secret="${CLIENT_SECRET}"
        echo "✅ Client ID saved"
    else
        echo "⚠️  Skipped — sync will be very slow without it"
    fi
fi

# ── 3. Configure Google Drive remote ─────────────────────────────────────
if rclone listremotes 2>/dev/null | grep -q "^gdrive:"; then
    echo "✅ gdrive remote already configured"
else
    echo ""
    echo "──────────────────────────────────────────────────────────"
    echo "  Link your Google Drive with rclone."
    echo ""
    echo "  → Name the remote:  gdrive"
    echo "  → Type:             drive"
    echo "  → Client ID/Secret: enter yours (leave blank for shared)"
    echo "  → Press Enter for defaults on everything else"
    echo "──────────────────────────────────────────────────────────"
    echo ""
    read -rp "Press Enter to start rclone config... " _
    rclone config create gdrive drive
    echo "✅ gdrive remote configured"
fi

# ── 4. Test connection ───────────────────────────────────────────────────
echo ""
echo "Testing Drive connection (this may fail if you skipped the client ID)..."
if rclone about gdrive: --tpslimit 1 --tpslimit-burst 1 --drive-pacer-min-sleep 2s &>/dev/null; then
    echo "✅ Google Drive reachable"
else
    echo "⚠️  Could not reach Drive (rate-limited). Continuing anyway."
fi

# ── 5. Create target folder ──────────────────────────────────────────────
rclone mkdir gdrive:zaide-backup 2>/dev/null || true
echo "✅ Target folder gdrive:zaide-backup ready"

# ── 6. Set up cron (every 30 min) ────────────────────────────────────────
SCRIPT="/home/cenoda/zaide/scripts/sync-to-gdrive.sh"
CRON_JOB="*/30 * * * * ${SCRIPT} >> /tmp/zaide-gdrive-cron.log 2>&1"

if crontab -l 2>/dev/null | grep -qF "${SCRIPT}"; then
    echo "✅ Cron job already present"
else
    (crontab -l 2>/dev/null; echo "${CRON_JOB}") | crontab -
    echo "✅ Cron job added (every 30 min)"
fi

# ── 7. First sync ────────────────────────────────────────────────────────
echo ""
echo "──────────────────────────────────────────────────────────"
read -rp "Run first sync now? [Y/n] " answer
if [[ "${answer,,}" != "n" ]]; then
    "${SCRIPT}"
fi

echo ""
echo "╔══════════════════════════════════════════════════════════╗"
echo "║   All set! Every git commit syncs instantly.           ║"
echo "║   Cron syncs every 30 min as safety net.               ║"
echo "║   Files appear in Drive → zaide-backup/                ║"
echo "╚══════════════════════════════════════════════════════════╝"
