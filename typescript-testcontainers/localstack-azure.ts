import { randomUUID } from 'node:crypto';
import { type AddressInfo, createServer as createNetServer } from 'node:net';
import { ClientSecretCredential } from '@azure/identity';
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

export type LocalStackAzure = {
  authorityHost: string;
  vaultUrl: string;
  stop: () => Promise<void>;
};

/**
 * The credential the emulator accepts: a placeholder service principal in its
 * all-zero tenant, signed in against the emulator instead of Microsoft Entra.
 */
export function emulatorCredential(
  authorityHost: string,
): ClientSecretCredential {
  return new ClientSecretCredential(TENANT_ID, CLIENT_ID, CLIENT_SECRET, {
    authorityHost,
    disableInstanceDiscovery: true,
  });
}

/**
 * Starts LocalStack for Azure and creates a vault through its Resource
 * Manager API. Returns where to sign in and the vault URL.
 */
export async function startLocalStackAzure(
  environment: Map<string, string>,
): Promise<LocalStackAzure> {
  let container: StartedTestContainer | undefined;

  const stop = async () => {
    await container?.stop();
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
    const vaultUrl = await createVault(
      emulatorCredential(authorityHost),
      authorityHost,
      `kv-${name}`,
    );

    return { authorityHost, vaultUrl, stop };
  } catch (error) {
    await stop();
    throw error;
  }
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
