import { readFileSync } from 'node:fs';
import { PutParameterCommand, SSMClient } from '@aws-sdk/client-ssm';
import {
  AwsSsmSecretProvider,
  Envilder,
  EnvilderClient,
  MapFileParser,
} from '@envilder/sdk';
import {
  LocalstackContainer,
  type StartedLocalStackContainer,
} from '@testcontainers/localstack';
import { afterAll, beforeAll, expect, it } from 'vitest';

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
