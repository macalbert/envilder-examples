import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { PutParameterCommand, SSMClient } from '@aws-sdk/client-ssm';
import {
  AwsSsmSecretProvider,
  ContractValidationError,
  Envilder,
  EnvilderClient,
  MapFileParser,
} from '@envilder/sdk';
import {
  LocalstackContainer,
  type StartedLocalStackContainer,
} from '@testcontainers/localstack';
import { afterAll, afterEach, beforeAll, expect, it, vi } from 'vitest';

let localstack: StartedLocalStackContainer;
let ssm: SSMClient;

beforeAll(async () => {
  const secrets = await Envilder.resolveFile('../envilder.json');

  localstack = await new LocalstackContainer('localstack/localstack:stable')
    .withEnvironment(Object.fromEntries(secrets))
    .start();

  ssm = new SSMClient({
    endpoint: localstack.getConnectionUri(),
    region: 'us-east-1',
    credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
  });
}, 180_000);

afterAll(async () => {
  await localstack?.stop();
});

afterEach(() => {
  vi.unstubAllEnvs();
});

// Aims the SSM client that Envilder.fromMapFile builds at LocalStack
function useLocalStackAsDefaultAwsEndpoint(): void {
  vi.stubEnv('AWS_ENDPOINT_URL_SSM', localstack.getConnectionUri());
  vi.stubEnv('AWS_ACCESS_KEY_ID', 'test');
  vi.stubEnv('AWS_SECRET_ACCESS_KEY', 'test');
  vi.stubEnv('AWS_REGION', 'us-east-1');
  vi.stubEnv('AWS_SESSION_TOKEN', undefined);
  vi.stubEnv('AWS_PROFILE', undefined);
}

async function putDemoSecret(): Promise<void> {
  const mapFile = new MapFileParser().parse(
    readFileSync('../envilder.test.aws.json', 'utf8'),
  );

  await ssm.send(
    new PutParameterCommand({
      Name: mapFile.mappings.get('DEMO_SECRET'),
      Value: crypto.randomUUID(),
      Type: 'SecureString',
      Overwrite: true,
    }),
  );
}

function mapFileWithExtraKey(variable: string, secretPath: string): string {
  const mapFile = JSON.parse(readFileSync('../envilder.test.aws.json', 'utf8'));
  mapFile[variable] = secretPath;

  const path = join(mkdtempSync(join(tmpdir(), 'envilder-')), 'envilder.test.aws.json');
  writeFileSync(path, JSON.stringify(mapFile));
  return path;
}

it('Should_ActivateLicense_When_LocalStackStartsWithTokenResolvedByEnvilder', async () => {
  // Act
  const info = await fetch(
    new URL('/_localstack/info', localstack.getConnectionUri()),
  ).then((response) => response.json());

  // Assert
  expect(info.is_license_activated).toBe(true);
});

it('Should_ResolveSecretFromSsm_When_MapFilePointsToIt', async () => {
  // Arrange
  const mapFile = new MapFileParser().parse(
    readFileSync('../envilder.test.aws.json', 'utf8'),
  );
  const expected = crypto.randomUUID();

  await ssm.send(
    new PutParameterCommand({
      Name: mapFile.mappings.get('DEMO_SECRET'),
      Value: expected,
      Type: 'SecureString',
      Overwrite: true,
    }),
  );

  const sut = new EnvilderClient(new AwsSsmSecretProvider(ssm));

  // Act
  const actual = await sut.resolveSecrets(mapFile);

  // Assert
  expect(actual.get('DEMO_SECRET')).toBe(expected);
});

it('Should_ReportEveryKeyAsExists_When_ContractHoldsInSsm', async () => {
  // Arrange
  await putDemoSecret();
  useLocalStackAsDefaultAwsEndpoint();

  // Act
  const actual = await Envilder.fromMapFile('../envilder.test.aws.json').validateContract();

  // Assert
  expect(actual.isValid).toBe(true);
  expect(actual.keys.map((key) => key.status)).toEqual(['exists']);
});

it('Should_ReportKeyAsMissing_When_SecretIsNotInSsm', async () => {
  // Arrange
  await putDemoSecret();
  const mapFile = mapFileWithExtraKey('NOT_PUSHED_YET', '/envilder-examples/not-pushed-yet');
  useLocalStackAsDefaultAwsEndpoint();

  // Act
  const actual = await Envilder.fromMapFile(mapFile).validateContract();

  // Assert
  expect(actual.isValid).toBe(false);
  expect(Object.fromEntries(actual.keys.map((key) => [key.variable, key.status]))).toEqual({
    DEMO_SECRET: 'exists',
    NOT_PUSHED_YET: 'missing',
  });
});

it('Should_FailTheLoad_When_ContractValidationIsOnAndSecretIsNotInSsm', async () => {
  // Arrange
  await putDemoSecret();
  const mapFile = mapFileWithExtraKey('NOT_PUSHED_YET', '/envilder-examples/not-pushed-yet');
  useLocalStackAsDefaultAwsEndpoint();

  // Act
  const act = Envilder.fromMapFile(mapFile).withContractValidation().resolve();

  // Assert
  await expect(act).rejects.toThrow(ContractValidationError);
  await expect(act).rejects.toThrow('NOT_PUSHED_YET (/envilder-examples/not-pushed-yet): missing');
});
