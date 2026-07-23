#!/bin/sh
# Repository-owned probe: attempt to read through a traversal path.
target="$1"
if [ -z "$target" ]; then
  echo "missing target" >&2
  exit 2
fi
if cat "$target" >/dev/null 2>&1; then
  echo "traversal_read=allowed"
  exit 0
fi
echo "traversal_read=denied"
exit 1
