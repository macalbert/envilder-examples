"""One emulator of each kind for the whole test session, shared by every test.

Tests stay independent: each one stores its own uniquely named secrets and
deletes them when it ends.
"""

import re
from collections.abc import Generator

import boto3
import pytest
from azure.keyvault.secrets import SecretClient
from envilder import Envilder
from mypy_boto3_ssm import SSMClient
from testcontainers.community.localstack import LocalStackContainer
from testcontainers.core.container import DockerContainer
from testcontainers.core.wait_strategies import LogMessageWaitStrategy

from localstack_azure import localstack_azure_key_vault
from mapfiles import ROOT


@pytest.fixture(scope="session")
def localstack() -> Generator[LocalStackContainer, None, None]:
    secrets = Envilder.resolve_file(str(ROOT / "envilder.json"))

    container = LocalStackContainer("localstack/localstack:stable")
    container.env.update(secrets)

    container.waiting_for(LogMessageWaitStrategy(re.compile(r"Ready\.")))
    DockerContainer.start(container)
    yield container
    container.stop()


@pytest.fixture(scope="session")
def ssm(localstack: LocalStackContainer) -> SSMClient:
    return boto3.client(
        "ssm",
        endpoint_url=localstack.get_url(),
        region_name="us-east-1",
        aws_access_key_id="test",
        aws_secret_access_key="test",
    )


@pytest.fixture(scope="session")
def secrets() -> Generator[SecretClient, None, None]:
    token = Envilder.resolve_file(str(ROOT / "envilder.json"))

    with localstack_azure_key_vault(token) as client:
        yield client


@pytest.fixture
def stored_paths(ssm: SSMClient) -> Generator[list[str], None, None]:
    """The SSM parameters a test creates, deleted once it ends."""
    paths: list[str] = []
    yield paths
    if paths:
        ssm.delete_parameters(Names=paths)


@pytest.fixture
def stored_names(secrets: SecretClient) -> Generator[list[str], None, None]:
    """The Key Vault secrets a test creates, deleted once it ends."""
    names: list[str] = []
    yield names
    for name in names:
        secrets.begin_delete_secret(name)
