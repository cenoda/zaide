#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $(basename "$0") <target-workspace-directory>" >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE="${SCRIPT_DIR}/workspace"
TARGET="$(mkdir -p "$1" && cd "$1" && pwd)"

if [[ ! -d "${SOURCE}" ]]; then
  echo "materialize-fixture.sh: fixture workspace not found at ${SOURCE}" >&2
  exit 1
fi

cp -a "${SOURCE}/." "${TARGET}/"

echo "Materialized TC-T01 fixture to: ${TARGET}"
