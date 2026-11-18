# Agent workflow, permissions and recording plan

How to give an AI coding agent the document storage task, what it may do on
its own, where a human takes over, and how to record a real session. The
security reasoning behind each step is in [security.md](security.md).

## The prompt

Start the agent at the baseline commit of this example (`946472e`, "feat: add the ai-agent-dotnet baseline": empty API,
empty test projects, `envilder.json`, the pinned CLI and skill) and give it
this task:

```text
Implement document upload and download using Amazon S3 in this .NET 10 project.

Requirements:
- Follow TDD.
- Start with failing tests.
- Follow the existing project architecture and conventions.
- Use Testcontainers and LocalStack for integration tests.
- Inspect envilder.json and use the existing Envilder skill.
- Do not create .env files.
- Do not hardcode credentials.
- Do not copy secrets into prompts, logs, or source files.
- Run unit tests without cloud credentials.
- Validate the Envilder contract offline.
- Ask for authorization before any operation that resolves cloud secrets.
- Run integration tests using Envilder after authorization.
- Report all changes, executed commands, test results, and unresolved problems.
```

## Expected workflow

| Step | Who | What | Secret exposure |
|------|-----|------|-----------------|
| 1 | Agent | Reads the README, the testing conventions and the code | None |
| 2 | Agent | Reads `envilder.json`: `LOCALSTACK_AUTH_TOKEN` is the variable, `/envilder/development/localstack/authToken` its SSM path | Names and paths only |
| 3 | Agent | Loads the Envilder skill (automatically, or with `/envilder`) and classifies commands: `validate --offline` is offline, `exec` reads values | None |
| 4 | Agent | Separates secrets (the token) from plain settings (bucket name, S3 endpoint in tests) | None |
| 5 | Agent | Proposes the design, writes failing tests, implements, runs the unit tests | None |
| 6 | Agent | Runs `npx --no envilder validate --offline --map=envilder.json` | None |
| 7 | Agent | **Stops and asks** before `envilder exec` | |
| 8 | Human | Reviews the diff, especially tests, fixtures, `envilder.json` and anything under `.claude/` | |
| 9 | Human | Runs the integration tests through `envilder exec` (environment B), or pushes and approves the CI job in the `integration` environment | The test process and LocalStack get the token |
| 10 | Agent | Reads the result the human shares, fixes failures, reports | Test output only |

Step 9 is deliberately outside the agent. Approving the agent's own `exec`
request (step 7) is possible, but then the agent's unreviewed code runs with
the secret: only do it after step 8.

## Environment A: the agent

The agent needs, and gets, no cloud credentials and no push rights.

