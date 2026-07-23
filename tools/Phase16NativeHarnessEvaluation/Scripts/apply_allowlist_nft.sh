#!/usr/bin/env bash
# Phase 16 — ephemeral nft allowlist for one IPv4:443 destination.
set -euo pipefail
bound_ipv4="${1:?bound IPv4 required}"
nft flush ruleset || true
nft add table inet phase16_egress
nft 'add chain inet phase16_egress output { type filter hook output priority 0; policy drop; }'
nft 'add chain inet phase16_egress input { type filter hook input priority 0; policy drop; }'
nft 'add rule inet phase16_egress output oif lo accept'
nft 'add rule inet phase16_egress input iif lo accept'
nft 'add rule inet phase16_egress input ct state established,related accept'
nft 'add rule inet phase16_egress output ct state established,related accept'
nft add rule inet phase16_egress output ip daddr "${bound_ipv4}/32" tcp dport 443 accept
nft 'add rule inet phase16_egress output counter drop'
nft list ruleset
