#!/bin/bash
# Egress firewall for environment A. Adapted from Anthropic's reference
# init-firewall.sh (github.com/anthropics/claude-code, .devcontainer/, commit 52dd3a1).
# Differences:
#   - no GitHub ranges, no outbound SSH, no host network;
#   - only the hosts this example needs, HTTPS only;
#   - a local dnsmasq resolves only those hosts and adds each address it returns
#     to the allowlist, so CDN-backed hosts (api.nuget.org) keep working when
#     their addresses rotate, and other names do not resolve at all (no DNS
#     queries leave the container except for allowed names).
set -euo pipefail
IFS=$'\n\t'

ALLOWED_DOMAINS=(
  # Claude Code: API and sign-in
  api.anthropic.com
  claude.ai
  console.anthropic.com
  platform.claude.com
  # Restore packages
  api.nuget.org
  registry.npmjs.org
)

# Keep Docker's embedded DNS (127.0.0.11) working before flushing.
DOCKER_DNS_RULES=$(iptables-save -t nat | grep "127\.0\.0\.11" || true)

iptables -F
iptables -X
iptables -t nat -F
iptables -t nat -X
iptables -t mangle -F
iptables -t mangle -X
ipset destroy allowed-domains 2>/dev/null || true

if [ -n "$DOCKER_DNS_RULES" ]; then
  iptables -t nat -N DOCKER_OUTPUT 2>/dev/null || true
  iptables -t nat -N DOCKER_POSTROUTING 2>/dev/null || true
  echo "$DOCKER_DNS_RULES" | xargs -L 1 iptables -t nat
fi

ipset create allowed-domains hash:ip

# The container's own resolver (Docker's embedded DNS or the host's), kept from
# the first run before resolv.conf points at dnsmasq.
[ -f /etc/resolv.conf.upstream ] || cp /etc/resolv.conf /etc/resolv.conf.upstream
UPSTREAM_DNS=$(awk '/^nameserver/ {print $2; exit}' /etc/resolv.conf.upstream)
if [ -z "$UPSTREAM_DNS" ]; then
  echo "ERROR: no upstream DNS server in /etc/resolv.conf"
  exit 1
fi

# A resolver that answers only for allowed names and records their addresses.
{
  echo "no-resolv"
  echo "listen-address=127.0.0.1"
  echo "bind-interfaces"
  echo "cache-size=0"
  for domain in "${ALLOWED_DOMAINS[@]}"; do
    echo "server=/${domain}/${UPSTREAM_DNS}"
    echo "ipset=/${domain}/allowed-domains"
  done
} > /etc/dnsmasq.d/allowlist.conf
pkill -x dnsmasq 2>/dev/null || true
dnsmasq --conf-dir=/etc/dnsmasq.d --user=root
echo "nameserver 127.0.0.1" > /etc/resolv.conf

# Only loopback (dnsmasq and Docker's DNS) and HTTPS to recorded addresses.
iptables -A INPUT -i lo -j ACCEPT
iptables -A OUTPUT -o lo -j ACCEPT
# DNS to the upstream resolver only from root (dnsmasq, and Docker's embedded
# DNS on user-defined networks). The agent runs as an unprivileged user, so its
# processes can query only dnsmasq, which resolves only the allowed names.
iptables -A OUTPUT -p udp --dport 53 -m owner --uid-owner 0 -j ACCEPT
iptables -A OUTPUT -p tcp --dport 53 -m owner --uid-owner 0 -j ACCEPT
iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT DROP
iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -p tcp --dport 443 -m set --match-set allowed-domains dst -j ACCEPT
iptables -A OUTPUT -j REJECT --reject-with icmp-admin-prohibited

# Self-check: an allowed host answers, others neither resolve nor connect.
if curl --silent --connect-timeout 5 https://example.com >/dev/null; then
  echo "ERROR: firewall check failed, example.com is reachable"
  exit 1
fi
if ! curl --silent --retry 3 --retry-all-errors --connect-timeout 5 --output /dev/null https://api.nuget.org/v3/index.json; then
  echo "ERROR: firewall check failed, api.nuget.org is not reachable"
  exit 1
fi
echo "Firewall ready: outbound HTTPS only to $(IFS=' '; echo "${ALLOWED_DOMAINS[*]}")"
