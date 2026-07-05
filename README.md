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

| Folder | Stack | Run it |
|--------|-------|--------|
| [`typescript-testcontainers/`](./typescript-testcontainers) | Vitest + Testcontainers | `npm install && npm test` |
| [`python-testcontainers/`](./python-testcontainers) | pytest + Testcontainers (uv) | `uv run pytest` |
| [`dotnet-testcontainers/`](./dotnet-testcontainers) | xUnit + Testcontainers | `dotnet test` |
| [`dotnet-aspire/`](./dotnet-aspire) | Aspire AppHost + `Aspire.Hosting.Testing` | `cd AppHost.Tests && dotnet test` |
| [`typescript-aspire/`](./typescript-aspire) | Aspire [TypeScript AppHost](https://devblogs.microsoft.com/aspire/aspire-typescript-apphost/) | `npm install && npm test` * |

Every test does the same three steps: resolve the token with the Envilder SDK, start LocalStack with it, and round-trip a `SecureString` against emulated SSM — the operation that requires a valid auth token.

Testcontainers and Aspire are alternative orchestrators; pick the folder that matches your project.

\* Needs the [Aspire CLI](https://aspire.dev) 13.2+. The test is black-box (spawns `aspire run` and waits for the health endpoint) since there's no TypeScript `Aspire.Hosting.Testing` yet.

## Before you run (once)

You need Docker, AWS credentials in `~/.aws/credentials`, and a [LocalStack auth token](https://docs.localstack.cloud/aws/getting-started/auth-token/) stored in your SSM. Envilder pushes it for you — also one command:

```bash
npx envilder --push --key=LOCALSTACK_AUTH_TOKEN \
  --value=<your-token> --secret-path=/demo/localstack/auth-token
```

Using a named AWS profile, or keeping the token in Azure Key Vault instead of SSM? Both are a `$config` block in [`envilder.json`](./envilder.json) — see [providers](https://envilder.com/#providers).

## Links

- [Envilder](https://github.com/macalbert/envilder) · [envilder.com](https://envilder.com)
- Blog: [Envilder + Testcontainers + LocalStack: integration tests that fetch their own secrets](https://dev.to/macalbert)
