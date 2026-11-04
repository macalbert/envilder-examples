import json
import uuid
from typing import Any
from urllib.request import urlopen

from envilder import AwsSsmSecretProvider, EnvilderClient
from mypy_boto3_ssm import SSMClient
from testcontainers.community.localstack import LocalStackContainer

from mapfiles import ROOT, with_unique_secrets


def get_json(url: str) -> Any:
    with urlopen(url) as response:
        return json.load(response)


class TestAwsSsm:
    def Should_ActivateLicense_When_LocalStackStartsWithTokenResolvedByEnvilder(
        self, localstack: LocalStackContainer
    ) -> None:
        # Act
        info = get_json(f"{localstack.get_url()}/_localstack/info")

        # Assert
        assert info["is_license_activated"] is True

    def Should_ResolveSecretFromSsm_When_MapFilePointsToIt(
        self, ssm: SSMClient, stored_paths: list[str]
    ) -> None:
        # Arrange
        map_file = with_unique_secrets(
            ROOT / "envilder.test.aws.json", lambda path: f"{path}/{uuid.uuid4().hex}"
        )
        expected = str(uuid.uuid4())

        stored_paths.append(map_file.mappings["DEMO_SECRET"])
        ssm.put_parameter(
            Name=map_file.mappings["DEMO_SECRET"],
            Value=expected,
            Type="SecureString",
        )

        sut = EnvilderClient(AwsSsmSecretProvider(ssm))

        # Act
        actual = sut.resolve_secrets(map_file)

        # Assert
        assert actual["DEMO_SECRET"] == expected
