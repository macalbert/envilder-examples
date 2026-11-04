import { SecretClient } from '@azure/keyvault-secrets';
import { AzureKeyVaultSecretProvider, EnvilderClient } from '@envilder/sdk';
import { afterEach, expect, inject, it } from 'vitest';
import { emulatorCredential } from './localstack-azure';
import { withUniqueSecrets } from './mapfiles';

const secrets = new SecretClient(
  inject('vaultUrl'),
  emulatorCredential(inject('azureAuthorityHost')),
);

// The Key Vault secrets each test creates, deleted once it ends
let storedNames: string[] = [];

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
