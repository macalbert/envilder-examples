# 2. AWS SSM

[← Back to the CLI guide](../README.md) · Needs the [setup](../setup.md) done, logged in to AWS · Next: [Azure Key Vault](../azure/README.md)

Real use case: [LocalStack](https://localstack.cloud) needs an [auth token](https://docs.localstack.cloud/aws/getting-started/auth-token/) to start. We keep that token in AWS SSM Parameter Store with Envilder, and Envilder hands it to Docker. It never goes into your shell or a file you commit.

Official docs: [AWS SSM Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html) · Envilder: [push](https://github.com/macalbert/envilder/blob/main/docs/push-command.md), [validate](https://github.com/macalbert/envilder/blob/main/docs/validate-command.md), [pull](https://github.com/macalbert/envilder/blob/main/docs/pull-command.md), [exec](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

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

## 3. Check it before you use it

`validate` is a preflight check: it says whether the map file is valid and whether every secret it declares exists, before `exec` or a pull needs them. It never reads or prints a secret value.

```bash
npx envilder validate --offline
```

`--offline` only reads the map file (JSON, schema, provider settings): no cloud call and no credentials, so it fits a pull request check. Without it, Envilder also asks SSM whether each declared secret exists:

```bash
npx envilder validate
```

```text
✓ Map file valid: envilder.json

☁ AWS identity · account=123456789012 · region=eu-west-1 · profile=default
Checking 1 secret in aws
  ✓ LOCALSTACK_AUTH_TOKEN  /envilder-cli/localstack-auth-token  exists
✓ 1 secret exists
```

If you skipped step 2, the key shows as `missing` and the summary `✗ Validation failed: 1 missing` goes to stderr. Other statuses are `access denied`, `authentication failed` and `provider error`; `access denied` is never reported as `missing`. The exit code is `0` when everything passes and `1` otherwise, so it can gate a script or a CI job.

The remote check calls `ssm:GetParameters` without decryption, a different IAM action from the `ssm:GetParameter` that pull and `exec` use, so your identity needs both. No `kms:Decrypt` is needed for `validate`. More in the [validate docs](https://github.com/macalbert/envilder/blob/main/docs/validate-command.md).

## 4. Run a command with the token

```bash
npx envilder exec -- docker run --rm --env LOCALSTACK_AUTH_TOKEN localstack/localstack
```

Envilder reads the token from SSM and starts Docker with it in its environment. `--env LOCALSTACK_AUTH_TOKEN` (no `=value`) tells Docker to copy that variable, so nothing is written to disk. If the secret is missing, the command is **not started** and Envilder exits with `125`. More in the [exec docs](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

Ctrl+C stops the container and `--rm` removes it.

## 5. Pull it into a `.env`

```bash
npx envilder
```

Envilder reads `envilder.json`, fetches every secret and writes `.env`:

```text
LOCALSTACK_AUTH_TOKEN=<your-token>
```

That file now has a secret in clear text: add `.env` to your `.gitignore`.

### Check the `.env` against the map file

> [!NOTE]
> `--envfile` and `--compare-values` are merged in Envilder but not released yet: they ship in the next CLI release after `0.20.0`. With `0.20.0`, `validate` rejects them with `unknown option`.

```bash
npx envilder validate --envfile=.env
```

Every variable of the map file must be in `.env` exactly once: a missing one (`missing from .env`) or a repeated one (`duplicated in .env`) fails, and a key only in `.env` is a warning (`not declared in map file`). Add `--compare-values` to also check that each value in `.env` matches the one in the store; it reports `matches` or `differs` and never prints either value.

## Troubleshooting

| You see | Cause and fix |
|---------|---------------|
| `Unable to locate credentials` | You are not logged in to AWS. See the [setup](../setup.md#your-clouds-cli) |
| `validate` says `access denied` | Your identity lacks `ssm:GetParameters`. Add it next to `ssm:GetParameter` (see [step 3](#3-check-it-before-you-use-it)) |
| `secret not found … skipped`, or `missing` in `validate` | The token was never pushed, or it went to another path. On Git Bash see the tip in [step 2](#2-push-the-token) |
| LocalStack exits right away | The token is wrong. Push it again (step 2) |
