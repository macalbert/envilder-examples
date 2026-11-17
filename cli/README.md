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
| `npx envilder validate --envfile=.env` | also checks a local `.env` against the map file; add `--compare-values` to compare each value with the store, without printing either | `.env` vs map file |
| `npx envilder validate --format json` | prints the same result as one JSON document, for scripts and CI | map file / store metadata |
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

For a script or a CI step that reads the result, add `--format json` (CLI `0.21.0` or later). It prints exactly one JSON document and keeps the same exit codes:

```bash
npx envilder validate --offline --format json --map=cli/aws/envilder.json
```

```json
{
  "valid": true,
  "offline": true,
  "mapFile": {
    "path": "cli/aws/envilder.json",
    "valid": true,
    "errors": []
  }
}
```

Without `--offline`, the document also has a `keys` array with every declared secret and its `status` (`exists`, `missing`, `access_denied`, ...), and with `--envfile` an `envFile` section. It never contains a secret value. See the [JSON output reference](https://github.com/macalbert/envilder/blob/main/docs/validate-command.md).
