# 3. Azure Key Vault

[← Back to the CLI guide](../README.md) · Needs the [setup](../setup.md) done, logged in with `az login` · Previous: [AWS SSM](../aws/README.md)

Real use case: [LocalStack for Azure](https://docs.localstack.cloud/azure/) needs an [auth token](https://docs.localstack.cloud/azure/getting-started) to start. We keep that token in Azure Key Vault with Envilder, and Envilder hands it to Docker. It never goes into your shell or a file you commit.

Official docs: [Azure Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/general/overview) · [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/javascript/api/@azure/identity/defaultazurecredential) · Envilder: [push](https://github.com/macalbert/envilder/blob/main/docs/push-command.md), [validate](https://github.com/macalbert/envilder/blob/main/docs/validate-command.md), [pull](https://github.com/macalbert/envilder/blob/main/docs/pull-command.md), [exec](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

You need a Key Vault. If you don't have one (the name must be unique worldwide):

```bash
az group create --name envilder-cli --location westeurope
az keyvault create --name <your-vault> --resource-group envilder-cli --location westeurope
```

## 1. Push the token

Copy your token from [app.localstack.cloud](https://app.localstack.cloud) (**Auth Tokens**). This folder (`cli/azure`) has an `envilder.json` with a `<your-vault>` placeholder, and a push to another vault won't add its mapping to it. So, from this folder, delete it and let the push write a new one for your vault:

```bash
rm envilder.json
npx envilder --push --provider=azure --vault-url=https://<your-vault>.vault.azure.net --key=LOCALSTACK_AUTH_TOKEN --value=<your-token> --secret-path=localstack-auth-token
```

The first run asks `Need to install the following packages: envilder`: answer `y`. You should see `SECRET PUSHED`.

The push stores the token in the vault and creates the map file, `envilder.json`. A map file says which environment variable lives at which secret. It holds names, never values, so it is safe in Git. Key Vault secret names allow only letters, digits and dashes, so the identifier is a name, not a path. For Azure the push also writes `$config`, so later commands know where to look without flags:

```json
{
  "$config": {
    "provider": "azure",
    "vaultUrl": "https://<your-vault>.vault.azure.net"
  },
  "LOCALSTACK_AUTH_TOKEN": "localstack-auth-token"
}
```

Next time, with this file in place, `--provider` and `--vault-url` are not needed: a push reads them from `$config`. More in the [push docs](https://github.com/macalbert/envilder/blob/main/docs/push-command.md#map-file-update).

## 2. Check it before you use it

`validate` is a preflight check: it says whether the map file is valid and whether every secret it declares exists, before `exec` or a pull needs them. It never reads or prints a secret value.

```bash
npx envilder validate --offline
```

`--offline` only reads the map file (JSON, schema, provider settings): no cloud call and no credentials, so it fits a pull request check. If you kept the committed `envilder.json` with its `<your-vault>` placeholder, it fails here with `$config.vaultUrl must be an absolute URI.` and exit code `1`.

Without `--offline`, Envilder also asks the vault whether each declared secret exists:

```bash
npx envilder validate
```

```text
✓ Map file valid: envilder.json
Checking 1 secret in azure, vault=<your-vault>.vault.azure.net
  ✓ LOCALSTACK_AUTH_TOKEN  localstack-auth-token  exists
✓ 1 secret exists
```

If you skipped step 1, the key shows as `missing` and the summary `✗ Validation failed: 1 missing` goes to stderr. Other statuses are `access denied`, `authentication failed` and `provider error`; `access denied` is never reported as `missing`. The exit code is `0` when everything passes and `1` otherwise, so it can gate a script or a CI job.

The remote check lists the vault's secret names (metadata only), so your user needs permission to list secrets, not only to read them. The *Key Vault Secrets User* and *Key Vault Secrets Officer* roles include it; with access policies, grant `List` besides `Get`. More in the [validate docs](https://github.com/macalbert/envilder/blob/main/docs/validate-command.md).

## 3. Run a command with the token

```bash
npx envilder exec -- docker run --rm --env LOCALSTACK_AUTH_TOKEN localstack/localstack-azure:latest
```

Envilder reads the token from the vault and starts Docker with it in its environment. `--env LOCALSTACK_AUTH_TOKEN` (no `=value`) tells Docker to copy that variable, so nothing is written to disk. If the secret is missing, the command is **not started** and Envilder exits with `125`. More in the [exec docs](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

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
| `Please run 'az login'` / Envilder can't authenticate | You are not logged in to Azure. See the [setup](../setup.md#your-clouds-cli) |
| `Forbidden` / access denied on the vault | Your user needs the *Key Vault Secrets Officer* role on the vault |
| `validate` says `access denied` for every key | Your user can read secrets but not list them. See [step 2](#2-check-it-before-you-use-it) |
| `secret not found … skipped`, or `missing` in `validate` | The token was never pushed, or the name differs from the map file |
| LocalStack exits right away | The token is wrong. Push it again (step 1) |
