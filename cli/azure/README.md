# 3. Azure Key Vault

[← Back to the CLI guide](../README.md) · Needs the [setup](../setup.md) done, logged in with `az login` · Previous: [AWS SSM](../aws/README.md)

Real use case: [LocalStack for Azure](https://docs.localstack.cloud/azure/) needs an [auth token](https://docs.localstack.cloud/azure/getting-started) to start. We keep that token in Azure Key Vault with Envilder, and Envilder hands it to Docker. It never goes into your shell or a file you commit.

Official docs: [Azure Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/general/overview) · [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/javascript/api/@azure/identity/defaultazurecredential) · Envilder: [push](https://github.com/macalbert/envilder/blob/main/docs/push-command.md), [pull](https://github.com/macalbert/envilder/blob/main/docs/pull-command.md), [exec](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

You need a Key Vault. If you don't have one (the name must be unique worldwide):

```bash
az group create --name envilder-cli --location westeurope
az keyvault create --name <your-vault> --resource-group envilder-cli --location westeurope
```

## 1. Create the map file

A map file says which environment variable lives at which secret. It holds names, never values, so it is safe in Git. Key Vault secret names allow only letters, digits and dashes, so the identifier is a name, not a path. Create `envilder.json` (this folder, `cli/azure`, already has it) with your vault:

```json
{
  "$config": {
    "provider": "azure",
    "vaultUrl": "https://<your-vault>.vault.azure.net"
  },
  "LOCALSTACK_AUTH_TOKEN": "localstack-auth-token"
}
```

## 2. Push the token

Copy your token from [app.localstack.cloud](https://app.localstack.cloud) (**Auth Tokens**) and store it in the vault:

```bash
npx envilder --push --key=LOCALSTACK_AUTH_TOKEN --value=<your-token> --secret-path=localstack-auth-token
```

The first run asks `Need to install the following packages: envilder`: answer `y`. You should see `SECRET PUSHED`.

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

## Troubleshooting

| You see | Cause and fix |
|---------|---------------|
| `Please run 'az login'` / Envilder can't authenticate | You are not logged in to Azure. See the [setup](../setup.md#your-clouds-cli) |
| `Forbidden` / access denied on the vault | Your user needs the *Key Vault Secrets Officer* role on the vault |
| `secret not found … skipped` | The token was never pushed, or the name differs from the map file |
| LocalStack exits right away | The token is wrong. Push it again (step 2) |
