import json
import re
from collections.abc import Generator
from pathlib import Path
from urllib.request import urlopen

import boto3
import pytest
from envilder import Envilder
from mypy_boto3_ssm import SSMClient
from testcontainers.core.container import DockerContainer
from testcontainers.core.wait_strategies import LogMessageWaitStrategy
from testcontainers.localstack import LocalStackContainer

MAP_FILE = Path(__file__).parent.parent / "envilder.json"


@pytest.fixture(scope="module")
def localstack() -> Generator[LocalStackContainer, None, None]:
    container = LocalStackContainer("localstack/localstack:stable")
    container.env.update(Envilder.resolve_file(str(MAP_FILE)))

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


class TestLocalStackSecrets:
    def Should_ReportSsmService_When_LocalStackIsRunning(
        self, localstack: LocalStackContainer
    ) -> None:
        # Act
        with urlopen(f"{localstack.get_url()}/_localstack/health") as response:
            health = json.load(response)

        # Assert
        assert health["services"]["ssm"] in ("available", "running")

    def Should_RoundTripSecureString_When_LocalStackStartsWithResolvedToken(
        self, ssm: SSMClient
    ) -> None:
        # Arrange
        ssm.put_parameter(
            Name="/demo/secret", Value="hunter2", Type="SecureString"
        )

        # Act
        actual = ssm.get_parameter(Name="/demo/secret", WithDecryption=True)

        # Assert
        assert actual["Parameter"].get("Value") == "hunter2"
