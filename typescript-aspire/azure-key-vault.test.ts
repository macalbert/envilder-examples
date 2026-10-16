import { randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { createServer, type Server } from 'node:http';
import type { AddressInfo } from 'node:net';
import {
  ClientSecretCredential,
  DefaultAzureCredential,
} from '@azure/identity';
import { SecretClient } from '@azure/keyvault-secrets';
import {
  AzureKeyVaultSecretProvider,
  EnvilderClient,
  MapFileParser,
} from '@envilder/sdk';
import { afterAll, beforeAll, expect, it } from 'vitest';
import { AZURE_PORT } from './aspire.setup.js';

// Placeholders: LocalStack for Azure accepts any service principal in its
// single all-zero tenant and subscription.
const TENANT_ID = '00000000-0000-0000-0000-000000000000';
const SUBSCRIPTION_ID = '00000000-0000-0000-0000-000000000000';
const AUTHORITY = `https://azure.localhost.localstack.cloud:${AZURE_PORT}`;

// These would make DefaultAzureCredential pick another credential before the
// managed identity one. Cleared for this file, restored afterwards.
const ENV_VARS = [
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
const previousEnv = new Map(ENV_VARS.map((name) => [name, process.env[name]]));

let tokenServer: Server;
let secrets: SecretClient;

beforeAll(async () => {
  const credential = new ClientSecretCredential(
    TENANT_ID,
    'any-app',
    'dummy',
    { authorityHost: AUTHORITY, disableInstanceDiscovery: true },
  );

  const vaultUrl = await createVault(credential);

  // MSAL rejects the emulator's authority when DefaultAzureCredential reads it
  // from AZURE_* variables, so it gets its token from this local endpoint.
  const identityHeader = randomUUID();
  tokenServer = await startTokenServer(credential, identityHeader);
  const { port } = tokenServer.address() as AddressInfo;

  for (const name of ENV_VARS) {
    delete process.env[name];
  }
  process.env.IDENTITY_ENDPOINT = `http://127.0.0.1:${port}/msi/token`;
  process.env.IDENTITY_HEADER = identityHeader;

  secrets = new SecretClient(vaultUrl, new DefaultAzureCredential());
});

afterAll(async () => {
  await new Promise((resolve) => tokenServer?.close(resolve));
  for (const [name, value] of previousEnv) {
    if (value === undefined) {
      delete process.env[name];
    } else {
      process.env[name] = value;
    }
  }
});

it('Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt', async () => {
  // Arrange
  const mapFile = new MapFileParser().parse(
    readFileSync('../envilder.test.azure.json', 'utf8'),
  );
  const expected = crypto.randomUUID();

  await secrets.setSecret(mapFile.mappings.get('DEMO_SECRET')!, expected);

  const sut = new EnvilderClient(new AzureKeyVaultSecretProvider(secrets));

  // Act
  const actual = await sut.resolveSecrets(mapFile);

  // Assert
  expect(actual.get('DEMO_SECRET')).toBe(expected);
});

// Creates a resource group and a vault through the emulated Resource Manager
// and returns the vault URL, which embeds the gateway port.
async function createVault(
  credential: ClientSecretCredential,
): Promise<string> {
  const { token } = await credential.getToken(
    'https://management.azure.com/.default',
  );
  const group = `${AUTHORITY}/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/envilder-examples`;

  await put(`${group}?api-version=2021-04-01`, token, {
    location: 'westeurope',
  });
  const vault = await put(
    `${group}/providers/Microsoft.KeyVault/vaults/kv-${randomUUID().slice(0, 8)}?api-version=2023-07-01`,
    token,
    {
      location: 'westeurope',
      properties: {
        tenantId: TENANT_ID,
        sku: { family: 'A', name: 'standard' },
        accessPolicies: [],
      },
    },
  );

  return vault.properties.vaultUri.replace(/\/$/, '');
}

async function put(url: string, token: string, body: unknown) {
  const response = await fetch(url, {
    method: 'PUT',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(`${url} failed (${response.status})`);
  }
  return response.json();
}

// Speaks the App Service managed identity protocol, handing out tokens the
// emulator issues to its placeholder service principal.
function startTokenServer(
  credential: ClientSecretCredential,
  identityHeader: string,
): Promise<Server> {
  const server = createServer(async (request, response) => {
    if (request.headers['x-identity-header'] !== identityHeader) {
      response.writeHead(401).end();
      return;
    }
    const resource =
      new URL(request.url ?? '/', 'http://127.0.0.1').searchParams.get(
        'resource',
      ) ?? '';
    try {
      const { token, expiresOnTimestamp } = await credential.getToken(
        `${resource.replace(/\/$/, '')}/.default`,
      );
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
