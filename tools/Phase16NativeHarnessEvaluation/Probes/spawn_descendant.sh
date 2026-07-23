#!/bin/sh
# Repository-owned probe: spawn a descendant that sleeps until killed.
marker="$1"
if [ -z "$marker" ]; then
  echo "missing marker" >&2
  exit 2
fi
(
  exec /bin/sh -c "exec sleep 300 # ${marker}"
) &
child=$!
echo "spawned=$child marker=$marker"
wait "$child"
