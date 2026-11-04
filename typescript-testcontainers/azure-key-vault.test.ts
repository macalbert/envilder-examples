import { readFileSync } from 'node:fs';
import {
  AzureKeyVaultSecretProvider,
  Envilder,
  EnvilderClient,
  MapFileParser,
} from '@envilder/sdk';
import { afterAll, beforeAll, expect, it } from 'vitest';
import { type LocalStackAzure, startLocalStackAzure } from './localstack-azure';

let azure: LocalStackAzure;

beforeAll(async () => {
  const environment = await Envilder.resolveFile('../envilder.json');

  azure = await startLocalStackAzure(environment);
}, 240_000);

afterAll(async () => {
  await azure?.stop();
});

it('Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt', async () => {
  // Arrange
  const mapFile = new MapFileParser().parse(
    readFileSync('../envilder.test.azure.json', 'utf8'),
  );
  const expected = crypto.randomUUID();

  await azure.secrets.setSecret(mapFile.mappings.get('DEMO_SECRET')!, expected);

  const sut = new EnvilderClient(new AzureKeyVaultSecretProvider(azure.secrets));

  // Act
  const actual = await sut.resolveSecrets(mapFile);

  // Assert
  expect(actual.get('DEMO_SECRET')).toBe(expected);
});
