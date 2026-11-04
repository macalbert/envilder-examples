import { Envilder } from '@envilder/sdk';
import { LocalstackContainer } from '@testcontainers/localstack';
import type { TestProject } from 'vitest/node';
import { startLocalStackAzure } from './localstack-azure';

declare module 'vitest' {
  export interface ProvidedContext {
    localstackUrl: string;
    azureAuthorityHost: string;
    vaultUrl: string;
  }
}

// One emulator of each kind for the whole test run, shared by every test file.
// Tests stay independent: each one stores its own uniquely named secrets and
// deletes them when it ends.
export default async function setup(
  project: TestProject,
): Promise<() => Promise<void>> {
  const secrets = await Envilder.resolveFile('../envilder.json');

  const [localstack, azure] = await Promise.all([
    new LocalstackContainer('localstack/localstack:stable')
      .withEnvironment(Object.fromEntries(secrets))
      .start(),
    startLocalStackAzure(secrets),
  ]);

  project.provide('localstackUrl', localstack.getConnectionUri());
  project.provide('azureAuthorityHost', azure.authorityHost);
  project.provide('vaultUrl', azure.vaultUrl);

  return async () => {
    await Promise.all([localstack.stop(), azure.stop()]);
  };
}
