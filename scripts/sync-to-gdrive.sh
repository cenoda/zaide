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

rclone sync . "${DEST}" \
    --exclude-if-present .rcloneignore \
    --exclude ".git/" \
    --delete-excluded \
    --progress \
    2>&1 | tee -a "${LOG_FILE}"

echo "[$(date '+%Y-%m-%d %H:%M:%S')] Done."
