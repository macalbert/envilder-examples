import json
import os
import secrets
import socket
import threading
import uuid
from collections.abc import Generator
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any
from urllib.parse import parse_qs, urlparse

import requests
from azure.core.credentials import TokenCredential
from azure.identity import ClientSecretCredential
from testcontainers.core.container import DockerContainer
from testcontainers.core.wait_strategies import LogMessageWaitStrategy

IMAGE = "localstack/localstack-azure:latest"

# The emulator accepts any service principal in its single all-zero tenant and
# subscription. These are placeholders, not secrets.
ZERO_GUID = "00000000-0000-0000-0000-000000000000"

# Would make DefaultAzureCredential pick a credential ahead of the managed
# identity one, and possibly reach real Azure.
COMPETING_ENV_VARS = (
    "AZURE_TENANT_ID",
    "AZURE_CLIENT_ID",
    "AZURE_CLIENT_SECRET",
    "AZURE_CLIENT_CERTIFICATE_PATH",
    "AZURE_USERNAME",
    "AZURE_PASSWORD",
    "AZURE_FEDERATED_TOKEN_FILE",
    "AZURE_AUTHORITY_HOST",
    "AZURE_TOKEN_CREDENTIALS",
)
MANAGED_IDENTITY_ENV_VARS = ("IDENTITY_ENDPOINT", "IDENTITY_HEADER")


@contextmanager
def localstack_azure_key_vault(
    environment: dict[str, str],
) -> Generator[str, None, None]:
    """Starts LocalStack for Azure, creates a vault and yields its URL.

    While it runs, DefaultAzureCredential authenticates against the emulator.
    """
    # Vault URLs embed the gateway port, so the host port must equal the port
    # the emulator listens on inside the container.
    port = _find_free_port()
    suffix = uuid.uuid4().hex[:8]
    authority = f"https://azure.localhost.localstack.cloud:{port}"

    container = (
        DockerContainer(IMAGE)
        .with_name(f"localstack-azure-{suffix}")
        .with_bind_ports(port, port)
        .with_env("GATEWAY_LISTEN", f"0.0.0.0:{port}")
        .with_env("LOCALSTACK_HOST", f"localhost.localstack.cloud:{port}")
        .waiting_for(LogMessageWaitStrategy(r"Ready\.").with_startup_timeout(180))
    )
    for name, value in environment.items():
        container.with_env(name, value)

    previous_env = {
        name: os.environ.get(name)
        for name in (*COMPETING_ENV_VARS, *MANAGED_IDENTITY_ENV_VARS)
    }
    token_server: _ManagedIdentityTokenServer | None = None
    try:
        container.start()

        credential = ClientSecretCredential(
            ZERO_GUID,
            "any-app",
            "dummy",
            authority=authority,
            disable_instance_discovery=True,
        )
        vault_url = _create_vault(authority, credential, f"kv-{suffix}")

        token_server = _ManagedIdentityTokenServer(credential).start()
        for name in COMPETING_ENV_VARS:
            os.environ.pop(name, None)
        os.environ["IDENTITY_ENDPOINT"] = token_server.endpoint
        os.environ["IDENTITY_HEADER"] = token_server.identity_header

        yield vault_url
    finally:
        if token_server:
            token_server.stop()
        container.stop()
        for name, value in previous_env.items():
            if value is None:
                os.environ.pop(name, None)
            else:
                os.environ[name] = value


def _create_vault(authority: str, credential: TokenCredential, vault_name: str) -> str:
    token = credential.get_token("https://management.azure.com/.default").token
    headers = {"Authorization": f"Bearer {token}"}
    group_url = f"{authority}/subscriptions/{ZERO_GUID}/resourceGroups/envilder-tests"

    _put(f"{group_url}?api-version=2021-04-01", headers, {"location": "westeurope"})
    vault = _put(
        f"{group_url}/providers/Microsoft.KeyVault/vaults/{vault_name}"
        "?api-version=2023-07-01",
        headers,
        {
            "location": "westeurope",
            "properties": {
                "tenantId": ZERO_GUID,
                "sku": {"family": "A", "name": "standard"},
                "accessPolicies": [],
            },
        },
    )
    return str(vault["properties"]["vaultUri"]).rstrip("/")


def _put(url: str, headers: dict[str, str], body: dict[str, Any]) -> dict[str, Any]:
    response = requests.put(url, headers=headers, json=body, timeout=30)
    response.raise_for_status()
    result: dict[str, Any] = response.json()
    return result


def _find_free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("", 0))
        port: int = sock.getsockname()[1]
        return port


# Local token endpoint speaking the App Service managed identity protocol, so
# DefaultAzureCredential reaches the emulator through ManagedIdentityCredential.
# EnvironmentCredential can't: MSAL validates the emulator's authority against
# login.microsoftonline.com and rejects it.
class _ManagedIdentityTokenServer:
    def __init__(self, credential: TokenCredential) -> None:
        self.identity_header = secrets.token_hex(16)
        handler = self._build_handler(credential, self.identity_header)
        self._server = ThreadingHTTPServer(("127.0.0.1", 0), handler)
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)

    @property
    def endpoint(self) -> str:
        return f"http://127.0.0.1:{self._server.server_address[1]}/msi/token"

    def start(self) -> "_ManagedIdentityTokenServer":
        self._thread.start()
        return self

    def stop(self) -> None:
        self._server.shutdown()
        self._server.server_close()
        self._thread.join()

    @staticmethod
    def _build_handler(
        credential: TokenCredential, identity_header: str
    ) -> type[BaseHTTPRequestHandler]:
        class Handler(BaseHTTPRequestHandler):
            def do_GET(self) -> None:  # noqa: N802
                url = urlparse(self.path)
                resource = parse_qs(url.query).get("resource", [""])[0]
                if url.path != "/msi/token" or not resource:
                    return self._respond(404, {"error": "not_found"})
                if self.headers.get("X-IDENTITY-HEADER") != identity_header:
                    return self._respond(401, {"error": "invalid_identity_header"})

                token = credential.get_token(resource.rstrip("/") + "/.default")
                self._respond(
                    200,
                    {
                        "access_token": token.token,
                        "expires_on": str(token.expires_on),
                        "resource": resource,
                        "token_type": "Bearer",
                    },
                )

            def _respond(self, status: int, body: dict[str, str]) -> None:
                payload = json.dumps(body).encode()
                self.send_response(status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(payload)))
                self.end_headers()
                self.wfile.write(payload)

            def log_message(self, format: str, *args: object) -> None:
                pass

        return Handler
