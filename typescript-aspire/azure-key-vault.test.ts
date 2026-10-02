import { readFileSync } from 'node:fs';
import { Agent } from 'node:https';
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

const secrets = new SecretClient(KEYVAULT_URL, new DefaultAzureCredential(), {
  agent: new Agent({ rejectUnauthorized: false }),
  disableChallengeResourceVerification: true,
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
