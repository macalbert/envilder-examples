import {
  GetParameterCommand,
  PutParameterCommand,
  SSMClient,
} from '@aws-sdk/client-ssm';
import { Envilder } from '@envilder/sdk';
import {
  LocalstackContainer,
  type StartedLocalStackContainer,
} from '@testcontainers/localstack';
import { afterAll, beforeAll, expect, it } from 'vitest';

let localstack: StartedLocalStackContainer;
let ssm: SSMClient;

beforeAll(async () => {
  localstack = await new LocalstackContainer('localstack/localstack:stable')
    .withEnvironment(
      Object.fromEntries(await Envilder.resolveFile('../envilder.json')),
    )
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

it('Should_ReportSsmService_When_LocalStackIsRunning', async () => {
  // Act
  const response = await fetch(
    new URL('/_localstack/health', localstack.getConnectionUri()),
  );
  const health = await response.json();

  // Assert
  expect(['available', 'running']).toContain(health.services.ssm);
});

it('Should_RoundTripSecureString_When_LocalStackStartsWithResolvedToken', async () => {
  // Arrange
  await ssm.send(
    new PutParameterCommand({
      Name: '/demo/secret',
      Value: 'hunter2',
      Type: 'SecureString',
    }),
  );

  // Act
  const actual = await ssm.send(
    new GetParameterCommand({ Name: '/demo/secret', WithDecryption: true }),
  );

  // Assert
  expect(actual.Parameter?.Value).toBe('hunter2');
});
