# Envilder CLI from zero: push, pull and exec

**`npx envilder` · AWS SSM and Azure Key Vault**

The example is a real one: keep the LocalStack auth token in a secret store and start LocalStack with it. By the end you will have:

1. **pushed** a secret to a secret store,
2. **pulled** it into a `.env` file,
3. **run a command** with the secret in its environment, without writing any file (`exec`),

with **AWS SSM** or with **Azure Key Vault**. You need an account in the cloud you pick.

## Follow these in order

| Step | Guide | What you do |
|------|-------|-------------|
| 1 | [Setup](./setup.md) | install Node.js, Docker and the AWS or Azure CLI |
| 2 | [AWS SSM](./aws/README.md) | push, pull and exec with the LocalStack token |
| 3 | [Azure Key Vault](./azure/README.md) | push, pull and exec with the LocalStack token (needs the Azure CLI) |

Steps 2 and 3 are independent: do the one you need. Each has its own map file next to its guide ([`aws/envilder.json`](./aws/envilder.json), [`azure/envilder.json`](./azure/envilder.json)).

## The four commands

| Command | What it does | Direction |
|---------|--------------|-----------|
| `npx envilder --push --key=… --value=… --secret-path=…` | stores one secret | you → store |
| `npx envilder --push` | stores every variable of your `.env` | `.env` → store |
| `npx envilder` | writes a `.env` with every secret of the map file | store → `.env` |
| `npx envilder exec <command>` | runs a command with the secrets in its environment, nothing on disk | store → memory → command |

Envilder's own docs: [requirements and installation](https://github.com/macalbert/envilder/blob/main/docs/requirements-installation.md), [push](https://github.com/macalbert/envilder/blob/main/docs/push-command.md), [pull](https://github.com/macalbert/envilder/blob/main/docs/pull-command.md), [exec](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).
