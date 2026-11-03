import json
import re
import uuid
from collections.abc import Generator
from pathlib import Path
from typing import Any
from urllib.request import urlopen

import boto3
import pytest
from envilder import (
    AwsSsmSecretProvider,
    ContractValidationError,
    Envilder,
    EnvilderClient,
    MapFileParser,
    SecretExistenceStatus,
)
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


@pytest.fixture
def default_aws_client_on_localstack(
    localstack: LocalStackContainer, monkeypatch: pytest.MonkeyPatch
) -> None:
    """Aims the boto3 client Envilder.from_map_file builds at LocalStack."""
    monkeypatch.setenv("AWS_ENDPOINT_URL_SSM", localstack.get_url())
    monkeypatch.setenv("AWS_ACCESS_KEY_ID", "test")
    monkeypatch.setenv("AWS_SECRET_ACCESS_KEY", "test")
    monkeypatch.setenv("AWS_REGION", "us-east-1")
    monkeypatch.delenv("AWS_SESSION_TOKEN", raising=False)
    monkeypatch.delenv("AWS_PROFILE", raising=False)


def put_demo_secret(ssm: SSMClient) -> None:
    map_file = MapFileParser().parse((ROOT / "envilder.test.aws.json").read_text())
    ssm.put_parameter(
        Name=map_file.mappings["DEMO_SECRET"],
        Value=str(uuid.uuid4()),
        Type="SecureString",
        Overwrite=True,
    )


def map_file_with_extra_key(tmp_path: Path, variable: str, secret_path: str) -> Path:
    map_file = json.loads((ROOT / "envilder.test.aws.json").read_text())
    map_file[variable] = secret_path
    path = tmp_path / "envilder.test.aws.json"
    path.write_text(json.dumps(map_file))
    return path


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

    @pytest.mark.usefixtures("default_aws_client_on_localstack")
    def Should_ReportEveryKeyAsExists_When_ContractHoldsInSsm(
        self, ssm: SSMClient
    ) -> None:
        # Arrange
        put_demo_secret(ssm)

        # Act
        actual = Envilder.from_map_file(str(ROOT / "envilder.test.aws.json")).validate_contract()

        # Assert
        assert actual.is_valid
        assert [k.status for k in actual.keys] == [SecretExistenceStatus.EXISTS]

    @pytest.mark.usefixtures("default_aws_client_on_localstack")
    def Should_ReportKeyAsMissing_When_SecretIsNotInSsm(
        self, ssm: SSMClient, tmp_path: Path
    ) -> None:
        # Arrange
        put_demo_secret(ssm)
        map_file = map_file_with_extra_key(
            tmp_path, "NOT_PUSHED_YET", "/envilder-examples/not-pushed-yet"
        )

        # Act
        actual = Envilder.from_map_file(str(map_file)).validate_contract()

        # Assert
        assert not actual.is_valid
        assert {k.variable: k.status for k in actual.keys} == {
            "DEMO_SECRET": SecretExistenceStatus.EXISTS,
            "NOT_PUSHED_YET": SecretExistenceStatus.MISSING,
        }

    @pytest.mark.usefixtures("default_aws_client_on_localstack")
    def Should_FailTheLoad_When_ContractValidationIsOnAndSecretIsNotInSsm(
        self, ssm: SSMClient, tmp_path: Path
    ) -> None:
        # Arrange
        put_demo_secret(ssm)
        map_file = map_file_with_extra_key(
            tmp_path, "NOT_PUSHED_YET", "/envilder-examples/not-pushed-yet"
        )
        sut = Envilder.from_map_file(str(map_file)).with_contract_validation()

        # Act / Assert
        with pytest.raises(
            ContractValidationError,
            match=r"NOT_PUSHED_YET \(/envilder-examples/not-pushed-yet\): missing",
        ):
            sut.resolve()
