import {
  DeleteParametersCommand,
  PutParameterCommand,
  SSMClient,
} from '@aws-sdk/client-ssm';
import { AwsSsmSecretProvider, EnvilderClient } from '@envilder/sdk';
import { afterEach, expect, it } from 'vitest';
import { LOCALSTACK_URL } from './aspire.setup.js';
import { withUniqueSecrets } from './mapfiles.js';

const ssm = new SSMClient({
  endpoint: LOCALSTACK_URL,
  region: 'us-east-1',
  credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
});

// The SSM parameters each test creates, deleted once it ends
let storedPaths: string[] = [];

afterEach(async () => {
  if (storedPaths.length > 0) {
    await ssm.send(new DeleteParametersCommand({ Names: storedPaths }));
  }
  storedPaths = [];
});

async function store(path: string, value: string): Promise<void> {
  storedPaths.push(path);
  await ssm.send(
    new PutParameterCommand({ Name: path, Value: value, Type: 'SecureString' }),
  );
}

it('Should_ActivateLicense_When_AspireStartsLocalStackWithTokenResolvedByEnvilder', async () => {
  // Act
  const info = await fetch(`${LOCALSTACK_URL}/_localstack/info`).then(
    (response) => response.json(),
  );

  // Assert
  expect(info.is_license_activated).toBe(true);
});

it('Should_ResolveSecretFromSsm_When_MapFilePointsToIt', async () => {
  // Arrange
  const mapFile = withUniqueSecrets(
    '../envilder.test.aws.json',
    (path) => `${path}/${crypto.randomUUID()}`,
  );
  const expected = crypto.randomUUID();

  await store(mapFile.mappings.get('DEMO_SECRET')!, expected);

  const sut = new EnvilderClient(new AwsSsmSecretProvider(ssm));

  // Act
  const actual = await sut.resolveSecrets(mapFile);

  // Assert
  expect(actual.get('DEMO_SECRET')).toBe(expected);
});
