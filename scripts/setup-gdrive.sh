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

# ── 2. Configure Google Drive remote ─────────────────────────────────────
if rclone listremotes 2>/dev/null | grep -q "^gdrive:"; then
    echo "✅ gdrive remote already configured"
else
    echo ""
    echo "──────────────────────────────────────────────────────────"
    echo "  You need to link your Google Drive."
    echo "  rclone will open your browser for OAuth."
    echo ""
    echo "  → Name the remote:  gdrive"
    echo "  → Type:             drive"
    echo "  → Press Enter for defaults on everything else"
    echo "──────────────────────────────────────────────────────────"
    echo ""
    read -rp "Press Enter to start rclone config... " _
    rclone config create gdrive drive
    echo "✅ gdrive remote configured"
fi

# ── 3. Test connection ───────────────────────────────────────────────────
echo ""
echo "Testing connection..."
if rclone lsd gdrive: &>/dev/null; then
    echo "✅ Google Drive reachable"
else
    echo "❌ Could not list Google Drive root. Check your config."
    exit 1
fi

# ── 4. Create target folder ──────────────────────────────────────────────
rclone mkdir gdrive:zaide-backup 2>/dev/null || true
echo "✅ Target folder gdrive:zaide-backup ready"

# ── 5. Set up cron (every 30 min) ────────────────────────────────────────
SCRIPT="/home/cenoda/zaide/scripts/sync-to-gdrive.sh"
CRON_JOB="*/30 * * * * ${SCRIPT} >> /tmp/zaide-gdrive-cron.log 2>&1"

if crontab -l 2>/dev/null | grep -qF "${SCRIPT}"; then
    echo "✅ Cron job already present"
else
    (crontab -l 2>/dev/null; echo "${CRON_JOB}") | crontab -
    echo "✅ Cron job added (every 30 min)"
fi

# ── 6. First sync ────────────────────────────────────────────────────────
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
