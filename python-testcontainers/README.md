# Python + Testcontainers

**pytest · Testcontainers · uv · LocalStack · Lowkey Vault**

```bash
uv run pytest
```

> **Before you run:** [`envilder.json`](../envilder.json) maps `LOCALSTACK_AUTH_TOKEN` to the SSM key `/envilder/development/localstack/authToken`, so that key must hold your LocalStack token in your AWS account, and you need the [AWS CLI](https://aws.amazon.com/cli/) installed and logged in (`aws configure` or `aws sso login`). See [Store your LocalStack token](../README.md#store-your-localstack-token-once).

## What happens when you run it

```mermaid
flowchart LR
    subgraph cloud["☁️ Your AWS account"]
        token[("SSM<br/>LOCALSTACK_AUTH_TOKEN")]
    end

    subgraph docker["🐳 Docker, started by Testcontainers"]
        localstack["LocalStack<br/><i>emulated SSM</i>"]
        lowkey["Lowkey Vault<br/><i>emulated Key Vault</i>"]
    end

    token -- "Envilder.resolve_file('envilder.json')" --> localstack
    aws["🧪 test_aws_ssm.py"] --> localstack
    azure["🧪 test_azure_key_vault.py"] --> lowkey
```

Three tests, two stories (see the [root README](../README.md#what-the-tests-prove)):

| Test | Proves |
|------|--------|
| `Should_ActivateLicense_When_LocalStackStartsWithTokenResolvedByEnvilder` | Envilder fetched the real token and LocalStack accepted it |
| `Should_ResolveSecretFromSsm_When_MapFilePointsToIt` | Envilder resolves `envilder.test.aws.json` against SSM |
| `Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt` | Envilder resolves `envilder.test.azure.json` against Key Vault |

## Read the code in this order

1. **[`test_aws_ssm.py`](./test_aws_ssm.py)**: the `localstack` fixture is the whole integration in two statements, `Envilder.resolve_file(...)` and `container.env.update(secrets)`. Then come the first two tests. In the second, look at the `Act` block: that is everything Envilder does.
2. **[`test_azure_key_vault.py`](./test_azure_key_vault.py)** is the same test against Key Vault. Its `secrets` fixture is emulator plumbing, so skim it. The test itself only changes the map file and the client.

## The line to look at

Your app resolves a map file in one call, and Envilder builds the cloud client for you:

```python
secrets = Envilder.resolve_file("envilder.test.aws.json")
```

The tests need that client aimed at an emulator, so they build it themselves and hand it to Envilder:

```python
sut = EnvilderClient(AwsSsmSecretProvider(ssm))
secrets = sut.resolve_secrets(map_file)
```

Everything after that line is the same code your app runs.

## Emulator-only bits

You won't need these against real clouds:

| What | Why |
|------|-----|
| `aws_access_key_id="test"`, … | LocalStack accepts any credentials |
| `IDENTITY_ENDPOINT` / `IDENTITY_HEADER` | `DefaultAzureCredential` gets its token from Lowkey Vault, as it would from Azure on an App Service |
| `connection_verify=False` | Lowkey Vault uses a self-signed certificate (the warning is silenced in `pyproject.toml`) |
| `verify_challenge_resource=False` | the vault isn't on `*.vault.azure.net` |
| `api_version="7.6"` | the newest Key Vault API version Lowkey Vault speaks |
