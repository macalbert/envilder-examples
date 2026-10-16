# 3. Azure Key Vault with LocalStack

[← Back to the CLI guide](../README.md) · Needs the [setup](../setup.md) done, including the **Azure CLI** · Previous: [AWS SSM](../aws/README.md)

[LocalStack for Azure](https://docs.localstack.cloud/azure/) pretends to be Azure on your machine, so `envilder` can store secrets in a Key Vault without an Azure account. It works like the [AWS guide](../aws/README.md) (same LocalStack token, same four commands), with one extra step: creating the vault, because the emulator starts empty.

Official docs: [LocalStack for Azure](https://docs.localstack.cloud/azure/getting-started) · [Key Vault in LocalStack](https://docs.localstack.cloud/azure/services/key-vault/) · [Credentials](https://docs.localstack.cloud/azure/connecting/credentials/) · [Azure Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/general/overview) · [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/javascript/api/@azure/identity/defaultazurecredential) · Envilder: [push](https://github.com/macalbert/envilder/blob/main/docs/push-command.md), [pull](https://github.com/macalbert/envilder/blob/main/docs/pull-command.md), [exec](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

## 1. Start LocalStack for Azure

You need your LocalStack token in `LOCALSTACK_AUTH_TOKEN`: see [step 1 of the AWS guide](../aws/README.md#1-get-a-localstack-token). If the AWS emulator is still running, stop it first (`docker stop localstack`): both use port `4566`.

```bash
docker run -d --rm --name localstack-azure --env LOCALSTACK_AUTH_TOKEN -p 4566:4566 localstack/localstack-azure:latest
```

Wait until `docker logs localstack-azure` shows `Ready.` (a few seconds). A "Docker not available" message in the logs doesn't affect Key Vault.

## 2. Point the Azure CLI at LocalStack

Envilder signs in to Azure with the Azure CLI's login. We give the emulator its own Azure CLI configuration folder, so **your real Azure login is never touched**. The folder is selected with `AZURE_CONFIG_DIR`, which lasts as long as the terminal.

```bash
# macOS / Ubuntu
export AZURE_CONFIG_DIR="$HOME/.azure-localstack"
```

```powershell
# Windows PowerShell
$env:AZURE_CONFIG_DIR = "$HOME\.azure-localstack"
```

Register the emulator as a cloud and sign in with the placeholder account it accepts (any client id and secret work):

```bash
az cloud register --name LocalStack --endpoint-active-directory https://azure.localhost.localstack.cloud:4566 --endpoint-resource-manager https://azure.localhost.localstack.cloud:4566 --suffix-keyvault-dns .vault.azure.localhost.localstack.cloud:4566
az cloud set --name LocalStack
az config set core.instance_discovery=false
az login --service-principal --username any-app --password dummy --tenant 00000000-0000-0000-0000-000000000000
```

| Command | Why |
|---------|-----|
| `az cloud register` / `az cloud set` | tells the Azure CLI that "Azure" is now your LocalStack |
| `az config set core.instance_discovery=false` | the Azure CLI otherwise refuses an authority it doesn't know |
| `az login --service-principal …` | the emulator has a single tenant and accepts any service principal |

> [!TIP]
> `lstk az start-interception` ([LocalStack's CLI](https://docs.localstack.cloud/azure/connecting/azure-cli/)) does this registration in one command, but it changes your **real** Azure CLI login and it failed on our Windows test machine, so this guide does it by hand.

## 3. Create a vault

```bash
az group create --name envilder-cli --location westeurope
az keyvault create --name kv-envilder-cli --resource-group envilder-cli --location westeurope
```

The second command prints a `vaultUri`: `https://kv-envilder-cli.vault.azure.localhost.localstack.cloud:4566/`. The public DNS resolves `localhost.localstack.cloud` and all its subdomains to your own machine, so no hosts file is needed.

## 4. Create the map file

Work in a folder of your own (or in this one, `cli/azure`, which already has it) and create `envilder.json`. Key Vault secret names allow only letters, digits and dashes, so the identifier is a name, not a path:

```json
{
  "$config": {
    "provider": "azure",
    "vaultUrl": "https://kv-envilder-cli.vault.azure.localhost.localstack.cloud:4566"
  },
  "DEMO_SECRET": "envilder-cli-demo-secret"
}
```

## 5. Allow the emulator's vault host

Envilder only talks to `*.vault.azure.net` vaults by default. Allow the emulator's domain, in the same terminal where you set `AZURE_CONFIG_DIR`:

```bash
# macOS / Ubuntu
export ENVILDER_ALLOWED_VAULT_HOSTS=localhost.localstack.cloud
```

```powershell
# Windows PowerShell
$env:ENVILDER_ALLOWED_VAULT_HOSTS = 'localhost.localstack.cloud'
```

## 6. Push, pull and exec

The same commands as in the AWS guide, with the Key Vault secret name:

```bash
# push
npx envilder --push --key=DEMO_SECRET --value=hello-from-azure --secret-path=envilder-cli-demo-secret

# pull -> .env (add .env to your .gitignore)
npx envilder

# exec -> nothing on disk
npx envilder exec node -p "process.env.DEMO_SECRET"
```

The last one prints `hello-from-azure`. If a secret is missing, `exec` does **not** start the command and exits with `125`. Pushing a whole `.env` (`npx envilder --push`) works the same way.

## 7. Clean up

```bash
docker stop localstack-azure
```

Then delete the `.azure-localstack` folder in your home directory. Your real Azure login was never involved.

## Going to a real Azure vault

The commands are identical. You only leave the emulator behind:

1. Open a new terminal, or unset `AZURE_CONFIG_DIR` and `ENVILDER_ALLOWED_VAULT_HOSTS`, so the emulator settings don't leak into the real run.
2. Sign in with `az login` ([setup](../setup.md#your-clouds-cli)).
3. Set `"vaultUrl": "https://<your-vault>.vault.azure.net"` in the map file.

## Troubleshooting

| You see | Cause and fix |
|---------|---------------|
| `port is already allocated` | `4566` is in use, maybe by the AWS emulator. Stop it with `docker stop localstack` |
| LocalStack exits right away | The token is missing or wrong. Check `docker logs localstack-azure` |
| `invalid_instance` on `az login` | `az config set core.instance_discovery=false` is missing in this terminal (or `AZURE_CONFIG_DIR` isn't set) |
| `vaultUrl hostname must end with one of …` | `ENVILDER_ALLOWED_VAULT_HOSTS=localhost.localstack.cloud` is missing in this terminal |
| `Please run 'az login'` / Envilder can't authenticate | `AZURE_CONFIG_DIR` isn't set in this terminal, so the Azure CLI looks at your real login |
| Everything worked, then stopped working | You opened a new terminal. The variables live only in the terminal where you set them |
