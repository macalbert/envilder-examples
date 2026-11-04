import uuid

from azure.keyvault.secrets import SecretClient
from envilder import AzureKeyVaultSecretProvider, EnvilderClient

from mapfiles import ROOT, with_unique_secrets


class TestAzureKeyVault:
    def Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt(
        self, secrets: SecretClient, stored_names: list[str]
    ) -> None:
        # Arrange
        map_file = with_unique_secrets(
            ROOT / "envilder.test.azure.json", lambda name: f"{name}-{uuid.uuid4().hex}"
        )
        expected = str(uuid.uuid4())

        stored_names.append(map_file.mappings["DEMO_SECRET"])
        secrets.set_secret(map_file.mappings["DEMO_SECRET"], expected)

        sut = EnvilderClient(AzureKeyVaultSecretProvider(secrets))

        # Act
        actual = sut.resolve_secrets(map_file)

        # Assert
        assert actual["DEMO_SECRET"] == expected
