import { randomUUID } from 'node:crypto';
import { ClientSecretCredential } from '@azure/identity';
import { SecretClient } from '@azure/keyvault-secrets';
import { AzureKeyVaultSecretProvider, EnvilderClient } from '@envilder/sdk';
import { afterEach, beforeAll, expect, it } from 'vitest';
import { AZURE_PORT } from './aspire.setup.js';
import { withUniqueSecrets } from './mapfiles.js';

// Placeholders: LocalStack for Azure accepts any service principal in its
// single all-zero tenant and subscription.
const TENANT_ID = '00000000-0000-0000-0000-000000000000';
const SUBSCRIPTION_ID = '00000000-0000-0000-0000-000000000000';
const AUTHORITY = `https://azure.localhost.localstack.cloud:${AZURE_PORT}`;

let secrets: SecretClient;

// The Key Vault secrets each test creates, deleted once it ends
let storedNames: string[] = [];

beforeAll(async () => {
  const credential = new ClientSecretCredential(
    TENANT_ID,
    'any-app',
    'dummy',
    { authorityHost: AUTHORITY, disableInstanceDiscovery: true },
  );

  const vaultUrl = await createVault(credential);

  secrets = new SecretClient(vaultUrl, credential);
});

afterEach(async () => {
  for (const name of storedNames) {
    await secrets.beginDeleteSecret(name);
  }
  storedNames = [];
});

async function store(name: string, value: string): Promise<void> {
  storedNames.push(name);
  await secrets.setSecret(name, value);
}

it('Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt', async () => {
  // Arrange
  const mapFile = withUniqueSecrets(
    '../envilder.test.azure.json',
    (name) => `${name}-${crypto.randomUUID()}`,
  );
  const expected = crypto.randomUUID();

  await store(mapFile.mappings.get('DEMO_SECRET')!, expected);

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
