#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE="${WORKSPACE:-${SCRIPT_DIR}/workspace}"

if [[ ! -f "${WORKSPACE}/Tuning.T01.slnx" ]]; then
  echo "verify.sh: workspace not found at ${WORKSPACE}" >&2
  exit 1
fi

cd "${WORKSPACE}"

dotnet build Tuning.T01.slnx --no-incremental
dotnet test Tuning.T01.slnx --no-build

if grep -R --include='*.cs' -n 'FetchData' .; then
  echo "verify.sh: FetchData references remain in source" >&2
  exit 1
fi

echo "verify.sh: build, tests, and FetchData absence checks passed"
