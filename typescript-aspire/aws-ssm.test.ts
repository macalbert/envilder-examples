import { readFileSync } from 'node:fs';
import { PutParameterCommand, SSMClient } from '@aws-sdk/client-ssm';
import {
  AwsSsmSecretProvider,
  EnvilderClient,
  MapFileParser,
} from '@envilder/sdk';
import { expect, it } from 'vitest';
import { LOCALSTACK_URL } from './aspire.setup.js';

const ssm = new SSMClient({
  endpoint: LOCALSTACK_URL,
  region: 'us-east-1',
  credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
});

it('Should_ActivateLicense_When_AspireStartsLocalStackWithTokenResolvedByEnvilder', async () => {
  // Act
  const response = await fetch(`${LOCALSTACK_URL}/_localstack/info`);
  const info = await response.json();

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

  // Act
  const envilder = new EnvilderClient(new AwsSsmSecretProvider(ssm));
  const actual = await envilder.resolveSecrets(mapFile);

  // Assert
  expect(actual.get('DEMO_SECRET')).toBe(expected);
});
