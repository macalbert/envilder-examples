from __future__ import annotations

from collections.abc import Collection, Mapping
from pathlib import Path

from envilder import (
    ISecretExistenceChecker,
    ISecretProvider,
    SecretExistence,
    SecretExistenceStatus,
)

from local_vault import open_sealed, read_vault


class LocalVaultProvider(ISecretProvider, ISecretExistenceChecker):
    """The whole custom provider.

    Envilder calls these two methods and nothing else: it never knows the
    secrets come from an encrypted file.
    """

    def __init__(self, vault_path: Path, key: bytes) -> None:
        self._vault_path = vault_path
        self._key = key

    # ISecretProvider: return the value, or None when the secret is missing.
    def get_secret(self, name: str) -> str | None:
        sealed = read_vault(self._vault_path).get(name)
        return None if sealed is None else open_sealed(self._key, sealed)

    # ISecretExistenceChecker: answer "does it exist?" without decrypting.
    # This is what lets with_contract_validation() check the map file at
    # startup.
    def check_secrets_exist(
        self, names: Collection[str]
    ) -> Mapping[str, SecretExistence]:
        vault = read_vault(self._vault_path)
        return {
            name: SecretExistence(
                SecretExistenceStatus.EXISTS
                if name in vault
                else SecretExistenceStatus.MISSING
            )
            for name in names
        }
