import { readFileSync } from 'node:fs';
import { DefaultAzureCredential } from '@azure/identity';
import { SecretClient } from '@azure/keyvault-secrets';
import {
  AzureKeyVaultSecretProvider,
  EnvilderClient,
  MapFileParser,
} from '@envilder/sdk';
import { expect, it } from 'vitest';
import { KEYVAULT_TOKEN_URL, KEYVAULT_URL } from './aspire.setup.js';

process.env.IDENTITY_ENDPOINT = `${KEYVAULT_TOKEN_URL}/metadata/identity/oauth2/token`;
process.env.IDENTITY_HEADER = 'dummy';

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
const secrets = new SecretClient(KEYVAULT_URL, new DefaultAzureCredential(), {
  disableChallengeResourceVerification: true,
});

it('Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt', async () => {
  // Arrange
  const mapFile = new MapFileParser().parse(
    readFileSync('../envilder.test.azure.json', 'utf8'),
  );
  const expected = crypto.randomUUID();

  await secrets.setSecret(mapFile.mappings.get('DEMO_SECRET')!, expected);

  // Act
  const envilder = new EnvilderClient(new AzureKeyVaultSecretProvider(secrets));
  const actual = await envilder.resolveSecrets(mapFile);

  // Assert
  expect(actual.get('DEMO_SECRET')).toBe(expected);
});
