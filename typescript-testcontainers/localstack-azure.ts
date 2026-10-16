import { randomUUID } from 'node:crypto';
import { createServer, type Server } from 'node:http';
import { type AddressInfo, createServer as createNetServer } from 'node:net';
import {
  ClientSecretCredential,
  DefaultAzureCredential,
} from '@azure/identity';
import { SecretClient } from '@azure/keyvault-secrets';
import {
  GenericContainer,
  type StartedTestContainer,
  Wait,
} from 'testcontainers';

// The emulator accepts any service principal in its single all-zero tenant
// and subscription. These are placeholders, not secrets.
const TENANT_ID = '00000000-0000-0000-0000-000000000000';
const SUBSCRIPTION_ID = '00000000-0000-0000-0000-000000000000';
const CLIENT_ID = 'any-app';
const CLIENT_SECRET = 'dummy';
const LOCATION = 'westeurope';

// Variables that would make DefaultAzureCredential pick another credential
// ahead of the managed identity one. Cleared while the emulator runs.
const COMPETING_ENV_VARS = [
  'AZURE_TENANT_ID',
  'AZURE_CLIENT_ID',
  'AZURE_CLIENT_SECRET',
  'AZURE_CLIENT_CERTIFICATE_PATH',
  'AZURE_USERNAME',
  'AZURE_PASSWORD',
  'AZURE_FEDERATED_TOKEN_FILE',
  'AZURE_AUTHORITY_HOST',
  'AZURE_TOKEN_CREDENTIALS',
  'IDENTITY_ENDPOINT',
  'IDENTITY_HEADER',
];

export type LocalStackAzure = {
  secrets: SecretClient;
  stop: () => Promise<void>;
};

/**
 * Starts LocalStack for Azure, creates a vault through its Resource Manager
 * API and returns a client for it.
 *
 * The client uses `DefaultAzureCredential`, like production code. It reaches
 * the emulator through a local managed identity token endpoint, configured
 * with the `IDENTITY_ENDPOINT` and `IDENTITY_HEADER` variables.
 */
export async function startLocalStackAzure(
  environment: Map<string, string>,
): Promise<LocalStackAzure> {
  const previousEnv = new Map(
    [...COMPETING_ENV_VARS].map((name) => [name, process.env[name]]),
  );
  let container: StartedTestContainer | undefined;
  let tokenServer: Server | undefined;

  const stop = async () => {
    await new Promise((resolve) => tokenServer?.close(resolve) ?? resolve(0));
    await container?.stop();
    for (const [name, value] of previousEnv) {
      if (value === undefined) delete process.env[name];
      else process.env[name] = value;
    }
  };

  try {
    // Vault URLs embed the gateway port, so the host port must be the same
    // port the emulator listens on inside the container.
    const port = await findFreePort();
    const name = randomUUID().slice(0, 8);

    container = await new GenericContainer('localstack/localstack-azure:latest')
      .withName(`localstack-azure-${name}`)
      .withExposedPorts({ container: port, host: port })
      .withEnvironment({
        ...Object.fromEntries(environment),
        GATEWAY_LISTEN: `0.0.0.0:${port}`,
        LOCALSTACK_HOST: `localhost.localstack.cloud:${port}`,
      })
      .withWaitStrategy(Wait.forLogMessage(/Ready\./))
      .withStartupTimeout(180_000)
      .start();

    const authorityHost = `https://azure.localhost.localstack.cloud:${port}`;
    const credential = new ClientSecretCredential(
      TENANT_ID,
      CLIENT_ID,
      CLIENT_SECRET,
      { authorityHost, disableInstanceDiscovery: true },
    );
    const vaultUrl = await createVault(credential, authorityHost, `kv-${name}`);

    const identityHeader = randomUUID();
    tokenServer = await startTokenServer(credential, identityHeader);
    const { port: tokenPort } = tokenServer.address() as AddressInfo;

    for (const variable of COMPETING_ENV_VARS) delete process.env[variable];
    process.env.IDENTITY_ENDPOINT = `http://127.0.0.1:${tokenPort}/msi/token`;
    process.env.IDENTITY_HEADER = identityHeader;

    return {
      secrets: new SecretClient(vaultUrl, new DefaultAzureCredential()),
      stop,
    };
  } catch (error) {
    await stop();
    throw error;
  }
}

// Speaks the App Service managed identity protocol, handing out the tokens
// the emulator issues to its placeholder service principal.
function startTokenServer(
  credential: ClientSecretCredential,
  identityHeader: string,
): Promise<Server> {
  const server = createServer(async (request, response) => {
    if (request.headers['x-identity-header'] !== identityHeader) {
      response.writeHead(401).end();
      return;
    }
    const url = new URL(request.url ?? '/', 'http://127.0.0.1');
    const resource = url.searchParams.get('resource') ?? '';
    const scope = resource.endsWith('/.default')
      ? resource
      : `${resource.replace(/\/$/, '')}/.default`;
    try {
      const { token, expiresOnTimestamp } = await credential.getToken(scope);
      response.writeHead(200, { 'Content-Type': 'application/json' }).end(
        JSON.stringify({
          access_token: token,
          expires_on: String(Math.floor(expiresOnTimestamp / 1000)),
          resource,
          token_type: 'Bearer',
        }),
      );
    } catch (error) {
      response.writeHead(500).end(String(error));
    }
  });
  return new Promise((resolve, reject) => {
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => resolve(server));
  });
}

async function createVault(
  credential: ClientSecretCredential,
  managementUrl: string,
  vaultName: string,
): Promise<string> {
  const { token } = await credential.getToken(
    'https://management.azure.com/.default',
  );
  const headers = {
    Authorization: `Bearer ${token}`,
    'Content-Type': 'application/json',
  };
  const group = `${managementUrl}/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/envilder-examples`;

  await armPut(`${group}?api-version=2021-04-01`, headers, {
    location: LOCATION,
  });
  const vault = (await armPut(
    `${group}/providers/Microsoft.KeyVault/vaults/${vaultName}?api-version=2023-07-01`,
    headers,
    {
      location: LOCATION,
      properties: {
        tenantId: TENANT_ID,
        sku: { family: 'A', name: 'standard' },
        accessPolicies: [],
      },
    },
  )) as { properties: { vaultUri: string } };

  return vault.properties.vaultUri.replace(/\/$/, '');
}

async function armPut(
  url: string,
  headers: Record<string, string>,
  body: unknown,
): Promise<unknown> {
  const response = await fetch(url, {
    method: 'PUT',
    headers,
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(
      `ARM request failed (${response.status}): ${await response.text()}`,
    );
  }
  return response.json();
}

function findFreePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = createNetServer();
    server.unref();
    server.on('error', reject);
    server.listen(0, () => {
      const { port } = server.address() as AddressInfo;
      server.close(() => resolve(port));
    });
  });
}
