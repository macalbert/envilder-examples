import type {
  ISecretExistenceChecker,
  ISecretProvider,
  SecretExistence,
} from '@envilder/sdk';
import { open, readVault } from './local-vault.js';

// The whole custom provider. Envilder calls these two methods and nothing
// else: it never knows the secrets come from an encrypted file.
export class LocalVaultProvider
  implements ISecretProvider, ISecretExistenceChecker
{
  constructor(
    private readonly vaultPath: string,
    private readonly key: Buffer,
  ) {}

  // ISecretProvider: return the secrets that exist, omit the others.
  async getSecrets(names: string[]): Promise<Map<string, string>> {
    const vault = readVault(this.vaultPath);
    const secrets = new Map<string, string>();
    for (const name of names) {
      const sealed = vault[name];
      if (sealed !== undefined) {
        secrets.set(name, open(this.key, sealed));
      }
    }
    return secrets;
  }

  // ISecretExistenceChecker: answer "does it exist?" without decrypting.
  // This is what lets withContractValidation() check the map file at startup.
  async checkSecretsExist(
    names: readonly string[],
  ): Promise<ReadonlyMap<string, SecretExistence>> {
    const vault = readVault(this.vaultPath);
    return new Map(
      names.map((name) => [
        name,
        { status: name in vault ? 'exists' : 'missing' } as const,
      ]),
    );
  }
}
