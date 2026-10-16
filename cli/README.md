# Envilder CLI from zero: push, pull and exec

**`npx envilder` · LocalStack (AWS SSM) · Lowkey Vault (Azure Key Vault)**

You have a brand new computer (macOS, Windows or Ubuntu) with nothing installed. By the end of this page you will have:

1. **pushed** a secret to a secret store,
2. **pulled** it into a `.env` file,
3. **run a command** with the secret in its environment, without writing any file (`exec`),

once against **AWS SSM** and once against **Azure Key Vault**. Both run in local emulators, so you don't need an AWS or Azure account to follow along.

| Command | What it does | Direction |
|---------|--------------|-----------|
| `npx envilder --push --key=… --value=… --secret-path=…` | stores one secret | you → store |
| `npx envilder --push` | stores every variable of your `.env` | `.env` → store |
| `npx envilder` | writes a `.env` with every secret of the map file | store → `.env` |
| `npx envilder exec <command>` | runs a command with the secrets in its environment, nothing on disk | store → memory → command |

> [!NOTE]
> Everything here was run against LocalStack and Lowkey Vault. Part 4 lists what changes for a real AWS account or Azure vault.

## Contents

1. [Install the tools](#1-install-the-tools)
2. [AWS SSM with LocalStack](#2-aws-ssm-with-localstack)
3. [Azure Key Vault with Lowkey Vault](#3-azure-key-vault-with-lowkey-vault)
4. [Going to a real cloud](#4-going-to-a-real-cloud)
5. [Troubleshooting](#5-troubleshooting)

## 1. Install the tools

You need two things: **Node.js** (it provides `npx`, which runs Envilder without installing it) and **Docker** (it runs the emulators).

| Tool | Version | Why |
|------|---------|-----|
| [Node.js](https://nodejs.org/) | 22.12 or newer | runs `npx envilder` |
| [Docker](https://www.docker.com/) | any recent | runs LocalStack and Lowkey Vault |

Open a terminal: **Terminal** on macOS and Ubuntu, **PowerShell** on Windows. Then follow your system.

### macOS

Install [Homebrew](https://brew.sh) if you don't have it (it is a one-line command on its home page), then:

```bash
brew install node
brew install --cask docker
```

Open **Docker** from Applications once and wait until it says it is running.

### Windows

PowerShell, with the built-in `winget`:

```powershell
winget install OpenJS.NodeJS.LTS
winget install Docker.DockerDesktop
```

Docker Desktop needs WSL 2 and will ask you to restart the computer. After the restart, open **Docker Desktop** and wait until it says it is running. **Close and reopen PowerShell** so it finds `node`.

### Ubuntu

Node.js from `apt` is often too old, so use [nvm](https://github.com/nvm-sh/nvm):

```bash
sudo apt update && sudo apt install -y curl
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.1/install.sh | bash
```

Close and reopen the terminal, then:

```bash
nvm install --lts
```

Install Docker Engine with Docker's [convenience script](https://docs.docker.com/engine/install/ubuntu/) and let your user run it without `sudo`:

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"
```

Log out and back in so the group change applies.

### Check it works

```bash
node --version
docker run --rm hello-world
```

`node` must print `v22.12` or higher, and Docker must print "Hello from Docker!". If so, you are done installing.

## 2. AWS SSM with LocalStack

[LocalStack](https://localstack.cloud) pretends to be AWS on your machine, so `envilder` can store secrets in "SSM Parameter Store" without an AWS account.

### 2.1 Get a LocalStack token

LocalStack needs an auth token to start. Create an account at [app.localstack.cloud](https://app.localstack.cloud), open **Auth Tokens** and copy yours. Put it in a variable of your terminal (this lasts until you close it):

```bash
# macOS / Ubuntu
export LOCALSTACK_AUTH_TOKEN=<your-token>
```

```powershell
# Windows PowerShell
$env:LOCALSTACK_AUTH_TOKEN = '<your-token>'
```

### 2.2 Start LocalStack

```bash
docker run -d --rm --name localstack -e LOCALSTACK_AUTH_TOKEN -p 4566:4566 localstack/localstack:stable
```

`-e LOCALSTACK_AUTH_TOKEN` (no `=value`) tells Docker to copy that variable from your terminal into the container. Wait about 20 seconds until `docker ps` shows `(healthy)` next to `localstack`.

### 2.3 Point Envilder at LocalStack

Envilder talks to AWS with the standard AWS variables. Set them to aim at the emulator:

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

### 2.4 Create the map file

A map file says which environment variable lives at which secret path. It holds paths, never values, so it is safe in Git. Work in a folder of your own (or `cli/aws` in this repo, which already has it) and create `envilder.json`:

```json
{
  "$config": {
    "provider": "aws"
  },
  "DEMO_SECRET": "/envilder-cli/demo-secret"
}
```

### 2.5 Push a secret

```bash
npx envilder --push --key=DEMO_SECRET --value=hello-from-cli --secret-path=/envilder-cli/demo-secret
```

The first run asks `Need to install the following packages: envilder`: answer `y`. You should see `SECRET PUSHED`. The secret now lives in SSM at `/envilder-cli/demo-secret`.

> [!TIP]
> On Windows with **Git Bash**, add `MSYS_NO_PATHCONV=1` in front of the command (or use PowerShell). Git Bash otherwise rewrites `/envilder-cli/…` into a Windows path.

### 2.6 Pull it into a `.env`

```bash
npx envilder
```

Envilder reads `envilder.json`, fetches every secret and writes `.env`:

```text
DEMO_SECRET=hello-from-cli
```

That file now has a secret in clear text: add `.env` to your `.gitignore`.

### 2.7 Run a command with the secret, no file

```bash
npx envilder exec node -p "process.env.DEMO_SECRET"
```

It prints `hello-from-cli`. Envilder fetched the secret, started `node` with it in its environment, and wrote nothing to disk. Everything after `exec` is your command, so this is how you would run your real app:

```bash
npx envilder exec npm run dev
```

If a secret is missing, the command is **not started** and Envilder exits with `125`. More in the [exec docs](https://github.com/macalbert/envilder/blob/main/docs/exec-command.md).

### 2.8 Push a whole `.env`

Edit the `.env` (say `DEMO_SECRET=changed`) and run:

```bash
npx envilder --push
```

Every variable of the `.env` that appears in the map file is stored at its path. Run `npx envilder exec node -p "process.env.DEMO_SECRET"` again and it prints `changed`.

### 2.9 Clean up

```bash
docker stop localstack
```

`--rm` made Docker delete the container, and the secrets with it.

## 3. Azure Key Vault with Lowkey Vault

[Lowkey Vault](https://github.com/nagyesta/lowkey-vault) is a free, offline Key Vault emulator. It needs no token. If you did Part 2, the commands are the same: only the map file and a few variables change.

### 3.1 Start Lowkey Vault

```bash
docker run -d --rm --name lowkey -p 8443:8443 -p 8080:8080 -e LOWKEY_ARGS="--server.port=8443 --LOWKEY_VAULT_RELAXED_PORTS=true" nagyesta/lowkey-vault:7.1.61
```

Wait until `docker logs lowkey` ends with `Started LowkeyVaultApp` (a few seconds; about 40 s on Apple Silicon and Windows on ARM, which run it under emulation).

### 3.2 Point Envilder at Lowkey Vault

```bash
# macOS / Ubuntu
export IDENTITY_ENDPOINT=http://localhost:8080/metadata/identity/oauth2/token
export IDENTITY_HEADER=dummy
export ENVILDER_ALLOWED_VAULT_HOSTS=localhost
export NODE_TLS_REJECT_UNAUTHORIZED=0
export NODE_NO_WARNINGS=1
```

```powershell
# Windows PowerShell
$env:IDENTITY_ENDPOINT = 'http://localhost:8080/metadata/identity/oauth2/token'
$env:IDENTITY_HEADER = 'dummy'
$env:ENVILDER_ALLOWED_VAULT_HOSTS = 'localhost'
$env:NODE_TLS_REJECT_UNAUTHORIZED = '0'
$env:NODE_NO_WARNINGS = '1'
```

| Variable | Why |
|----------|-----|
| `IDENTITY_ENDPOINT` / `IDENTITY_HEADER` | Azure's default login gets its token from here, as it would from Azure on an App Service. Lowkey Vault hands out fake tokens |
| `ENVILDER_ALLOWED_VAULT_HOSTS` | Envilder only accepts `*.vault.azure.net` vaults by default; this also allows `localhost` |
| `NODE_TLS_REJECT_UNAUTHORIZED=0` | Lowkey Vault uses a self-signed HTTPS certificate. **Only for this emulator** |
| `NODE_NO_WARNINGS=1` | hides the warning Node prints because of the line above |

### 3.3 Create the map file

Create `envilder.json` in a new folder (or use `cli/azure` in this repo). Key Vault secret names allow only letters, digits and dashes, so the identifier is a name, not a path:

```json
{
  "$config": {
    "provider": "azure",
    "vaultUrl": "https://localhost:8443"
  },
  "DEMO_SECRET": "envilder-cli-demo-secret"
}
```

### 3.4 Push, pull and exec

The same three commands as in Part 2, with the Key Vault secret name:

```bash
# push
npx envilder --push --key=DEMO_SECRET --value=hello-from-azure --secret-path=envilder-cli-demo-secret

# pull -> .env
npx envilder

# exec -> nothing on disk
npx envilder exec node -p "process.env.DEMO_SECRET"
```

The last one prints `hello-from-azure`. Pushing a whole `.env` (`npx envilder --push`) works the same way.

### 3.5 Clean up

```bash
docker stop lowkey
```

## 4. Going to a real cloud

The commands are identical. You only remove the emulator and log in for real.

| | AWS SSM | Azure Key Vault |
|---|---------|-----------------|
| Install | [AWS CLI](https://aws.amazon.com/cli/) | [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) |
| Log in | `aws configure` or `aws sso login` | `az login` |
| Remove | `AWS_ENDPOINT_URL` and the fake keys | `IDENTITY_*`, `ENVILDER_ALLOWED_VAULT_HOSTS`, `NODE_*` |
| Map file | `"provider": "aws"` (add `"profile": "my-profile"` to use a named profile) | `"provider": "azure"` and `"vaultUrl": "https://<your-vault>.vault.azure.net"` |

Open a new terminal, or unset the variables from Parts 2 and 3, so they don't leak into the real run.

A nice use of `exec`: keep the LocalStack token itself in your real cloud and start LocalStack with it, without ever typing it again. See [`envilder exec` in the root README](../README.md#run-any-command-with-its-secrets-envilder-exec).

## 5. Troubleshooting

| You see | Cause and fix |
|---------|---------------|
| `node: command not found` / `npx: command not found` | Node isn't installed, or the terminal was open before you installed it. Close it and open a new one |
| `Cannot connect to the Docker daemon` | Docker isn't running. Open Docker Desktop (macOS, Windows) or `sudo systemctl start docker` (Ubuntu) |
| `permission denied` on `/var/run/docker.sock` (Ubuntu) | You didn't log out after `usermod -aG docker`. Log out and in |
| `port is already allocated` | `4566`, `8443` or `8080` is in use. Stop the old container with `docker stop localstack lowkey` |
| `secret not found … skipped` | The secret was never pushed, or it went to another path. On Git Bash see the tip in [2.5](#25-push-a-secret) |
| `Unable to locate credentials` (AWS) | The AWS variables of [2.3](#23-point-envilder-at-localstack) are missing in this terminal |
| `vaultUrl hostname must end with one of …` | `ENVILDER_ALLOWED_VAULT_HOSTS=localhost` is missing in this terminal |
| `self-signed certificate` (Azure) | `NODE_TLS_REJECT_UNAUTHORIZED=0` is missing in this terminal |
| LocalStack exits right away | The token is missing or wrong. Check `docker logs localstack` |
| Everything worked, then stopped working | You opened a new terminal. The variables live only in the terminal where you set them |
