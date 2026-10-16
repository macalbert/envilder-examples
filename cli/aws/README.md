# 2. AWS SSM with LocalStack

[← Back to the CLI guide](../README.md) · Needs the [setup](../setup.md) done · Next: [Azure Key Vault](../azure/README.md)

[LocalStack](https://localstack.cloud) pretends to be AWS on your machine, so `envilder` can store secrets in "SSM Parameter Store" without an AWS account.

Official docs: [LocalStack getting started](https://docs.localstack.cloud/aws/getting-started/) · [AWS SSM Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html) · [`docker run`](https://docs.docker.com/reference/cli/docker/container/run/) · Envilder: [push](https://github.com/macalbert/envilder/blob/main/docs/push-command.md), [pull](https://github.com/macalbert/envilder/blob/main/docs/pull-command.md), [exec](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

## 1. Get a LocalStack token

LocalStack needs an [auth token](https://docs.localstack.cloud/aws/getting-started/auth-token/) to start. Create an account at [app.localstack.cloud](https://app.localstack.cloud), open **Auth Tokens** and copy yours. Put it in a variable of your terminal (this lasts until you close it):

```bash
# macOS / Ubuntu
export LOCALSTACK_AUTH_TOKEN=<your-token>
```

```powershell
# Windows PowerShell
$env:LOCALSTACK_AUTH_TOKEN = '<your-token>'
```

## 2. Start LocalStack

```bash
docker run -d --rm --name localstack --env LOCALSTACK_AUTH_TOKEN -p 4566:4566 localstack/localstack:stable
```

`--env LOCALSTACK_AUTH_TOKEN` (no `=value`) tells Docker to copy that variable from your terminal into the container. Wait about 20 seconds until `docker ps` shows `(healthy)` next to `localstack`.

## 3. Point Envilder at LocalStack

Envilder talks to AWS with the standard [AWS variables](https://docs.aws.amazon.com/sdkref/latest/guide/feature-ss-endpoints.html). Set them to aim at the emulator:

```bash
# macOS / Ubuntu
export AWS_ENDPOINT_URL=http://localhost:4566
export AWS_REGION=us-east-1
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
```

```powershell
# Windows PowerShell
$env:AWS_ENDPOINT_URL = 'http://localhost:4566'
$env:AWS_REGION = 'us-east-1'
$env:AWS_ACCESS_KEY_ID = 'test'
$env:AWS_SECRET_ACCESS_KEY = 'test'
```

| Variable | Why |
|----------|-----|
| `AWS_ENDPOINT_URL` | sends the requests to LocalStack instead of AWS |
| `AWS_REGION` | the AWS SDK refuses to run without one |
| `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` | LocalStack accepts any credentials |

## 4. Create the map file

A map file says which environment variable lives at which secret path. It holds paths, never values, so it is safe in Git. Work in a folder of your own (or in this one, `cli/aws`, which already has it) and create `envilder.json`:

```json
{
  "$config": {
    "provider": "aws"
  },
  "DEMO_SECRET": "/envilder-cli/demo-secret"
}
```

## 5. Push a secret

```bash
npx envilder --push --key=DEMO_SECRET --value=hello-from-cli --secret-path=/envilder-cli/demo-secret
```

The first run asks `Need to install the following packages: envilder`: answer `y`. You should see `SECRET PUSHED`. The secret now lives in SSM at `/envilder-cli/demo-secret`.

> [!TIP]
> On Windows with **Git Bash**, add `MSYS_NO_PATHCONV=1` in front of the command (or use PowerShell). Git Bash otherwise rewrites `/envilder-cli/…` into a Windows path.

## 6. Pull it into a `.env`

```bash
npx envilder
```

Envilder reads `envilder.json`, fetches every secret and writes `.env`:

```text
DEMO_SECRET=hello-from-cli
```

That file now has a secret in clear text: add `.env` to your `.gitignore`.

## 7. Run a command with the secret, no file

```bash
npx envilder exec node -p "process.env.DEMO_SECRET"
```

It prints `hello-from-cli`. Envilder fetched the secret, started `node` with it in its environment, and wrote nothing to disk. Everything after `exec` is your command, so this is how you would run your real app:

```bash
npx envilder exec npm run dev
```

If a secret is missing, the command is **not started** and Envilder exits with `125`. More in the [exec docs](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

## 8. Push a whole `.env`

Edit the `.env` (say `DEMO_SECRET=changed`) and run:

```bash
npx envilder --push
```

Every variable of the `.env` that appears in the map file is stored at its path. Run `npx envilder exec node -p "process.env.DEMO_SECRET"` again and it prints `changed`.

## 9. Clean up

```bash
docker stop localstack
```

`--rm` made Docker delete the container, and the secrets with it.

## Going to a real AWS account

The commands are identical. You only remove the emulator and log in for real:

1. Install the AWS CLI ([setup](../setup.md#your-clouds-cli)) and log in with `aws configure` or `aws sso login`.
2. Open a new terminal, or unset `AWS_ENDPOINT_URL` and the fake keys, so they don't leak into the real run.
3. Optionally add `"profile": "my-profile"` to `$config` in the map file to use a named AWS profile.

A nice use of `exec`: keep the LocalStack token itself in your real SSM and start LocalStack with it, without ever typing it again. See [`envilder exec` in the root README](../../README.md#run-any-command-with-its-secrets-envilder-exec).

## Troubleshooting

| You see | Cause and fix |
|---------|---------------|
| `port is already allocated` | `4566` is in use. Stop the old container with `docker stop localstack` |
| LocalStack exits right away | The token is missing or wrong. Check `docker logs localstack` |
| `Unable to locate credentials` | The AWS variables of [step 3](#3-point-envilder-at-localstack) are missing in this terminal |
| `secret not found … skipped` | The secret was never pushed, or it went to another path. On Git Bash see the tip in [step 5](#5-push-a-secret) |
| Everything worked, then stopped working | You opened a new terminal. The variables live only in the terminal where you set them |
