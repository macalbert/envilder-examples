import json
import re
import uuid
from collections.abc import Generator
from pathlib import Path
from typing import Any
from urllib.request import urlopen

import boto3
import pytest
from envilder import AwsSsmSecretProvider, Envilder, EnvilderClient, MapFileParser
from mypy_boto3_ssm import SSMClient
from testcontainers.community.localstack import LocalStackContainer
from testcontainers.core.container import DockerContainer
from testcontainers.core.wait_strategies import LogMessageWaitStrategy

ROOT = Path(__file__).parent.parent


@pytest.fixture(scope="module")
def localstack() -> Generator[LocalStackContainer, None, None]:
    secrets = Envilder.resolve_file(str(ROOT / "envilder.json"))

    container = LocalStackContainer("localstack/localstack:stable")
    container.env.update(secrets)

    container.waiting_for(LogMessageWaitStrategy(re.compile(r"Ready\.")))
    DockerContainer.start(container)
    yield container
    container.stop()


@pytest.fixture(scope="module")
def ssm(localstack: LocalStackContainer) -> SSMClient:
    return boto3.client(
        "ssm",
        endpoint_url=localstack.get_url(),
        region_name="us-east-1",
        aws_access_key_id="test",
        aws_secret_access_key="test",
    )


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
        self, ssm: SSMClient
    ) -> None:
        # Arrange
        map_file = MapFileParser().parse((ROOT / "envilder.test.aws.json").read_text())
        expected = str(uuid.uuid4())

        ssm.put_parameter(
            Name=map_file.mappings["DEMO_SECRET"],
            Value=expected,
            Type="SecureString",
            Overwrite=True,
        )

        sut = EnvilderClient(AwsSsmSecretProvider(ssm))

        # Act
        actual = sut.resolve_secrets(map_file)

        # Assert
        assert actual["DEMO_SECRET"] == expected
