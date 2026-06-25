#!/usr/bin/env bash
set -euo pipefail

# ── Sync Zaide working tree to Google Drive ───────────────────────────────
# Uses rclone to mirror the repo (minus build artifacts) into a Google
# Drive folder.  Designed to be called from cron or a git hook.
#
# Setup (one-time):  rclone config
#   → choose "New remote" → name it "gdrive" → type "drive"
#   → follow the OAuth prompts (needs a browser)
#
# Then edit the DEST line below if you want a different folder name.

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="gdrive:zaide-backup"
LOG_FILE="/tmp/zaide-gdrive-sync.log"

echo "[$(date '+%Y-%m-%d %H:%M:%S')] Syncing ${REPO_ROOT} → ${DEST}"

cd "${REPO_ROOT}"

# --tpslimit / --tpslimit-burst prevent Google rate-limit errors.
# --drive-pacer-min-sleep adds a safety cushion between API calls.
rclone sync . "${DEST}" \
    --exclude ".git/" \
    --exclude "bin/" \
    --exclude "obj/" \
    --exclude ".vs/" \
    --exclude ".idea/" \
    --exclude ".vscode/" \
    --exclude "*.suo" \
    --exclude "*.user" \
    --exclude ".DS_Store" \
    --exclude "Thumbs.db" \
    --tpslimit 1 \
    --tpslimit-burst 1 \
    --drive-pacer-min-sleep 2s \
    --progress \
    2>&1 | tee -a "${LOG_FILE}"

echo "[$(date '+%Y-%m-%d %H:%M:%S')] Done."
