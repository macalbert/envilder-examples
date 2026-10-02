import os
import uuid
from collections.abc import Generator
from pathlib import Path

import pytest
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
from envilder import AzureKeyVaultSecretProvider, EnvilderClient, MapFileParser
from testcontainers.core.container import DockerContainer
from testcontainers.core.wait_strategies import LogMessageWaitStrategy

ROOT = Path(__file__).parent.parent

VAULT_PORT = 8443
TOKEN_PORT = 8080


@pytest.fixture(scope="module")
def secrets() -> Generator[SecretClient, None, None]:
    container = (
        DockerContainer("nagyesta/lowkey-vault:7.1.61")
        .with_exposed_ports(VAULT_PORT, TOKEN_PORT)
        .with_env("LOWKEY_ARGS", "--server.port=8443 --LOWKEY_VAULT_RELAXED_PORTS=true")
        .waiting_for(
            LogMessageWaitStrategy("Started LowkeyVaultApp").with_startup_timeout(180)
        )
    )
    container.start()

    host = container.get_container_host_ip()

    os.environ["IDENTITY_ENDPOINT"] = (
        f"http://{host}:{container.get_exposed_port(TOKEN_PORT)}/metadata/identity/oauth2/token"
    )
    os.environ["IDENTITY_HEADER"] = "dummy"

    yield SecretClient(
        vault_url=f"https://{host}:{container.get_exposed_port(VAULT_PORT)}",
        credential=DefaultAzureCredential(),
        connection_verify=False,
        verify_challenge_resource=False,
        api_version="7.6",
    )

    container.stop()
    os.environ.pop("IDENTITY_ENDPOINT", None)
    os.environ.pop("IDENTITY_HEADER", None)


class TestAzureKeyVault:
    def Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt(
        self, secrets: SecretClient
    ) -> None:
        # Arrange
        map_file = MapFileParser().parse((ROOT / "envilder.test.azure.json").read_text())
        expected = str(uuid.uuid4())

        secrets.set_secret(map_file.mappings["DEMO_SECRET"], expected)

        # Act
        envilder = EnvilderClient(AzureKeyVaultSecretProvider(secrets))
        actual = envilder.resolve_secrets(map_file)

        # Assert
        assert actual["DEMO_SECRET"] == expected
