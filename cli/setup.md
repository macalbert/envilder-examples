# 1. Setup

[← Back to the CLI guide](./README.md) · Next: [AWS SSM](./aws/README.md) or [Azure Key Vault](./azure/README.md)

You need three things: **Node.js** (it provides `npx`, which runs Envilder without installing it), **Docker** (it runs LocalStack) and the **CLI of your cloud**, which is the last section ([AWS CLI or Azure CLI](#your-clouds-cli)).

| Tool | Version | Why | Official install docs |
|------|---------|-----|-----------------------|
| [Node.js](https://nodejs.org/) | 22.12 or newer | runs `npx envilder` | [Download](https://nodejs.org/en/download) · [nvm](https://github.com/nvm-sh/nvm#installing-and-updating) |
| [Docker](https://www.docker.com/) | any recent | runs LocalStack | [macOS](https://docs.docker.com/desktop/setup/install/mac-install/) · [Windows](https://docs.docker.com/desktop/setup/install/windows-install/) · [Ubuntu](https://docs.docker.com/engine/install/ubuntu/) |

Open a terminal: **Terminal** on macOS and Ubuntu, **PowerShell** on Windows. Then follow your system.

## macOS

Install [Homebrew](https://brew.sh) if you don't have it (it is a one-line command on its home page), then:

```bash
brew install node
brew install --cask docker
```

Open **Docker** from Applications once and wait until it says it is running. Docs: [Homebrew](https://brew.sh), [Docker Desktop for Mac](https://docs.docker.com/desktop/setup/install/mac-install/).

## Windows

PowerShell, with the built-in [`winget`](https://learn.microsoft.com/en-us/windows/package-manager/winget/):

```powershell
winget install OpenJS.NodeJS.LTS
winget install Docker.DockerDesktop
```

Docker Desktop needs [WSL 2](https://learn.microsoft.com/en-us/windows/wsl/install) and will ask you to restart the computer. After the restart, open **Docker Desktop** and wait until it says it is running. **Close and reopen PowerShell** so it finds `node`. Docs: [Docker Desktop for Windows](https://docs.docker.com/desktop/setup/install/windows-install/), [Node.js](https://nodejs.org/en/download).

## Ubuntu

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

Log out and back in so the group change applies. Docs: [Docker Engine on Ubuntu](https://docs.docker.com/engine/install/ubuntu/), [running Docker without `sudo`](https://docs.docker.com/engine/install/linux-postinstall/), [nvm](https://github.com/nvm-sh/nvm#installing-and-updating).

## Check it works

```bash
node --version
docker run --rm hello-world
```

`node` must print `v22.12` or higher, and Docker must print "Hello from Docker!". If so, you are done: continue with [AWS SSM](./aws/README.md) or [Azure Key Vault](./azure/README.md).

## Your cloud's CLI

Envilder reads and writes secrets in a real cloud, so install the CLI of the one you use: the **AWS CLI**, the **Azure CLI**, or both. It is how you log in to your account. Both guides need you to be logged in to that cloud first.

| Tool | Log in with | Used for | Official docs |
|------|-------------|----------|---------------|
| [AWS CLI](https://aws.amazon.com/cli/) v2 | `aws configure` or `aws sso login` | real AWS SSM | [Install](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html) · [Configure](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-quickstart.html) · [SSO](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sso.html) |
| [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) | `az login` | real Azure Key Vault | [Install](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) · [Sign in](https://learn.microsoft.com/en-us/cli/azure/authenticate-azure-cli) |

### macOS

```bash
brew install awscli azure-cli
```

### Windows

```powershell
winget install Amazon.AWSCLI
winget install Microsoft.AzureCLI
```

Close and reopen PowerShell afterwards.

### Ubuntu

```bash
sudo snap install aws-cli --classic
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
```

### Check it works

```bash
aws --version
az --version
```

## Troubleshooting

| You see | Cause and fix |
|---------|---------------|
| `node: command not found` / `npx: command not found` | Node isn't installed, or the terminal was open before you installed it. Close it and open a new one |
| `Cannot connect to the Docker daemon` | Docker isn't running. Open Docker Desktop (macOS, Windows) or `sudo systemctl start docker` (Ubuntu) |
| `permission denied` on `/var/run/docker.sock` (Ubuntu) | You didn't log out after `usermod -aG docker`. Log out and in |
