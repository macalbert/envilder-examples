# 2. AWS SSM

[← Back to the CLI guide](../README.md) · Needs the [setup](../setup.md) done, logged in to AWS · Next: [Azure Key Vault](../azure/README.md)

Real use case: [LocalStack](https://localstack.cloud) needs an [auth token](https://docs.localstack.cloud/aws/getting-started/auth-token/) to start. We keep that token in AWS SSM Parameter Store with Envilder, and Envilder hands it to Docker. It never goes into your shell or a file you commit.

Official docs: [AWS SSM Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html) · Envilder: [push](https://github.com/macalbert/envilder/blob/main/docs/push-command.md), [pull](https://github.com/macalbert/envilder/blob/main/docs/pull-command.md), [exec](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

## 1. Create the map file

A map file says which environment variable lives at which secret path. It holds paths, never values, so it is safe in Git. Create `envilder.json` (this folder, `cli/aws`, already has it):

```json
{
  "LOCALSTACK_AUTH_TOKEN": "/envilder-cli/localstack-auth-token"
}
```

## 2. Push the token

Copy your token from [app.localstack.cloud](https://app.localstack.cloud) (**Auth Tokens**) and store it in SSM:

```bash
npx envilder --push --key=LOCALSTACK_AUTH_TOKEN --value=<your-token> --secret-path=/envilder-cli/localstack-auth-token
```

The first run asks `Need to install the following packages: envilder`: answer `y`. You should see `SECRET PUSHED`.

> [!TIP]
> On Windows with **Git Bash**, add `MSYS_NO_PATHCONV=1` in front of the command (or use PowerShell). Git Bash otherwise rewrites `/envilder-cli/…` into a Windows path.

## 3. Run a command with the token

```bash
npx envilder exec -- docker run --rm --env LOCALSTACK_AUTH_TOKEN localstack/localstack
```

Envilder reads the token from SSM and starts Docker with it in its environment. `--env LOCALSTACK_AUTH_TOKEN` (no `=value`) tells Docker to copy that variable, so nothing is written to disk. If the secret is missing, the command is **not started** and Envilder exits with `125`. More in the [exec docs](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

Ctrl+C stops the container and `--rm` removes it.

## 4. Pull it into a `.env`

```bash
npx envilder
```

Envilder reads `envilder.json`, fetches every secret and writes `.env`:

```text
LOCALSTACK_AUTH_TOKEN=<your-token>
```

That file now has a secret in clear text: add `.env` to your `.gitignore`.

## Troubleshooting

| You see | Cause and fix |
|---------|---------------|
| `Unable to locate credentials` | You are not logged in to AWS. See the [setup](../setup.md#your-clouds-cli) |
| `secret not found … skipped` | The token was never pushed, or it went to another path. On Git Bash see the tip in [step 2](#2-push-the-token) |
| LocalStack exits right away | The token is wrong. Push it again (step 2) |
