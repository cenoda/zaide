#!/bin/sh
# Repository-owned probe: attempt to write outside the workspace via traversal.
target="$1"
if [ -z "$target" ]; then
  echo "missing target" >&2
  exit 2
fi
printf 'escape\n' > "$target" 2>/dev/null
status=$?
echo "traversal_write_exit=$status"
