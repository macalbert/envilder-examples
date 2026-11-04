import socket
import uuid
from collections.abc import Generator
from contextlib import contextmanager
from typing import Any

import requests
from azure.core.credentials import TokenCredential
from azure.identity import ClientSecretCredential
from azure.keyvault.secrets import SecretClient
from testcontainers.core.container import DockerContainer
from testcontainers.core.wait_strategies import LogMessageWaitStrategy

IMAGE = "localstack/localstack-azure:latest"

# The emulator accepts any service principal in its single all-zero tenant and
# subscription. These are placeholders, not secrets.
ZERO_GUID = "00000000-0000-0000-0000-000000000000"


@contextmanager
def localstack_azure_key_vault(
    environment: dict[str, str],
) -> Generator[SecretClient, None, None]:
    """Starts LocalStack for Azure, creates a vault and yields a client for it."""
    # Vault URLs embed the gateway port, so the host port must equal the port
    # the emulator listens on inside the container.
    port = _find_free_port()
    suffix = uuid.uuid4().hex[:8]
    authority = f"https://azure.localhost.localstack.cloud:{port}"

    container = (
        DockerContainer(IMAGE)
        .with_name(f"localstack-azure-{suffix}")
        .with_bind_ports(port, port)
        .with_env("GATEWAY_LISTEN", f"0.0.0.0:{port}")
        .with_env("LOCALSTACK_HOST", f"localhost.localstack.cloud:{port}")
        .waiting_for(LogMessageWaitStrategy(r"Ready\.").with_startup_timeout(180))
    )
    for name, value in environment.items():
        container.with_env(name, value)

    try:
        container.start()

        credential = ClientSecretCredential(
            ZERO_GUID,
            "any-app",
            "dummy",
            authority=authority,
            disable_instance_discovery=True,
        )
        vault_url = _create_vault(authority, credential, f"kv-{suffix}")

        yield SecretClient(vault_url=vault_url, credential=credential)
    finally:
        container.stop()


def _create_vault(authority: str, credential: TokenCredential, vault_name: str) -> str:
    token = credential.get_token("https://management.azure.com/.default").token
    headers = {"Authorization": f"Bearer {token}"}
    group_url = f"{authority}/subscriptions/{ZERO_GUID}/resourceGroups/envilder-tests"

    _put(f"{group_url}?api-version=2021-04-01", headers, {"location": "westeurope"})
    vault = _put(
        f"{group_url}/providers/Microsoft.KeyVault/vaults/{vault_name}"
        "?api-version=2023-07-01",
        headers,
        {
            "location": "westeurope",
            "properties": {
                "tenantId": ZERO_GUID,
                "sku": {"family": "A", "name": "standard"},
                "accessPolicies": [],
            },
        },
    )
    return str(vault["properties"]["vaultUri"]).rstrip("/")


def _put(url: str, headers: dict[str, str], body: dict[str, Any]) -> dict[str, Any]:
    response = requests.put(url, headers=headers, json=body, timeout=30)
    response.raise_for_status()
    result: dict[str, Any] = response.json()
    return result


def _find_free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("", 0))
        port: int = sock.getsockname()[1]
        return port
