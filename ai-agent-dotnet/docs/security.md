# Security model and threat analysis

This page describes what protects the LocalStack token in this example, who
can read it, and what is **not** protected. It follows the principles of the
OWASP DevSecOps Guideline page
[AI Agent and MCP Security](https://owasp.github.io/DevSecOpsGuideline/2-Process/2-2-Develop/2-2-5-AI-Agent-and-MCP-Security/),
mainly **least agency**: an agent gets only the permissions, tools, data and
execution it needs for its task. Following a guideline is not a certification,
and this example claims no compliance.

## Three separate concerns

| Concern | Who provides it here | What it does |
|---------|----------------------|--------------|
| Secret management | AWS SSM Parameter Store and IAM | Stores the value; decides which identity can read it |
| Secret contract and resolution | Envilder (`envilder.json`, `envilder exec`) | Declares which secrets a project needs; resolves them for a command run by an authorized identity, without a generated `.env` file |
| Agent security | The agent runtime, the OS or container, GitHub, IAM, human review | Limits what an agent can read, run and reach |

Envilder removes the need to copy and distribute secret values. It does not
prevent prompt injection, it does not stop an authorized process from reading
its own environment, it does not sandbox commands or MCP servers, and it does
not guarantee that a secret is never exfiltrated.

## Who can read the secret value

| Actor | Reads the mapping | Can read the value | Why |
|-------|-------------------|--------------------|-----|
| Human developer | Yes | Yes, with their AWS identity | IAM allows it |
| AI coding agent (environment A, isolated) | Yes | No, as long as it has no cloud credentials and does not run `envilder exec` | It sees names and paths only |
| AI coding agent with the developer's shell and `~/.aws` | Yes | **Yes** | It can run `envilder exec` or the AWS CLI itself |
| Envilder CLI | Yes | Yes | It resolves the value with the caller's identity |
| AWS credential provider (profile, SSO cache, OIDC) | No | No (it provides credentials, not the value) | |
| AWS SSM Parameter Store | n/a | Holds it | Source of truth |
| Test process (`dotnet test` and everything it starts) | No | **Yes** | Envilder puts the value in its environment |
| Testcontainers | No | **Yes** | Runs in the test process |
| Docker engine and LocalStack container | No | **Yes** | The value is passed as a container environment variable and kept in container metadata while the container exists |
| GitHub Actions runner (approved `integration` job) | Yes | **Yes** | Same as the test process, on a disposable runner |

### Five operations, five different boundaries

1. **An agent reads `envilder.json`.** It learns a variable name and an SSM
   path. No secret is exposed.
2. **An agent runs a command.** The command runs with the agent's identity and
   environment. If that environment holds cloud credentials, the command can
   use them.
3. **A child process receives environment variables.** Everything that process
   runs, including test code and its dependencies, can read them.
4. **Application or test code reads a variable.** It can log it, send it over
   the network or write it to disk. Nothing in Envilder stops it.
5. **Docker receives a variable.** The engine stores it with the container
   (`docker inspect`), and the containerized process can read it.

## Two trust environments

**Environment A: the coding agent (unprivileged).** It may read the code, the
map file, the skill and approved documentation, write code and tests, run the
unit tests and `envilder validate --offline`, inspect diffs and prepare a
branch. It must not receive secret values or cloud credentials, run commands
with production credentials, change cloud secrets, push, approve or merge.
Setup and agent permissions: [agent-workflow.md](agent-workflow.md#environment-a-the-agent).

**Environment B: the trusted executor.** After a maintainer has reviewed the
change, the integration tests run through `envilder exec`:

- **in CI**, in the `integration` GitHub environment (required reviewer), on a
  disposable GitHub-hosted runner, with short-lived OIDC credentials for a role
  that can read only `/envilder/development/localstack/authToken`
  ([`infra/integration-test-role.yml`](../../infra/integration-test-role.yml));
- **locally**, by the maintainer, with their own AWS identity.

The executor never trusts code or a map file only because an agent produced
it: the trust transition is the human review and approval.

## Threats

Each row says which component provides each control: **E** Envilder, **R**
agent runtime, **I** AWS IAM, **G** GitHub (CI, environments, rulesets,
CODEOWNERS), **O** OS or container, **H** human review.

### T1. Prompt injection

| | |
|-|-|
| Entry point | Repository files (code comments, README, `AGENTS.md`, skills), issues, PR text, test output, dependency content, web pages |
| Trust boundary | Untrusted content enters the agent's context; the agent acts with its own permissions |
| Impact | The agent is steered to run `envilder exec` or read credentials, weaken tests or CI, or add exfiltration code that runs later with secrets |
| Preventive | Agent without cloud credentials (O, R); deny-by-default permissions with `exec` and network on ask or deny (R); skills and instruction files pinned and under CODEOWNERS (G); review before any privileged run (H) |
| Detective | Diff review of instruction files and skills (H); agent transcript (R) |
| Residual | The model can be fooled; review can miss a subtle change |
| Verification | Not exercised with an adversarial payload in this example (unverified) |

### T2. Secret exfiltration

| | |
|-|-|
| Entry point | Test code, fixtures, dependencies or the application, run through `envilder exec` |
| Trust boundary | The process that receives the secret |
| Impact | The LocalStack token (a development credential) is sent to an attacker |
| Preventive | Review before the privileged run (H); CI runs only after approval (G); IAM limits what any leaked CI credential can read (I); only a development token is mapped, no production secret (H) |
| Detective | GitHub Actions log masking hides exact matches of registered secrets only (G); CloudTrail records `GetParameter` calls (I) |
| Residual | **Not prevented.** `envilder exec` is not a sandbox (E docs). GitHub-hosted runners have unrestricted egress; this example adds no egress filtering |
| Verification | Not attempted (no exfiltration test against real credentials, by design) |

### T3. Mapping manipulation

| | |
|-|-|
| Entry point | A change to `envilder.json` that points `LOCALSTACK_AUTH_TOKEN` (or a new variable) at another secret |
| Trust boundary | Code review and the executing identity's IAM permissions |
| Impact | A run resolves a secret that was never approved for it |
| Preventive | In CI, the role can read only the approved path, so any other path fails with access denied and `exec` exits `125` before the tests start (I, E); `envilder*.json` under CODEOWNERS (G) |
| Detective | `envilder validate` reports `access_denied` per variable (E); diff review (H) |
| Residual | Locally, the maintainer's identity may read far more than one path, so IAM does not catch it: only review does. The ruleset on `main` does not yet require a code owner's approval (see residual risks) |
| Verification | With the CI role: `GetParameters` on another path returned `AccessDenied` (verified 2026-11-18 with the AWS CLI). `exec` with an unreadable path exits `125` (verified with a missing path). Not run end to end in CI |

### T4. Tool misuse

| | |
|-|-|
| Entry point | The agent's shell, file tools, MCP servers, `gh`, `aws` |
| Trust boundary | The agent runtime's permission system and the OS user it runs as |
| Impact | The agent uses more privilege than the task needs: reads `~/.aws`, calls AWS, pushes, merges |
| Preventive | Committed permission settings with deny rules for credentials, AWS CLI, push and merge, and ask rules for `exec` (R); agent in a container without `~/.aws` and without push credentials (O); no MCP server added (H) |
| Detective | Agent transcript; GitHub audit log for pushes and merges (G) |
| Residual | Permission prompts are not a security boundary against a manipulated agent; only isolation is |
| Verification | Isolated agent run not done yet (unverified). See [agent-workflow.md](agent-workflow.md#agent-run-evidence) |

### T5. Supply-chain compromise

| | |
|-|-|
| Entry point | NuGet and npm packages, the LocalStack image, the Envilder CLI, the agent skill, MCP servers |
| Trust boundary | Code that runs in the agent's environment or in the privileged test process |
| Impact | Malicious code runs with the agent's permissions or with the secret |
| Preventive | NuGet versions pinned centrally, Envilder CLI pinned to `0.22.0` with a lockfile (`npx --no` never downloads), skill pinned to tag `v0.22.0` with its tree SHA recorded in its frontmatter, no MCP server (H, G) |
| Detective | Dependency diffs in review (H); NuGet audit warnings at build (`NU1903`/`NU1904`) |
| Residual | `localstack/localstack:stable` is a moving tag. The solution build reports known high and critical vulnerabilities in Aspire packages of another example in this repository |
| Verification | Pins present in the files listed above (verified). Image digest pinning not done |

### T6. Workflow manipulation

| | |
|-|-|
| Entry point | A change to `.github/workflows/`, the IAM template or the environment settings |
| Trust boundary | GitHub: who can change workflows, who approves environment runs |
| Impact | Unreviewed code runs with cloud credentials |
| Preventive | Every job that assumes the role runs in the `integration` environment with a required reviewer (G); the role trusts only that environment's OIDC subject (I); `.github/` and `infra/` under CODEOWNERS (G); `main` accepts changes only through pull requests (G) |
| Detective | Environment deployment history; GitHub audit log (G) |
| Residual | A workflow change in a pull request still needs the approval of a maintainer who reads it; an approval without review is a hole. The agent must not hold credentials that can change environment settings |
| Verification | The first run of the pull request that introduced the gating waited for approval with status `waiting` (verified, run 44330321008). A forged OIDC subject was not tested |

### T7. Audit manipulation

| | |
|-|-|
| Entry point | Agent edits or deletes logs, transcripts or Git history |
| Trust boundary | Where the evidence is stored |
| Impact | A privileged run cannot be investigated |
| Preventive | Evidence that matters lives outside the agent's reach: CloudTrail (I), GitHub Actions logs and environment deployment history (G), pull request history on GitHub (G); `main` rejects force pushes and deletion (G) |
| Detective | Compare local transcripts with GitHub and CloudTrail records |
| Residual | Local agent transcripts are files the agent's OS user can modify. CloudTrail retention and alerting are not configured by this example |
| Verification | Ruleset blocks deletion and non-fast-forward on `main` (configured, not adversarially tested) |

## Controlled security checks

| # | Check | Status | Evidence |
|---|-------|--------|----------|
| 1 | Offline validation without cloud credentials | Verified | `validate --offline` exit 0; no provider call |
| 2 | Failure when a mapped secret is unavailable | Verified | `exec` with a missing path: `not found`, exit `125`, child not started ([troubleshooting](troubleshooting.md)) |
| 3 | Rejection of an unapproved mapping change | Partly verified | The CI role is denied any other path (AWS CLI). Not exercised through a pull request. Code-owner approval is not enforced |
| 4 | No privileged execution from an unreviewed PR | Verified | Run 44330321008 waited in `integration` for approval |
| 5 | Scoped IAM permissions | Verified | Assumed role: token path readable; other path, `DescribeParameters`, `PutParameter` denied |
| 6 | Agent-side processes do not inherit cloud credentials | **Unverified** | The reference implementation was built by an agent session with access to the developer's `~/.aws`. The isolated environment A run is still to do |
| 7 | The agent's actual sandbox and network restrictions | **Unverified** | Pending the environment A run |

## OWASP alignment

| Practice (OWASP AI Agent and MCP Security) | Implemented control | Component | Evidence | Gaps and residual risk |
|-------------------------------------------|---------------------|-----------|----------|------------------------|
| Agents have their own identity, not a developer's credentials | Environment A has no cloud credentials and no push credentials; the human pushes | R, O, H | Pending agent run | No dedicated GitHub identity (bot or App) for the agent yet |
| Scoped, short-lived tokens; no long-lived credentials in the agent environment | CI: OIDC, one-hour session, one parameter | I, G | Verified (check 5) | Local runs use the maintainer's IAM user, which has long-lived access keys |
| Assume anything in the agent's environment can be exfiltrated | No secret and no `.env` in the workspace; `exec` only in environment B | E, H | No `.env` after runs (verified) | An agent given `exec` permission can read the value |
| Deny by default, allow explicitly, in committed settings | Project permission settings for the agent | R | See [agent-workflow.md](agent-workflow.md#agent-permissions) | Prompts are not a boundary; isolation is |
| Sandboxing and isolation; no home directory mounts | Agent in a container without `~/.aws` | O | Pending | Not yet exercised |
| Restrict network egress | Agent container egress allowlist | O | Pending | CI runners have unrestricted egress |
| Treat repository content as untrusted; instruction files under CODEOWNERS | CODEOWNERS for map files, workflows, `infra/`, `AGENTS.md`, `CLAUDE.md`, `.claude/`, skills | G | File present | Code-owner review not required by the ruleset (single maintainer) |
| Pin versions; vet skills and MCP servers | CLI, skill and NuGet pinned; no MCP server | H | Files present | LocalStack image tag not pinned to a digest |
| Human approval for privileged and irreversible actions | Required reviewer on `integration`; `exec` on ask; agent never merges | G, R, H | Check 4 | Single maintainer reviews their own agent's work |
| Never let an agent approve or merge its own PR | Agent has no merge credentials (environment A) | O, H | Pending | Ruleset requires 0 approvals |
| Audit logging outside the agent's control | CloudTrail, GitHub Actions and environment history | I, G | Run history | No alerting configured |

## Residual risks accepted for this example

- **Local runs use the maintainer's IAM user.** It has long-lived access keys
  and broad permissions. The maintainer chose one profile over a second,
  scoped one. Anything run through local `exec` could use those credentials
  too, not only the token.
- **Single maintainer.** The `main` ruleset requires pull requests but zero
  approvals, because the only maintainer cannot approve their own pull
  request. Independent review of an agent's work is a process rule here, not
  an enforced control.
- **The reference implementation was written by an agent in a non-isolated
  session** (Claude Code on the maintainer's machine, with access to
  `~/.aws`). It shows the code and test workflow, not the isolation of
  environment A.
- **No egress filtering in CI.** An approved job that runs malicious code can
  send the token out. Approval is the control.
