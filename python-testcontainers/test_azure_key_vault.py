import uuid
from collections.abc import Generator
from pathlib import Path

import pytest
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
from envilder import AzureKeyVaultSecretProvider, Envilder, EnvilderClient, MapFileParser

from localstack_azure import localstack_azure_key_vault

ROOT = Path(__file__).parent.parent


@pytest.fixture(scope="module")
def secrets() -> Generator[SecretClient, None, None]:
    token = Envilder.resolve_file(str(ROOT / "envilder.json"))

    with localstack_azure_key_vault(token) as vault_url:
        yield SecretClient(vault_url=vault_url, credential=DefaultAzureCredential())


class TestAzureKeyVault:
    def Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt(
        self, secrets: SecretClient
    ) -> None:
        # Arrange
        map_file = MapFileParser().parse((ROOT / "envilder.test.azure.json").read_text())
        expected = str(uuid.uuid4())

        secrets.set_secret(map_file.mappings["DEMO_SECRET"], expected)

        sut = EnvilderClient(AzureKeyVaultSecretProvider(secrets))

        # Act
        actual = sut.resolve_secrets(map_file)

        # Assert
        assert actual["DEMO_SECRET"] == expected
