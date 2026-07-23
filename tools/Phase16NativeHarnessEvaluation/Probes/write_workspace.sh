#!/bin/sh
# Repository-owned probe: write inside workspace when path argument is provided.
target="$1"
if [ -z "$target" ]; then
  echo "missing target" >&2
  exit 2
fi
printf 'dirty-write\n' > "$target"
echo "wrote=$target"
