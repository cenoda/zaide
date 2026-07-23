#!/bin/sh
# Repository-owned probe: attempt to write outside the workspace bind.
printf 'host-escape\n' > /phase16-host-escape-probe.txt 2>/dev/null
status=$?
echo "host_write_exit=$status"