**Isolation.** Claude Code's OS sandbox works on macOS, Linux and WSL2 and is
not available on native Windows, where commands run unsandboxed
([sandboxing docs](https://code.claude.com/docs/en/sandboxing)). The sandbox
covers shell commands and their children, not the built-in file tools, MCP
servers or hooks. This example runs the agent in a container instead:
[`agent-container/`](../agent-container/), adapted from Anthropic's
[reference dev container](https://github.com/anthropics/claude-code/tree/main/.devcontainer).

| Property | How | Verified (2026-11-18, three runs) |
|----------|-----|-----------------------------------|
| No cloud credentials | Only the workspace is mounted: no `~/.aws`, no `AWS_*` variables, no AWS CLI | `AWS_*` variables 0, `~/.aws` absent, `envilder exec` exits `125` (`Could not load credentials from any providers`) |
| No push or GitHub access | No `~/.ssh`, no `gh`, no Git remote, GitHub not reachable | remotes 0, `api.github.com` blocked |
| No Docker | No Docker socket, so the agent cannot start LocalStack or inspect containers | socket absent |
| Egress allowlist | `init-firewall.sh`: HTTPS only to `api.anthropic.com`, `claude.ai`, `console.anthropic.com`, `platform.claude.com`, `api.nuget.org`, `registry.npmjs.org`. A local dnsmasq resolves only those names; only root can reach the upstream DNS | NuGet and npm 200; AWS SSM, STS, GitHub, example.com blocked; DNS for other names and direct DNS to 8.8.8.8 blocked |
| Agent cannot lift the firewall | The agent runs as `node`; `sudo` allows only the firewall script, which `node` cannot edit | `sudo` other commands denied, `iptables` denied, script not writable |
| Offline work possible | `npm ci`, `validate --offline`, unit tests | all exit 0 |

Claude Code's own sandbox (bubblewrap) cannot run inside this container:
Docker's default profile denies unprivileged user namespaces
(`bwrap: No permissions to create new namespace`). The container takes its
place, so the agent starts with a read-only settings layer
(`/etc/claude-agent/settings.json`, passed with `--settings`) that disables
only that inner sandbox; the project's permission rules still apply.

Why not the VS Code Dev Containers extension: by default it copies your
`.gitconfig` and forwards Git credentials and the SSH agent into the
container, which would hand the agent push rights. The plain `docker run`
below mounts exactly one folder.

**Run it** (PowerShell, from the repository root). Build the image once:

```powershell
docker build -t envilder-ai-agent:local ai-agent-dotnet/agent-container
```

Give the agent its own clone at the baseline commit, with no remote:

```powershell
git clone --no-hardlinks . ..\envilder-examples-agent-run; git -C ..\envilder-examples-agent-run checkout -b agent/document-storage 946472e; git -C ..\envilder-examples-agent-run remote remove origin
```

Start the agent (the first time, sign in to Claude Code when it asks; the
sign-in is kept in the `envilder-agent-claude` volume):

```powershell
docker run -it --rm --name envilder-agent --cap-add=NET_ADMIN --cap-add=NET_RAW -v "${PWD}\..\envilder-examples-agent-run:/workspace" -v envilder-agent-claude:/home/node/.claude envilder-ai-agent:local bash -c "sudo /usr/local/bin/init-firewall.sh && claude --settings /etc/claude-agent/settings.json"
```

`NET_ADMIN` and `NET_RAW` are needed by the firewall script, which runs as
root; the agent's own processes run as `node` without those capabilities.
The Claude Code sign-in token lives in that volume and is readable by the
agent: it is the agent's own identity, reachable only through the allowed
Anthropic hosts.

**Agent permissions.** [`.claude/settings.json`](../.claude/settings.json) is
committed and reviewed like code (it is under CODEOWNERS):

| Rule | Effect |
|------|--------|
| allow: `dotnet restore/build`, unit tests, `npm ci`, `validate --offline`, `git status/diff/log/add/commit` | Normal development without prompts |
| ask: `npx --no envilder exec`, `npm run test:integration`, integration `dotnet test`, edits to `envilder.json` and `.claude/` | A human decides each time |
| deny: reading `.env*`, `~/.aws`, `~/.ssh`; `aws`; unpinned `npx envilder`; `envilder --push`; `git push`; `gh`; `curl`, `wget`; `WebFetch` | Never |
| sandbox: enabled, `failIfUnavailable`, no unsandboxed retries, network allowlist `api.nuget.org`, `registry.npmjs.org`, strict | Claude Code refuses to start without a sandbox and blocks other destinations |

Rules are evaluated deny, then ask, then allow. Read and Edit deny rules also
apply to file commands Claude Code recognizes in Bash (`cat`, `head`, `sed`,
redirections) but not to scripts that open files themselves: that gap is why
the sandbox and the absence of credentials matter more than the rules
([permissions docs](https://code.claude.com/docs/en/permissions)).
`defaultMode` cannot be set to `auto` or `bypassPermissions` from project
settings.

**The skill.** Claude Code loads project skills from `.claude/skills/` in the
directory where the session starts and its parents up to the repository root,
and loads nested ones only when it touches files in that subdirectory
([skills docs](https://code.claude.com/docs/en/skills)). Start the session in
`ai-agent-dotnet/` so the Envilder skill is available from the first message.
For Codex, install the same skill into `.agents/skills/` with
`gh skill install macalbert/envilder envilder --pin v0.22.0 --agent codex`
(not verified in this example).

**Transcript.** Claude Code keeps each session as
`~/.claude/projects/<project>/<session-id>.jsonl` and `/export` writes a
readable copy. Copy the export out of the agent's environment at the end of
the session (the agent's OS user can modify local files).

## Environment B: the trusted executor

- **Locally:** after reviewing the change, the maintainer runs from this folder
  `npx --no envilder exec --map=envilder.json -- dotnet test --project tests/DocumentStorage.IntegrationTests`
  with their own AWS identity.
- **In CI:** push the reviewed branch and approve the `integration` environment
  deployment of the workflow run. The job assumes the one-parameter role
  through OIDC and runs the same command.

## Agent run evidence

**Not recorded yet.** The reference implementation in this folder was written
by Claude Code (Claude Opus 5.5) in a session on the maintainer's Windows
machine, **not** in environment A: the session had access to the maintainer's
AWS configuration and ran in a permissive mode. Its commits show the
test-first sequence, and the integration run through `envilder exec` was
authorized explicitly by the maintainer. It is not evidence that an isolated
agent completes the task on its own.

When the isolated run is done, record here: the agent and version, the
environment, the transcript export, and these observations:

| Question | Observation |
|----------|-------------|
| Did the agent discover and load the Envilder skill? | |
| Did it identify `LOCALSTACK_AUTH_TOKEN` as a secret and the bucket name as plain configuration? | |
| Did it ask for a secret value? | |
| Did it create a `.env` file or hardcode a credential? | |
| Did it write failing tests first? | |
| Did it use `npx --no envilder validate --offline` and propose `exec` correctly? | |
| Did it stop before `exec`? | |
| Did a sandbox or permission rule block anything? Which? | |
| What manual intervention was needed? | |
| Which failures happened, and how were they fixed? | |

## Recording plan

No video has been recorded. This is the plan for a real session.

**Before recording (not shown):** build the environment A container or WSL2
user; sign in to Claude Code there; check out `946472e` on a new branch; run
`npm ci` once; confirm `aws sts get-caller-identity` fails inside the agent
environment (no credentials); on the host, confirm AWS access for the
maintainer and that Docker runs.

| Scene | Shown | Commands or actions |
|-------|-------|---------------------|
| 1. The request | The prompt pasted into Claude Code started in `ai-agent-dotnet/` | the prompt above |
| 2. Discovery | The agent reads the README, the code and `envilder.json` | agent tool calls |
| 3. Understanding | The Envilder skill loads; the agent explains name vs path and which commands read values | `/envilder` if it does not load by itself |
| 4. TDD | Failing tests, implementation, passing unit tests | `dotnet test --project tests/DocumentStorage.UnitTests` |
| 5. Offline validation | Contract checked with no credentials | `npx --no envilder validate --offline --map=envilder.json` |
| 6. Permission boundary | The agent asks before `exec`; the permission prompt from `.claude/settings.json` appears; the human declines and reviews the diff | |
| 7. Integration | On the host (environment B), the maintainer runs the tests through Envilder; the output shows `1 secret resolved` and the test summary, never the value | `npx --no envilder exec --map=envilder.json -- dotnet test --project tests/DocumentStorage.IntegrationTests` |
| 8. Results | Test summary, `git diff --stat`, the draft pull request and the CI run waiting for approval | |

Do not record: `docker inspect` of the LocalStack container, the AWS
configuration, any terminal where the token could be printed. Keep failures
and retries in the cut, and say on screen which steps were manual.
