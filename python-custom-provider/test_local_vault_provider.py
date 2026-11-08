from __future__ import annotations

import os
from collections.abc import Generator
from pathlib import Path

import pytest
from envilder import ContractValidationError, Envilder, SecretExistenceStatus

from local_vault import generate_key, seal, write_vault
from local_vault_provider import LocalVaultProvider

MAP_FILE = str(Path(__file__).parent.parent / "envilder.vault.json")
KEY = generate_key()


@pytest.fixture()
def vault_path(tmp_path: Path) -> Generator[Path, None, None]:
    yield tmp_path / "vault.json"
    for name in ("DB_PASSWORD", "API_KEY"):
        os.environ.pop(name, None)


class TestLocalVaultProvider:
    def Should_InjectDecryptedSecrets_When_VaultHoldsEveryMappedSecret(
        self, vault_path: Path
    ) -> None:
        # Arrange
        write_vault(
            vault_path,
            {
                "/envilder-examples/db-password": seal(KEY, "correct-horse"),
                "/envilder-examples/api-key": seal(KEY, "battery-staple"),
            },
        )

        # Act
        (
            Envilder.from_map_file(MAP_FILE)
            .with_secret_provider(LocalVaultProvider(vault_path, KEY))
            .with_contract_validation()
            .inject()
        )

        # Assert
        assert os.environ["DB_PASSWORD"] == "correct-horse"
        assert os.environ["API_KEY"] == "battery-staple"

    def Should_FailAtStartupWithoutInjecting_When_MappedSecretIsMissing(
        self, vault_path: Path
    ) -> None:
        # Arrange
        write_vault(
            vault_path,
            {"/envilder-examples/db-password": seal(KEY, "correct-horse")},
        )
        sut = (
            Envilder.from_map_file(MAP_FILE)
            .with_secret_provider(LocalVaultProvider(vault_path, KEY))
            .with_contract_validation()
        )

        # Act
        act = sut.inject

        # Assert
        with pytest.raises(
            ContractValidationError,
            match=r"API_KEY \(/envilder-examples/api-key\): missing",
        ):
            act()
        assert "DB_PASSWORD" not in os.environ

    def Should_ReportEachKeyWithoutDecrypting_When_ContractIsValidated(
        self, vault_path: Path
    ) -> None:
        # Arrange
        write_vault(
            vault_path,
            {"/envilder-examples/db-password": seal(KEY, "correct-horse")},
        )
        wrong_key = generate_key()

        # Act
        actual = (
            Envilder.from_map_file(MAP_FILE)
            .with_secret_provider(LocalVaultProvider(vault_path, wrong_key))
            .validate_contract()
        )

        # Assert
        assert [(k.variable, k.status) for k in actual.keys] == [
            ("DB_PASSWORD", SecretExistenceStatus.EXISTS),
            ("API_KEY", SecretExistenceStatus.MISSING),
        ]
