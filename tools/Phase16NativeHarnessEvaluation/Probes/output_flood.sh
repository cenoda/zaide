#!/bin/sh
# Repository-owned probe: emit more than 64 KiB on stdout.
i=0
while [ "$i" -lt 7000 ]; do
  printf 'LINE-%04d-abcdefghijklmnopqrstuvwxyz\n' "$i"
  i=$((i + 1))
done
