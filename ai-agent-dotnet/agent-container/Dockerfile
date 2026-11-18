# Environment A: an unprivileged container for the AI coding agent.
# Adapted from Anthropic's reference dev container
# (github.com/anthropics/claude-code, .devcontainer/, commit 52dd3a1), reduced to
# what this example needs: .NET 10, Node.js 24, Claude Code and an egress firewall.
# It holds no cloud credentials, no SSH keys and no GitHub token.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet

FROM node:24-bookworm

RUN apt-get update && apt-get install -y --no-install-recommends \
    bubblewrap \
    ca-certificates \
    dnsmasq-base \
    dnsutils \
    git \
    procps \
    iproute2 \
    ipset \
    iptables \
    jq \
    libicu72 \
    socat \
    sudo \
  && apt-get clean && rm -rf /var/lib/apt/lists/* \
  && mkdir -p /etc/dnsmasq.d

# The .NET SDK, copied from Microsoft's official multi-architecture image
COPY --from=dotnet /usr/share/dotnet /usr/share/dotnet
RUN ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Claude Code, pinned. Non-essential traffic (telemetry, error reporting) off,
# so the firewall only needs the API and sign-in hosts.
ARG CLAUDE_CODE_VERSION=2.1.327
ENV NPM_CONFIG_PREFIX=/usr/local/share/npm-global \
    PATH=$PATH:/usr/local/share/npm-global/bin \
    CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC=1 \
    DEVCONTAINER=true
RUN mkdir -p /usr/local/share/npm-global /workspace /home/node/.claude \
  && chown -R node:node /usr/local/share/npm-global /workspace /home/node/.claude
USER node
RUN npm install -g @anthropic-ai/claude-code@${CLAUDE_CODE_VERSION}

# The firewall runs as root at start; the agent user may run only that script with sudo.
USER root
COPY init-firewall.sh /usr/local/bin/init-firewall.sh

# Claude Code's own sandbox (bubblewrap) cannot nest here: Docker's default
# profile denies unprivileged user namespaces. This container is the sandbox
# instead, so the agent starts with this read-only layer, passed with --settings,
# which disables only the inner sandbox. The project's permission rules still apply.
COPY claude-settings.json /etc/claude-agent/settings.json
RUN chmod 0444 /etc/claude-agent/settings.json
RUN chmod 0755 /usr/local/bin/init-firewall.sh \
  && echo "node ALL=(root) NOPASSWD: /usr/local/bin/init-firewall.sh" > /etc/sudoers.d/node-firewall \
  && chmod 0440 /etc/sudoers.d/node-firewall

# Commits made by the agent are attributable to it, not to a person.
USER node
RUN git config --global user.name "AI coding agent (environment A)" \
  && git config --global user.email "agent@invalid" \
  && git config --global --add safe.directory /workspace

WORKDIR /workspace/ai-agent-dotnet
