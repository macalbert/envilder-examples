import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  AzureKeyVaultSecretProvider,
  ContractValidationError,
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

async function setDemoSecret(): Promise<void> {
  const mapFile = new MapFileParser().parse(
    readFileSync('../envilder.test.azure.json', 'utf8'),
  );

  await azure.secrets.setSecret(mapFile.mappings.get('DEMO_SECRET')!, crypto.randomUUID());
}

function mapFileWithExtraKey(variable: string, secretName: string): string {
  const mapFile = JSON.parse(readFileSync('../envilder.test.azure.json', 'utf8'));
  mapFile[variable] = secretName;

  const path = join(mkdtempSync(join(tmpdir(), 'envilder-')), 'envilder.test.azure.json');
  writeFileSync(path, JSON.stringify(mapFile));
  return path;
}

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

it('Should_ReportEveryKeyAsExists_When_ContractHoldsInKeyVault', async () => {
  // Arrange
  await setDemoSecret();

  // Act
  const actual = await Envilder.fromMapFile('../envilder.test.azure.json')
    .withVaultUrl(azure.secrets.vaultUrl)
    .validateContract();

  // Assert
  expect(actual.isValid).toBe(true);
  expect(actual.keys.map((key) => key.status)).toEqual(['exists']);
});

it('Should_ReportKeyAsMissing_When_SecretIsNotInKeyVault', async () => {
  // Arrange
  await setDemoSecret();
  const mapFile = mapFileWithExtraKey('NOT_PUSHED_YET', 'envilder-examples-not-pushed-yet');

  // Act
  const actual = await Envilder.fromMapFile(mapFile)
    .withVaultUrl(azure.secrets.vaultUrl)
    .validateContract();

  // Assert
  expect(actual.isValid).toBe(false);
  expect(Object.fromEntries(actual.keys.map((key) => [key.variable, key.status]))).toEqual({
    DEMO_SECRET: 'exists',
    NOT_PUSHED_YET: 'missing',
  });
});

it('Should_FailTheLoad_When_ContractValidationIsOnAndSecretIsNotInKeyVault', async () => {
  // Arrange
  await setDemoSecret();
  const mapFile = mapFileWithExtraKey('NOT_PUSHED_YET', 'envilder-examples-not-pushed-yet');

  // Act
  const act = Envilder.fromMapFile(mapFile)
    .withVaultUrl(azure.secrets.vaultUrl)
    .withContractValidation()
    .resolve();

  // Assert
  await expect(act).rejects.toThrow(ContractValidationError);
  await expect(act).rejects.toThrow('NOT_PUSHED_YET (envilder-examples-not-pushed-yet): missing');
});
