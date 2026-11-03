import json
import uuid
from collections.abc import Generator
from pathlib import Path

import pytest
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
from envilder import (
    AzureKeyVaultSecretProvider,
    ContractValidationError,
    Envilder,
    EnvilderClient,
    MapFileParser,
    SecretExistenceStatus,
)

from localstack_azure import localstack_azure_key_vault

ROOT = Path(__file__).parent.parent


@pytest.fixture(scope="module")
def secrets() -> Generator[SecretClient, None, None]:
    token = Envilder.resolve_file(str(ROOT / "envilder.json"))

    with localstack_azure_key_vault(token) as vault_url:
        yield SecretClient(vault_url=vault_url, credential=DefaultAzureCredential())


def set_demo_secret(secrets: SecretClient) -> None:
    map_file = MapFileParser().parse((ROOT / "envilder.test.azure.json").read_text())
    secrets.set_secret(map_file.mappings["DEMO_SECRET"], str(uuid.uuid4()))


def map_file_with_extra_key(tmp_path: Path, variable: str, secret_name: str) -> Path:
    map_file = json.loads((ROOT / "envilder.test.azure.json").read_text())
    map_file[variable] = secret_name
    path = tmp_path / "envilder.test.azure.json"
    path.write_text(json.dumps(map_file))
    return path


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

    def Should_ReportEveryKeyAsExists_When_ContractHoldsInKeyVault(
        self, secrets: SecretClient
    ) -> None:
        # Arrange
        set_demo_secret(secrets)

        # Act
        actual = (
            Envilder.from_map_file(str(ROOT / "envilder.test.azure.json"))
            .with_vault_url(secrets.vault_url)
            .validate_contract()
        )

        # Assert
        assert actual.is_valid
        assert [k.status for k in actual.keys] == [SecretExistenceStatus.EXISTS]

    def Should_ReportKeyAsMissing_When_SecretIsNotInKeyVault(
        self, secrets: SecretClient, tmp_path: Path
    ) -> None:
        # Arrange
        set_demo_secret(secrets)
        map_file = map_file_with_extra_key(
            tmp_path, "NOT_PUSHED_YET", "envilder-examples-not-pushed-yet"
        )

        # Act
        actual = (
            Envilder.from_map_file(str(map_file))
            .with_vault_url(secrets.vault_url)
            .validate_contract()
        )

        # Assert
        assert not actual.is_valid
        assert {k.variable: k.status for k in actual.keys} == {
            "DEMO_SECRET": SecretExistenceStatus.EXISTS,
            "NOT_PUSHED_YET": SecretExistenceStatus.MISSING,
        }

    def Should_FailTheLoad_When_ContractValidationIsOnAndSecretIsNotInKeyVault(
        self, secrets: SecretClient, tmp_path: Path
    ) -> None:
        # Arrange
        set_demo_secret(secrets)
        map_file = map_file_with_extra_key(
            tmp_path, "NOT_PUSHED_YET", "envilder-examples-not-pushed-yet"
        )
        sut = (
            Envilder.from_map_file(str(map_file))
            .with_vault_url(secrets.vault_url)
            .with_contract_validation()
        )

        # Act / Assert
        with pytest.raises(
            ContractValidationError,
            match=r"NOT_PUSHED_YET \(envilder-examples-not-pushed-yet\): missing",
        ):
            sut.resolve()
