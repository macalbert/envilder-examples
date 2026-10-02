import { readFileSync } from 'node:fs';
import { DefaultAzureCredential } from '@azure/identity';
import { SecretClient } from '@azure/keyvault-secrets';
import {
  AzureKeyVaultSecretProvider,
  EnvilderClient,
  MapFileParser,
} from '@envilder/sdk';
import {
  GenericContainer,
  type StartedTestContainer,
  Wait,
} from 'testcontainers';
import { afterAll, beforeAll, expect, it } from 'vitest';

const VAULT_PORT = 8443;
const TOKEN_PORT = 8080;

let lowkeyVault: StartedTestContainer;
let secrets: SecretClient;

beforeAll(async () => {
  lowkeyVault = await new GenericContainer('nagyesta/lowkey-vault:7.1.61')
    .withExposedPorts(VAULT_PORT, TOKEN_PORT)
    .withEnvironment({
      LOWKEY_ARGS: '--server.port=8443 --LOWKEY_VAULT_RELAXED_PORTS=true',
    })
    .withWaitStrategy(Wait.forLogMessage('Started LowkeyVaultApp'))
    .withStartupTimeout(180_000)
    .start();

  const host = lowkeyVault.getHost();

  process.env.IDENTITY_ENDPOINT = `http://${host}:${lowkeyVault.getMappedPort(TOKEN_PORT)}/metadata/identity/oauth2/token`;
  process.env.IDENTITY_HEADER = 'dummy';

  process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
  secrets = new SecretClient(
    `https://${host}:${lowkeyVault.getMappedPort(VAULT_PORT)}`,
    new DefaultAzureCredential(),
    { disableChallengeResourceVerification: true },
  );
}, 240_000);

afterAll(async () => {
  await lowkeyVault?.stop();
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
