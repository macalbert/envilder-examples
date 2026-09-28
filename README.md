# Envilder Examples

How much code does it take to feed a secret from AWS SSM into a LocalStack container — with nothing on disk and nothing committed?

This much:

```typescript
const env = await Envilder.resolveFile('envilder.json');

const localstack = await new LocalstackContainer('localstack/localstack:stable')
  .withEnvironment(Object.fromEntries(env))
  .start();
```

Plus one committed file that maps names to paths — paths, not values, so it's safe in Git:

```json
{
  "LOCALSTACK_AUTH_TOKEN": "/demo/localstack/auth-token"
}
```

That's the entire integration. [Envilder](https://envilder.com) resolves the token from your cloud at runtime: SSM → memory → container. No `.env`, no export, no bash script per stack. This repo shows the same two lines in five setups.

## The examples

| Folder | Stack | Run it (from the folder) |
|--------|-------|--------|
| [`typescript-testcontainers/`](./typescript-testcontainers) | Vitest + Testcontainers | `npm install && npm test` |
| [`python-testcontainers/`](./python-testcontainers) | pytest + Testcontainers (uv) | `uv run pytest` |
| [`dotnet-testcontainers/`](./dotnet-testcontainers) | xUnit v3 + Testcontainers | `dotnet test` |
| [`dotnet-aspire/`](./dotnet-aspire) | Aspire AppHost + `Aspire.Hosting.Testing` | `cd AppHost.Tests && dotnet test` |
| [`typescript-aspire/`](./typescript-aspire) | Aspire [TypeScript AppHost](https://devblogs.microsoft.com/aspire/aspire-typescript-apphost/) | `npm install && npm test` * |

Each folder is self-contained: there is no `package.json` at the repo root, so run the command inside the folder. The two .NET examples also share a solution, so `dotnet test` from the repo root runs all of them.

Every test does the same three steps: resolve the token with the Envilder SDK, start LocalStack with it, and round-trip a `SecureString` against emulated SSM — the operation that requires a valid auth token.

Testcontainers and Aspire are alternative orchestrators; pick the folder that matches your project. (Aspire AppHosts can also orchestrate [Python](https://aspire.dev/integrations/frameworks/python/) and JavaScript apps.)

* Needs the [Aspire CLI](https://aspire.dev) 13.5+. The test is black-box (spawns `aspire run` and waits for the health endpoint) since there's no TypeScript `Aspire.Hosting.Testing` yet. The first run generates `.modules/` and `.aspire/` (both git-ignored).

## Before you run (once)

Install only what the folders you want need:

| Tool | Version | Used by |
|------|---------|---------|
| [Docker](https://www.docker.com/) | any recent | all |
| [Node.js](https://nodejs.org/) | 24 LTS or newer (see [`.nvmrc`](./.nvmrc)) | `typescript-*` |
| [uv](https://docs.astral.sh/uv/) | any recent — it installs Python 3.14 for you | `python-testcontainers` |
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 (see [`global.json`](./global.json)) | `dotnet-*` |
| [Aspire CLI](https://aspire.dev/get-started/install-cli/) | 13.5+ | `typescript-aspire` |

You also need AWS credentials in `~/.aws/credentials` and a [LocalStack auth token](https://docs.localstack.cloud/aws/getting-started/auth-token/) stored in your SSM. Envilder pushes it for you — also one command:

```bash
npx envilder --push --key=LOCALSTACK_AUTH_TOKEN   --value=<your-token> --secret-path=/demo/localstack/auth-token
```

Using a named AWS profile, or keeping the token in Azure Key Vault instead of SSM? Both are a `$config` block in [`envilder.json`](./envilder.json) — see [providers](https://envilder.com/#providers).

## Updating dependencies

Versions are pinned in one place per ecosystem, so an upgrade is one command each:

| Ecosystem | Where versions live | Upgrade |
|-----------|---------------------|---------|
| .NET | [`Directory.Packages.props`](./Directory.Packages.props) (NuGet), [`global.json`](./global.json) (SDK + `Aspire.AppHost.Sdk`) | `dotnet outdated envilder-examples.slnx -u` ([dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated)) |
| Python | [`pyproject.toml`](./python-testcontainers/pyproject.toml) + `uv.lock` | `uv lock --upgrade` |
| Node | each folder's `package.json` + `package-lock.json` | `npx npm-check-updates -u && npm install` |
| Aspire (TypeScript) | [`aspire.config.json`](./typescript-aspire/aspire.config.json) | `aspire update`, then delete `.modules/` so it regenerates |

When you bump Aspire, update the `Aspire.AppHost.Sdk` version in `global.json` and `Aspire.Hosting.Testing` in `Directory.Packages.props` together.

## Links

- [Envilder](https://github.com/macalbert/envilder) · [envilder.com](https://envilder.com)
- Blog: [Envilder + Testcontainers + LocalStack: integration tests that fetch their own secrets](https://dev.to/macalbert)
