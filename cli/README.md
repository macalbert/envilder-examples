# Envilder CLI from zero: push, validate, pull and exec

**`npx envilder` · AWS SSM and Azure Key Vault**

The example is a real one: keep the LocalStack auth token in a secret store and start LocalStack with it. By the end you will have:

1. **pushed** a secret to a secret store,
2. **checked** that the map file is valid and that the secret exists, without reading it (`validate`),
3. **run a command** with the secret in its environment, without writing any file (`exec`),
4. **pulled** it into a `.env` file,

with **AWS SSM** or with **Azure Key Vault**. You need an account in the cloud you pick.

## Follow these in order

| Step | Guide | What you do |
|------|-------|-------------|
| 1 | [Setup](./setup.md) | install Node.js, Docker and the AWS or Azure CLI |
| 2 | [AWS SSM](./aws/README.md) | push, validate, exec and pull with the LocalStack token |
| 3 | [Azure Key Vault](./azure/README.md) | push, validate, exec and pull with the LocalStack token (needs the Azure CLI) |

Steps 2 and 3 are independent: do the one you need. Each has its own map file next to its guide ([`aws/envilder.json`](./aws/envilder.json), [`azure/envilder.json`](./azure/envilder.json)).

## The commands

| Command | What it does | Direction |
|---------|--------------|-----------|
| `npx envilder --push --key=… --value=… --secret-path=…` | stores one secret | you → store |
| `npx envilder --push` | stores every variable of your `.env` | `.env` → store |
| `npx envilder validate --offline` | checks the map file only: JSON, schema, provider settings. No cloud call | map file |
| `npx envilder validate` | also checks that every declared secret exists, without reading its value | store metadata |
| `npx envilder` | writes a `.env` with every secret of the map file | store → `.env` |
| `npx envilder exec <command>` | runs a command with the secrets in its environment, nothing on disk | store → memory → command |

`validate` exits with `0` when every check passes and `1` otherwise, and it never prints a secret value.

Envilder's own docs: [requirements and installation](https://github.com/macalbert/envilder/blob/main/docs/requirements-installation.md), [push](https://github.com/macalbert/envilder/blob/main/docs/push-command.md), [validate](https://github.com/macalbert/envilder/blob/main/docs/validate-command.md), [pull](https://github.com/macalbert/envilder/blob/main/docs/pull-command.md), [exec](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

## In CI

Run `validate` before the step that needs the secrets, so a missing or misnamed secret fails the job with its name instead of halfway through a deploy:

```yaml
- name: Check the map file
  run: npx envilder validate --offline --map=cli/aws/envilder.json

- name: Check that every secret exists
  run: npx envilder validate --map=cli/aws/envilder.json
```

The offline step needs no cloud credentials, so it can run on every pull request, forks included. The second one needs the same credentials as a pull, plus the list or `GetParameters` permission described in each guide.

> [!NOTE]
> Two more options are coming. `--envfile=<path>` (optionally with `--compare-values`) checks a local env file against the map file: it is merged in Envilder and ships in the next CLI release after `0.20.0`. `--format json`, a machine-readable report for CI, is still in review. With `0.20.0`, `validate` rejects both as unknown options.
