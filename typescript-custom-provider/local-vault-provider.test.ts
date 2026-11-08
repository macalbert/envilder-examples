import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { ContractValidationError, Envilder } from '@envilder/sdk';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { generateKey, seal, writeVault } from './local-vault.js';
import { LocalVaultProvider } from './local-vault-provider.js';

const MAP_FILE = '../envilder.vault.json';

describe('LocalVaultProvider', () => {
  let directory: string;
  let vaultPath: string;
  const key = generateKey();

  beforeEach(() => {
    directory = mkdtempSync(join(tmpdir(), 'envilder-vault-'));
    vaultPath = join(directory, 'vault.json');
  });

  afterEach(() => {
    rmSync(directory, { recursive: true, force: true });
    delete process.env.DB_PASSWORD;
    delete process.env.API_KEY;
  });

  it('Should_InjectDecryptedSecrets_When_VaultHoldsEveryMappedSecret', async () => {
    // Arrange
    writeVault(vaultPath, {
      '/envilder-examples/db-password': seal(key, 'correct-horse'),
      '/envilder-examples/api-key': seal(key, 'battery-staple'),
    });

    // Act
    await Envilder.fromMapFile(MAP_FILE)
      .withSecretProvider(new LocalVaultProvider(vaultPath, key))
      .withContractValidation()
      .inject();

    // Assert
    expect(process.env.DB_PASSWORD).toBe('correct-horse');
    expect(process.env.API_KEY).toBe('battery-staple');
  });

  it('Should_FailAtStartupWithoutInjecting_When_MappedSecretIsMissing', async () => {
    // Arrange
    writeVault(vaultPath, {
      '/envilder-examples/db-password': seal(key, 'correct-horse'),
    });

    // Act
    const act = Envilder.fromMapFile(MAP_FILE)
      .withSecretProvider(new LocalVaultProvider(vaultPath, key))
      .withContractValidation()
      .inject();

    // Assert
    await expect(act).rejects.toThrow(ContractValidationError);
    await expect(act).rejects.toThrow(
      'API_KEY (/envilder-examples/api-key): missing',
    );
    expect(process.env.DB_PASSWORD).toBeUndefined();
  });

  it('Should_ReportEachKeyWithoutDecrypting_When_ContractIsValidated', async () => {
    // Arrange
    writeVault(vaultPath, {
      '/envilder-examples/db-password': seal(key, 'correct-horse'),
    });
    const wrongKey = generateKey();

    // Act
    const actual = await Envilder.fromMapFile(MAP_FILE)
      .withSecretProvider(new LocalVaultProvider(vaultPath, wrongKey))
      .validateContract();

    // Assert
    expect(actual.keys.map((k) => [k.variable, k.status])).toEqual([
      ['DB_PASSWORD', 'exists'],
      ['API_KEY', 'missing'],
    ]);
  });
});
